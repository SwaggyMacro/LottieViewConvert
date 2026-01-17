using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using LottieViewConvert.Helper;
using LottieViewConvert.Helper.Convert;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using LottieViewConvert.Utils;
using Material.Icons;
using ReactiveUI;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class TgsDownloadViewModel : Page, IDisposable
{
    private TelegramStickerEmojiDownloader? _downloader;
    private CancellationTokenSource? _cancellationTokenSource;
    private string _tempDownloadPath;
    private static readonly Regex TelegramLinkRegex = new(
        @"t\.me/(?:addstickers|addemoji)/([^/?#\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public TgsDownloadViewModel() : base(Resources.Telegram, MaterialIconKind.SendCheck, 2)
    {
        _tempDownloadPath = Path.Combine(Path.GetTempPath(), "TgsDownload");
        
        StickerItems = [];

        _ = InitTelegramBot();
        
        // initialize commands
        DownloadCommand = ReactiveCommand.CreateFromTask(ExecuteDownload, 
            this.WhenAnyValue(x => x.StickerInput, x => x.IsDownloading)
                .Select(x => !string.IsNullOrWhiteSpace(x.Item1) && !x.Item2));
                
        CancelDownloadCommand = ReactiveCommand.Create(CancelDownload);
        ToggleAllSelectionCommand = ReactiveCommand.Create(ToggleAllSelection);
        ToggleStickerSelectionCommand = ReactiveCommand.Create<StickerItemViewModel>(ToggleStickerSelection);
        SelectSaveLocationCommand = ReactiveCommand.CreateFromTask(SelectSaveLocation);
        
        var canSaveObservable = this.WhenAnyValue(x => x.SaveLocation)
            .CombineLatest(
                Observable.FromEventPattern<System.Collections.Specialized.NotifyCollectionChangedEventHandler, System.Collections.Specialized.NotifyCollectionChangedEventArgs>(
                        h => StickerItems.CollectionChanged += h,
                        h => StickerItems.CollectionChanged -= h)
                    .Select(_ => Unit.Default)
                    .StartWith(Unit.Default),
                (saveLocation, _) => !string.IsNullOrWhiteSpace(saveLocation) && StickerItems.Count > 0);

        SaveSelectedStickersCommand = ReactiveCommand.CreateFromTask(SaveSelectedStickers, canSaveObservable);
        SaveAsGifCommand = ReactiveCommand.CreateFromTask(SaveSelectedStickersAsGif, this.WhenAnyValue(x => x.CanSaveAsGif));
        
        // initialize properties
        SaveLocation = Path.Combine(AppContext.BaseDirectory, "SavedStickers");
        if (!Directory.Exists(SaveLocation))
        {
            Directory.CreateDirectory(SaveLocation);
        }
        
        // subscribe to collection changes
        StickerItems.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasStickers));
            this.RaisePropertyChanged(nameof(ShowWaitingState));
            this.RaisePropertyChanged(nameof(SelectionCountText));
            this.RaisePropertyChanged(nameof(HasSelectedStickers));
            this.RaisePropertyChanged(nameof(CanSave));
            this.RaisePropertyChanged(nameof(SelectedCountText));
        };
    }

    private string _stickerInput = "";
    public string StickerInput
    {
        get => _stickerInput;
        set => this.RaiseAndSetIfChanged(ref _stickerInput, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
    }

    private double _overallProgress;
    public double OverallProgress
    {
        get => _overallProgress;
        set => this.RaiseAndSetIfChanged(ref _overallProgress, value);
    }

    private string _downloadStatusText = "";
    public string DownloadStatusText
    {
        get => _downloadStatusText;
        set => this.RaiseAndSetIfChanged(ref _downloadStatusText, value);
    }

    private string _progressText = "";
    public string ProgressText
    {
        get => _progressText;
        set => this.RaiseAndSetIfChanged(ref _progressText, value);
    }

    private string _stickerPackTitle = "";
    public string StickerPackTitle
    {
        get => _stickerPackTitle;
        set => this.RaiseAndSetIfChanged(ref _stickerPackTitle, value);
    }

    private bool _isAllSelected;
    public bool IsAllSelected
    {
        get => _isAllSelected;
        set => this.RaiseAndSetIfChanged(ref _isAllSelected, value);
    }

    private string _saveLocation = "";
    public string SaveLocation
    {
        get => _saveLocation;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _saveLocation, value);
            this.RaisePropertyChanged(nameof(SaveLocationText));
            this.RaisePropertyChanged(nameof(CanSave));
            this.RaisePropertyChanged(nameof(CanSaveAsGif));
        }
    }
    
    private bool _isLoadingMetadata;
    public bool IsLoadingMetadata
    {
        get => _isLoadingMetadata;
        set => this.RaiseAndSetIfChanged(ref _isLoadingMetadata, value);
    }

    private double _metadataProgress;
    public double MetadataProgress
    {
        get => _metadataProgress;
        set => this.RaiseAndSetIfChanged(ref _metadataProgress, value);
    }

    private string _metadataProgressText = "";
    public string MetadataProgressText
    {
        get => _metadataProgressText;
        set => this.RaiseAndSetIfChanged(ref _metadataProgressText, value);
    }

    public ObservableCollection<StickerItemViewModel> StickerItems { get; }
    
    
    public bool CanDownload => !string.IsNullOrWhiteSpace(StickerInput) && !IsDownloading;
    public bool HasStickers => StickerItems.Count > 0;
    public bool ShowWaitingState => !IsDownloading && StickerItems.Count == 0;
    public bool HasSelectedStickers => StickerItems.Any(x => x.IsSelected);
    public bool CanSave => HasSelectedStickers && !string.IsNullOrWhiteSpace(SaveLocation);
    public ReactiveCommand<Unit, Unit> SaveSelectedStickersCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsGifCommand { get; }
    public bool HasGifEligibleStickers => StickerItems.Any(x => x.IsSelected && ((x.IsImageFile && x.FileExtension.Equals(".webp", StringComparison.OrdinalIgnoreCase)) || (x.IsVideoFile && x.FileExtension.Equals(".webm", StringComparison.OrdinalIgnoreCase))));
    public bool CanSaveAsGif => HasGifEligibleStickers && !string.IsNullOrWhiteSpace(SaveLocation);
    private bool _isSavingGif;
    public bool IsSavingGif
    {
        get => _isSavingGif;
        set => this.RaiseAndSetIfChanged(ref _isSavingGif, value);
    }
    private double _saveGifProgress;
    public double SaveGifProgress
    {
        get => _saveGifProgress;
        set => this.RaiseAndSetIfChanged(ref _saveGifProgress, value);
    }
    
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => this.RaiseAndSetIfChanged(ref _playbackSpeed, Math.Max(0.25, Math.Min(4.0, value)));
    }

    public string SelectionCountText => $"{Resources.Selected} {StickerItems.Count(x => x.IsSelected)} / {StickerItems.Count}";
    public string SelectedCountText => $"{Resources.Selected} {StickerItems.Count(x => x.IsSelected)} {Resources.Sticker}";
    public string SaveLocationText => $"{Resources.SaveTo}: {(string.IsNullOrWhiteSpace(SaveLocation) ? $"{Resources.UnSelected}" : SaveLocation)}";

    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAllSelectionCommand { get; }
    public ReactiveCommand<StickerItemViewModel, Unit> ToggleStickerSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSaveLocationCommand { get; }

    private async Task InitTelegramBot()
    {
        try
        {
            var config = new ConfigService();
            await config.LoadConfigAsync();
            var token = config.GetConfig().TelegramBotToken;
            var proxyUrl = config.GetConfig().ProxyAddress;
            if (string.IsNullOrWhiteSpace(token)) return;

            _downloader = new TelegramStickerEmojiDownloader(token, string.IsNullOrWhiteSpace(proxyUrl) ? null : proxyUrl);
            // subscribe to downloader events
            _downloader.OverallProgressChanged += OnOverallProgressChanged;
            _downloader.DownloadFileCompleted += OnDownloadFileCompleted;
            _downloader.DownloadProgressChanged += OnDownloadProgressChanged;
            _downloader.MetadataProgressChanged += OnMetadataProgressChanged;
        } catch (Exception ex)
        {
            Logger.Error($"Failed to initialize Telegram bot: {ex}");
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Error)
                .WithContent($"Failed to initialize Telegram bot: {ex}")
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();
        }
    }

    private async Task ExecuteDownload()
    {
        await InitTelegramBot();
        if (_downloader is null)
        {
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Error)
                .WithContent(Resources.PleaseSettingTelegramBotToken)
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();
            return;
        }
        
        SaveLocation = Path.Combine(Path.Combine(AppContext.BaseDirectory, "SavedStickers"), ExtractStickerEmojiName(StickerInput));
         
        try
        {
            IsDownloading = true;
            IsLoadingMetadata = false;
            DownloadStatusText = Resources.ReadyToDownloadDotDotDot;
            OverallProgress = 0;
            MetadataProgress = 0;
            ProgressText = "";
            MetadataProgressText = "";
            StickerItems.Clear();
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // ensure temp directory exists
            Directory.CreateDirectory(_tempDownloadPath);
            
            // get sticker set name from input
            var stickerSetName = ExtractStickerEmojiName(StickerInput);
            if (string.IsNullOrWhiteSpace(stickerSetName))
            {
                DownloadStatusText = Resources.InvalidStickerOrEmojiSetNameOrLink;
                return;
            }
            
            StickerPackTitle = $"{Resources.StickerOrEmojiSet}: {stickerSetName}";
            DownloadStatusText = $"{Resources.DownloadingStickerOrEmojiSet}...";
            
            await _downloader.DownloadAsync(
                stickerSetName, 
                _tempDownloadPath, 
                maxConcurrency: 4, 
                _cancellationTokenSource.Token);
                
            DownloadStatusText = $"{Resources.DownloadCompleted}!";
            
            await Task.Delay(1000);
        }
        catch (OperationCanceledException)
        {
            DownloadStatusText = Resources.DownloadCancelled;
            Logger.Info("Download cancelled by user.");
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.DownloadCancelled)
                .WithContent(Resources.DownloadCancelled)
                .OfType(NotificationType.Warning)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"{Resources.DownloadFailed}: {ex.Message}";
            Logger.Error($"Download failed: {ex}");
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.DownloadFailed)
                .WithContent(DownloadStatusText)
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();
        }
        finally
        {
            IsDownloading = false;
            IsLoadingMetadata = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void CancelDownload()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void ToggleAllSelection()
    {
        if (!StickerItems.Any())
        {
            Logger.Info("No stickers to select");
            return;
        }
    
        bool newState = !IsAllSelected;
        IsAllSelected = newState;
    
        foreach (var item in StickerItems)
        {
            item.IsSelected = newState;
        }
    
        UpdateSelectionProperties();
    }

    private void ToggleStickerSelection(StickerItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        
        // update the select all selection state
        IsAllSelected = StickerItems.Count > 0 && StickerItems.All(x => x.IsSelected);
        
        UpdateSelectionProperties();
    }

    private void UpdateSelectionProperties()
    {
        this.RaisePropertyChanged(nameof(HasSelectedStickers));
        this.RaisePropertyChanged(nameof(CanSave));
        this.RaisePropertyChanged(nameof(CanSaveAsGif));
        this.RaisePropertyChanged(nameof(HasGifEligibleStickers));
        this.RaisePropertyChanged(nameof(SelectionCountText));
        this.RaisePropertyChanged(nameof(SelectedCountText));
    }

    private async Task SelectSaveLocation()
    {
        try
        {
            Window mainWindow = Global.GetMainWindow();

            var selectedFolder = await FilePickerHelper.SelectFolderAsync(
                mainWindow, 
                Resources.ChooseSaveLocation);

            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                SaveLocation = selectedFolder;
                DownloadStatusText = $"{Resources.SaveTo}: {SaveLocation}";
            }
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"{Resources.ChooseSaveLocation} {Resources.Failed}: {ex.Message}";
            Logger.Error($"Choose save location failed {ex}");
        }
    }

    private async Task SaveSelectedStickers()
    {
        await Task.Yield();
        try
        {
            Directory.CreateDirectory(SaveLocation);
            
            var selectedStickers = StickerItems.Where(x => x.IsSelected).ToList();
            int savedCount = 0;
            
            foreach (var sticker in selectedStickers)
            {
                string destinationPath = Path.Combine(SaveLocation, sticker.FileName);
                File.Copy(sticker.FilePath, destinationPath, overwrite: true);
                savedCount++;
            }
            
            DownloadStatusText = $"{Resources.SaveSucceeded} {savedCount} {Resources.Sticker} {SaveLocation}";
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.SaveSucceeded)
                .WithContent(DownloadStatusText)
                .OfType(NotificationType.Success)
                .WithActionButton(Resources.Open, _ => { FolderUtil.OpenSavedFolder(SaveLocation); })
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();

        }
        catch (Exception ex)
        {
            DownloadStatusText = $"{Resources.SaveFailed}: {ex.Message}";
            Logger.Error($"Save selected stickers failed: {ex}");
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.SaveFailed)
                .WithContent(DownloadStatusText)
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(5))
                .Queue();
        }
    }
    
    private async Task<double> GetVideoFrameRateAsync(string videoPath, CommandExecutor executor)
    {
        try
        {
            // Use ffprobe to get the frame rate
            var args = new List<string>
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=r_frame_rate",
                "-of", "default=noprint_wrappers=1:nokey=1",
                videoPath
            };
            
            var tempOutput = Path.Combine(Path.GetTempPath(), $"fps_{Guid.NewGuid()}.txt");
            try
            {
                // Execute ffprobe and capture output
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a)),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // Parse frame rate (format is usually "30/1" or "30000/1001")
                    var parts = output.Trim().Split('/');
                    if (parts.Length == 2 && 
                        double.TryParse(parts[0], out var numerator) && 
                        double.TryParse(parts[1], out var denominator) &&
                        denominator > 0)
                    {
                        return numerator / denominator;
                    }
                }
            }
            finally
            {
                if (File.Exists(tempOutput))
                    File.Delete(tempOutput);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get video frame rate: {ex}");
        }
        
        // Default to 30 fps if detection fails
        return 30.0;
    }
    
    private async Task SaveSelectedStickersAsGif()
    {
        // initialize UI state for GIF saving
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsSavingGif = true;
            SaveGifProgress = 0;
        });

        var selectedStickers = StickerItems
            .Where(x => x.IsSelected && (x.IsImageFile || x.IsVideoFile))
            .ToList();
        Directory.CreateDirectory(SaveLocation);

        try
        {
            // Capture the playback speed value to avoid threading issues
            var playbackSpeed = PlaybackSpeed;
            
            await Task.Run(async () =>
            {
                var totalCount = selectedStickers.Count;
                var processed = 0;
                var semaphore = new SemaphoreSlim(6);
                var tasks = selectedStickers.Select(async sticker =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(sticker.FileName);
                        var destPath = Path.Combine(SaveLocation, fileName + ".gif");

                        // reuse existing conversion logic (Magick or ffmpeg + Magick)
                        if (sticker.IsImageFile)
                        {
                            using var imgList = new MagickImageCollection();
                            await imgList.ReadAsync(sticker.FilePath);
                            // Apply playback speed adjustment for animated images (like WebP)
                            if (imgList.Count > 1)
                            {
                                foreach (var img in imgList)
                                {
                                    if (img.AnimationDelay > 0)
                                    {
                                        img.AnimationDelay = (uint)Math.Max(1, img.AnimationDelay / playbackSpeed);
                                    }
                                }
                            }
                            await imgList.WriteAsync(destPath);
                        }
                        else
                        {
                            // webm conversion via ffmpeg + MagickImageCollection
                            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                            Directory.CreateDirectory(tempDir);
                            var exec = new CommandExecutor();
                            
                            // Get the frame rate from the WebM file
                            var fps = await GetVideoFrameRateAsync(sticker.FilePath, exec);
                            
                            // Apply speed adjustment using ffmpeg's setpts filter
                            // For speed adjustment: setpts=PTS/speed (e.g., PTS/2 for 2x speed, PTS*2 for 0.5x speed)
                            var ptsMultiplier = 1.0 / playbackSpeed; // Inverse for setpts filter
                            var videoFilter = $"setpts={ptsMultiplier:F4}*PTS,format=yuva420p";
                            
                            // Extract frames with speed adjustment applied
                            var extractArgs = new List<string>
                            {
                                "-hide_banner", "-y",
                                "-i", sticker.FilePath,
                                "-vf", videoFilter,
                                "-c:v", "png", "-pix_fmt", "rgba",
                                Path.Combine(tempDir, "frame_%03d.png")
                            };
                            if (await exec.ExecuteAsync("ffmpeg", extractArgs, Path.GetDirectoryName(sticker.FilePath) ?? string.Empty))
                            {
                                using var imgList = new MagickImageCollection();
                                foreach (var f in Directory.GetFiles(tempDir, "frame_*.png").OrderBy(f => f))
                                {
                                    var img = new MagickImage(f)
                                    { BackgroundColor = MagickColors.Transparent };
                                    img.Alpha(AlphaOption.Set);
                                    imgList.Add(img);
                                }
                                // Calculate delay based on the actual frame rate
                                // Since we've already adjusted speed in ffmpeg, use the original fps for delay
                                var delay = fps > 0 ? (uint)Math.Max(1, Math.Round(100.0 / fps)) : 3;
                                foreach (var img in imgList)
                                {
                                    img.AnimationDelay = delay;
                                    img.Format = MagickFormat.Gif;
                                    img.GifDisposeMethod = GifDisposeMethod.Background;
                                    img.BackgroundColor = MagickColors.Transparent;
                                }
                                await imgList.WriteAsync(destPath);
                                Directory.Delete(tempDir, true);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    var count = Interlocked.Increment(ref processed);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        SaveGifProgress = (double)count / totalCount * 100
                    );
                }).ToList();
                await Task.WhenAll(tasks);
            });

            // success: finalize on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SaveGifProgress = 100;
                DownloadStatusText = $"{Resources.SaveSucceeded} {selectedStickers.Count} GIF {Resources.Sticker} {SaveLocation}";
                Global.GetToastManager()
                    .CreateToast()
                    .WithTitle(Resources.SaveSucceeded)
                    .WithContent(DownloadStatusText)
                    .OfType(NotificationType.Success)
                    .WithActionButton(Resources.Open, _ => FolderUtil.OpenSavedFolder(SaveLocation))
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(5))
                    .Queue();
                IsSavingGif = false;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DownloadStatusText = $"{Resources.SaveFailed}: {ex.Message}";
                Logger.Error($"Save as GIF failed: {ex}");
                Global.GetToastManager()
                    .CreateToast()
                    .WithTitle(Resources.SaveFailed)
                    .WithContent(DownloadStatusText)
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(5))
                    .Queue();
                IsSavingGif = false;
            });
        }
    }

    private string ExtractStickerEmojiName(string input)
    {
        var match = TelegramLinkRegex.Match(input);
        return match.Success ? match.Groups[1].Value : input;
    }
    
    private void OnMetadataProgressChanged(int fetchedCount, int totalCount)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoadingMetadata = totalCount > 0 && fetchedCount < totalCount;
            
            if (totalCount > 0)
            {
                MetadataProgress = (double)fetchedCount / totalCount * 100;
                MetadataProgressText = $"{Resources.FetchingMetadata}: {fetchedCount} / {totalCount}";
            }
            
            if (fetchedCount == totalCount && totalCount > 0)
            {
                IsLoadingMetadata = false;
                DownloadStatusText = $"{Resources.StartingDownload}...";
            }
        });
    }

    private void OnOverallProgressChanged(long totalDownloadedBytes, long grandTotalBytes)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (grandTotalBytes <= 0) return;
            OverallProgress = (double)totalDownloadedBytes / grandTotalBytes * 100;
            ProgressText = $"{FormatBytes(totalDownloadedBytes)} / {FormatBytes(grandTotalBytes)}";
        });
    }

    private void OnDownloadFileCompleted(string filePath)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var fileInfo = new FileInfo(filePath);
            var stickerItem = new StickerItemViewModel();
            
            stickerItem.FileName = fileInfo.Name;
            stickerItem.FileSize = fileInfo.Length;
            stickerItem.FileSizeText = FormatBytes(fileInfo.Length);
            stickerItem.IsSelected = false;
            
            stickerItem.FilePath = filePath;
        
            // subscribe to selection changes
            stickerItem.WhenAnyValue(x => x.IsSelected)
                .Subscribe(_ => 
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateSelectionProperties();
                        // update the select all state
                        IsAllSelected = StickerItems.Count > 0 && StickerItems.All(x => x.IsSelected);
                    });
                });
        
            StickerItems.Add(stickerItem);
        });
    }

    private void OnDownloadProgressChanged(string filePath, long downloadedBytes, long totalBytes)
    {
        // single file progress update
        // no need now
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        const int scale = 1024;
        string[] orders = ["B", "KB", "MB", "GB"];
        
        for (var i = orders.Length - 1; i >= 0; i--)
        {
            var orderSize = (long)Math.Pow(scale, i);
            if (bytes >= orderSize)
            {
                return $"{(double)bytes / orderSize:0.##} {orders[i]}";
            }
        }
        
        return $"{bytes} B";
    }
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        WebPImageService.Instance.ClearCache(); 
    }
}

