using System;
using System.Collections.ObjectModel;
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
using LottieViewConvert.Helper;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
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
    public TgsDownloadViewModel() : base(Resources.Download, MaterialIconKind.SendCheck, 2)
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
        
        // initialize properties
        SaveLocation = Path.Combine(AppContext.BaseDirectory, "TelegramStickers");
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

    public string SelectionCountText => $"{Resources.Selected} {StickerItems.Count(x => x.IsSelected)} / {StickerItems.Count}";
    public string SelectedCountText => $"{Resources.Selected} {StickerItems.Count(x => x.IsSelected)} {Resources.Sticker}";
    public string SaveLocationText => $"{Resources.SaveTo}: {(string.IsNullOrWhiteSpace(SaveLocation) ? $"{Resources.UnSelected}" : SaveLocation)}";

    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAllSelectionCommand { get; }
    public ReactiveCommand<StickerItemViewModel, Unit> ToggleStickerSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSaveLocationCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSelectedStickersCommand { get; }

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
                .WithActionButton(Resources.Open, _ => { OpenSavedFolder(SaveLocation); })
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
    
    private void OpenSavedFolder(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Open saved folder failed: {ex}");
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

    public string FilePath 
    { 
        get => _filePath;
        set 
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(IsTgsFile));
            this.RaisePropertyChanged(nameof(IsImageFile));
            this.RaisePropertyChanged(nameof(FileExtension));
            
            if (IsImageFile)
            {
                _ = LoadPreviewImageAsync();
            }
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
    
    private static bool IsImageExtension(string extension)
    {
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico" };
        return imageExtensions.Contains(extension.ToLowerInvariant());
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
            var bitmap = await WebPImageService.Instance.LoadImageAsync(FilePath);
            
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