/// <summary>
/// Model for each sticker item in the download list.
/// </summary>
public class StickerItemViewModel : ReactiveObject
{
    private bool _isSelected;
    private string _filePath = "";
    private Bitmap? _previewImage;
    private bool _isImageLoading;
    private bool _imageLoadFailed;
    private static readonly VideoCoverExtractor SVideoCoverExtractor = new(new CommandExecutor());

    public string FilePath
    { 
        get => _filePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(IsTgsFile));
            this.RaisePropertyChanged(nameof(IsImageFile));
            this.RaisePropertyChanged(nameof(IsVideoFile));
            this.RaisePropertyChanged(nameof(FileExtension));
            
            if (IsImageFile || IsVideoFile)
            {
                _ = LoadPreviewImageAsync();
            }
            this.RaisePropertyChanged(nameof(IsPreview));
        }
    }
    
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string FileSizeText { get; set; } = "";
    
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set => this.RaiseAndSetIfChanged(ref _previewImage, value);
    }

    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set => this.RaiseAndSetIfChanged(ref _isImageLoading, value);
    }

    public bool ImageLoadFailed
    {
        get => _imageLoadFailed;
        private set => this.RaiseAndSetIfChanged(ref _imageLoadFailed, value);
    }
    
    public string FileExtension => Path.GetExtension(FilePath);
    
    public bool IsTgsFile => Path.GetExtension(FilePath).Equals(".tgs", StringComparison.OrdinalIgnoreCase);
    
    public bool IsImageFile => !IsTgsFile && IsImageExtension(Path.GetExtension(FilePath));
    public bool IsVideoFile => !IsTgsFile && IsVideoExtension(FileExtension);
    public bool IsPreview => IsImageFile || IsVideoFile;
    private static bool IsImageExtension(string extension)
    {
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico" };
        return imageExtensions.Contains(extension.ToLowerInvariant());
    }
    private static bool IsVideoExtension(string extension)
    {
        var videoExt = new[] { ".webm" };
        return videoExt.Contains(extension.ToLowerInvariant());
    }

    private async Task LoadPreviewImageAsync()
    {
        if (string.IsNullOrEmpty(FilePath))
            return;

        IsImageLoading = true;
        ImageLoadFailed = false;
        PreviewImage = null;

        try
        {
            Bitmap? bitmap = null;
            if (IsVideoFile)
            {
                // extract first frame
                var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
                var ok = await SVideoCoverExtractor.ExtractCoverFrameAsync(FilePath, tempFile);
                if (ok && File.Exists(tempFile))
                {
                    bitmap = await WebPImageService.Instance.LoadImageAsync(tempFile);
                    File.Delete(tempFile);
                }
            }
            else
            {
                bitmap = await WebPImageService.Instance.LoadImageAsync(FilePath);
            }
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage = bitmap;
                ImageLoadFailed = bitmap == null;
                IsImageLoading = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load preview for {FilePath}: {ex.Message}");
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ImageLoadFailed = true;
                IsImageLoading = false;
            });
        }
    }
}