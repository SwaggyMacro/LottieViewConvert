using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Lottie;
using LottieViewConvert.Helper.Convert;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using ReactiveUI;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class FactoryViewModel : Page
{
    private string? _selectedFolder;
    public string? SelectedFolder
    {
        get => _selectedFolder;
        set => this.RaiseAndSetIfChanged(ref _selectedFolder, value);
    }

    public ObservableCollection<FileItemModel> FileItems { get; } = [];

    private FileItemModel? _selectedFileItem;
    public FileItemModel? SelectedFileItem
    {
        get => _selectedFileItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFileItem, value);
            if (value != null) 
                SelectedFilePath = value.FullPath;
        }
    }

    private string _selectedFilePath = "/Assets/CarBrandsSticker_Cadillac.tgs";
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
    }

    private int _fps = 60;
    public int Fps
    {
        get => _fps;
        set => this.RaiseAndSetIfChanged(ref _fps, value);
    }

    private double _speed = 1;
    public double Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    // Conversion formats
    public ObservableCollection<string> ConversionFormats { get; } =
    [
        "GIF",
        "Png",
        "Webp",
        "Apng",
        "Mp4",
        "Webm",
        "Avif",
        "Mkv"
    ];

    private string _selectedFormat = "GIF";
    public string SelectedFormat
    {
        get => _selectedFormat;
        set => this.RaiseAndSetIfChanged(ref _selectedFormat, value);
    }

    // Quality and output size
    private int _quality = 75;
    public int Quality
    {
        get => _quality;
        set => this.RaiseAndSetIfChanged(ref _quality, value);
    }

    private int _outputWidth = 512;
    public int OutputWidth
    {
        get => _outputWidth;
        set => this.RaiseAndSetIfChanged(ref _outputWidth, value);
    }

    private int _outputHeight = 512;
    public int OutputHeight
    {
        get => _outputHeight;
        set => this.RaiseAndSetIfChanged(ref _outputHeight, value);
    }
    
    private bool _isFileListVisible;
    public bool IsFileListVisible
    {
        get => _isFileListVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileListVisible, value);
    }

    private string? _outputFolder;
    public string? OutputFolder
    {
        get => _outputFolder;
        set => this.RaiseAndSetIfChanged(ref _outputFolder, value);
    }

    private bool _isConverting;
    public bool IsConverting
    {
        get => _isConverting;
        set => this.RaiseAndSetIfChanged(ref _isConverting, value);
    }

    private double _overallProgress;
    public double OverallProgress
    {
        get => _overallProgress;
        set => this.RaiseAndSetIfChanged(ref _overallProgress, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }
    
    private bool _isLottieViewPaused;

    public bool IsLottieViewPaused
    {
        get => _isLottieViewPaused;
        set => this.RaiseAndSetIfChanged(ref _isLottieViewPaused, value);
    }

    private int _lottieViewCurrentFrame;

    public int LottieViewCurrentFrame
    {
        get => _lottieViewCurrentFrame;
        set => this.RaiseAndSetIfChanged(ref _lottieViewCurrentFrame, value);
    }

    private int _lottieViewTotalFrames;

    public int LottieViewTotalFrames
    {
        get => _lottieViewTotalFrames;
        set
        {
            this.RaiseAndSetIfChanged(ref _lottieViewTotalFrames, value);
        }
    }

    // Commands
    public ReactiveCommand<Unit, Unit> BrowseSourceFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseOutputFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> StartConversionCommand { get; }
    public ReactiveCommand<Unit, Unit> StopConversionCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> LottieViewPauseResumeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCurrentFrameCommand { get; }

    private CancellationTokenSource? _cancellationTokenSource;

    private void PopulateFilesFromFolder(string folder)
    {
        FileItems.Clear();
        var files = Directory.GetFiles(folder)
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".tgs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in files)
        {
            FileItems.Add(new FileItemModel
            {
                FileName = Path.GetFileName(file),
                FullPath = file,
                Status = ConversionStatus.Pending,
                Progress = 0
            });
        }

        if (FileItems.Any())
            SelectedFileItem = FileItems.First();
        
        IsFileListVisible = FileItems.Count > 0;
        GenerateOutputFolder();
    }
    
    public async Task HandleFolderDrop(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;
        SelectedFolder = folder;
    
        await Task.Run(() =>
        {
            var files = Directory.GetFiles(folder)
                .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".tgs", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName)
                .Select(file => new FileItemModel
                {
                    FileName = Path.GetFileName(file),
                    FullPath = file,
                    Status = ConversionStatus.Pending,
                    Progress = 0
                })
                .ToList();
            
            Dispatcher.UIThread.Post(() =>
            {
                FileItems.Clear();
                foreach (var fileItem in files)
                {
                    FileItems.Add(fileItem);
                }

                if (FileItems.Any())
                    SelectedFileItem = FileItems.First();
            
                IsFileListVisible = FileItems.Count > 0;
                GenerateOutputFolder();
            });
        });
    }
    
    [Obsolete("Obsolete")]
    private async Task BrowseSourceFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return;
        var window = lifetime.MainWindow;
        if (window == null)
            return;
        var dlg = new OpenFolderDialog { Title = Resources.SelectLottieFolder };
        var result = await dlg.ShowAsync(window);
        if (string.IsNullOrWhiteSpace(result) || !Directory.Exists(result))
            return;
        SelectedFolder = result;
        PopulateFilesFromFolder(result);
    }
    
    [Obsolete("Obsolete")]
    private async Task BrowseOutputFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return;
        var window = lifetime.MainWindow;
        if (window == null)
            return;
        var dlg = new OpenFolderDialog { Title = Resources.SelectOutputFolder };
        var result = await dlg.ShowAsync(window);
        if (string.IsNullOrWhiteSpace(result) || !Directory.Exists(result))
            return;
        OutputFolder = result;
    }

    private async Task StartConversionAsync()
    {
        await Task.Run(async () =>
        {
            if (string.IsNullOrWhiteSpace(OutputFolder) || !FileItems.Any())
            {
                StatusText = Resources.PleaseChooseOutputFolderAndFiles;
                return;
            }

            IsConverting = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                Directory.CreateDirectory(OutputFolder);
                
                foreach (var item in FileItems)
                {
                    item.Status = ConversionStatus.Pending;
                    item.Progress = 0;
                }

                var totalFiles = FileItems.Count;
                var completedFiles = 0;

                StatusText =  $"{Resources.StartBatchConvert} {totalFiles}";

                foreach (var fileItem in FileItems)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    await ConvertSingleFileAsync(fileItem, _cancellationTokenSource.Token);

                    completedFiles++;
                    OverallProgress = (double)completedFiles / totalFiles * 100;
                    StatusText = $"{completedFiles}/{totalFiles}";
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var successCount = FileItems.Count(f => f.Status == ConversionStatus.Success);
                    var failedCount = FileItems.Count(f => f.Status == ConversionStatus.Failed);

                    StatusText = $"{Resources.BatchConvertDone}! {Resources.Succeeded}: {successCount}, {Resources.Failed}: {failedCount}";
                    
                    Dispatcher.UIThread.Post(() =>
                    {
                        Global.GetToastManager().CreateToast()
                            .WithTitle(Resources.BatchConvertDone)
                            .WithContent(StatusText)
                            .OfType(NotificationType.Success)
                            .WithActionButton(Resources.OpenOutputFolder, _ => OpenOutputFolder(), true)
                            .Dismiss().ByClicking()
                            .Dismiss().After(TimeSpan.FromSeconds(5))
                            .Queue();
                    });
                }
            }
            catch (Exception ex)
            {
                StatusText = $"{Resources.BatchConvertError}: {ex.Message}";
                Logger.Error($"Batch conversion failed: {ex.Message}");
            }
            finally
            {
                IsConverting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        });
    }

    private void OpenOutputFolder()
    {
        try
        {
            var uri = new Uri(OutputFolder!);
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.LocalPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Error)
                .WithContent($"Failed to open folder: {ex.Message}")
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
        }
    }
    
    private async Task ConvertSingleFileAsync(FileItemModel fileItem, CancellationToken cancellationToken)
    {
        try
        {
            fileItem.Status = ConversionStatus.Converting;
            fileItem.Progress = 0;

            var converter = new Converter(
                fileItem.FullPath,
                OutputFolder!,
                new ConversionOptions
                {
                    PlaySpeed = Speed,
                    Width = OutputWidth,
                    Height = OutputHeight,
                    Fps = Fps,
                    Quality = Quality
                }
            );

            converter.DetailedProgressUpdated += (progress) =>
            {
                Dispatcher.UIThread.Post(() => {
                    fileItem.Progress = progress.OverallProgress;
                });
            };

            var success = false;
            switch (SelectedFormat.ToLowerInvariant())
            {
                case "gif":
                    success = await converter.ToGif(null, cancellationToken);
                    break;
                case "png":
                case "webp":
                    success = await converter.ToWebp(null, cancellationToken);
                    break;
                case "mp4":
                    success = await converter.ToMp4(null, cancellationToken);
                    break;
                case "apng":
                    success = await converter.ToApng(null, cancellationToken);
                    break;
                case "webm":
                    success = await converter.ToWebm(null, cancellationToken);
                    break;
                case "avif":
                    success = await converter.ToAvif(null, cancellationToken);
                    break;
                case "mkv":
                    success = await converter.ToMkv(null, cancellationToken);
                    break;
            }

            fileItem.Status = success ? ConversionStatus.Success : ConversionStatus.Failed;
            fileItem.Progress = success ? 100 : 0;
        }
        catch (OperationCanceledException)
        {
            fileItem.Status = ConversionStatus.Failed;
            fileItem.Progress = 0;
        }
        catch (Exception)
        {
            fileItem.Status = ConversionStatus.Failed;
            fileItem.Progress = 0;
        }
    }

    private void OnStopConversion()
    {
        _cancellationTokenSource?.Cancel();
        StatusText = Resources.StoppingConversion;
    }

    private void OnNext()
    {
        if (SelectedFileItem == null) return;
        
        var currentIndex = FileItems.IndexOf(SelectedFileItem);
        if (currentIndex < FileItems.Count - 1)
            SelectedFileItem = FileItems[currentIndex + 1];
    }

    private void OnPrevious()
    {
        if (SelectedFileItem == null) return;
        
        var currentIndex = FileItems.IndexOf(SelectedFileItem);
        if (currentIndex > 0)
            SelectedFileItem = FileItems[currentIndex - 1];
    }

    [Obsolete("Obsolete")]
    public FactoryViewModel() : base(Resources.Factory, Material.Icons.MaterialIconKind.HammerScrewdriver, 1)
    {
        BrowseSourceFolderCommand = ReactiveCommand.CreateFromTask(BrowseSourceFolderAsync);
        BrowseOutputFolderCommand = ReactiveCommand.CreateFromTask(BrowseOutputFolderAsync);
        StartConversionCommand = ReactiveCommand.CreateFromTask(StartConversionAsync);
        StopConversionCommand = ReactiveCommand.Create(OnStopConversion);
        NextCommand = ReactiveCommand.Create(OnNext);
        PreviousCommand = ReactiveCommand.Create(OnPrevious);
        LottieViewPauseResumeCommand = ReactiveCommand.Create(() => { IsLottieViewPaused = !IsLottieViewPaused; });
        ExportCurrentFrameCommand = ReactiveCommand.Create(ExportCurrentFrame, 
            this.WhenAnyValue(vm => vm.SelectedFilePath, path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .CombineLatest(this.WhenAnyValue(vm => vm.LottieViewCurrentFrame), (hasFile, frame) => hasFile && frame >= 0));
    }
    
    private void GenerateOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
            return;
        var folderName = Path.GetFileName(SelectedFolder);
        OutputFolder = Path.Combine(SelectedFolder, $"{folderName}_output");
    }
    
    private void ExportCurrentFrame()
    {
        try
        {
            PngExporter.ExportSingleFrame(
                SelectedFilePath,
                Path.Combine(OutputFolder!, $"{Path.GetFileNameWithoutExtension(SelectedFilePath)}_frame{LottieViewCurrentFrame}.png"),
                LottieViewCurrentFrame,
                Fps,
                Speed,
                OutputWidth,
                OutputHeight
            );
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Export)
                .WithContent(Resources.ExportSucceeded)
                .OfType(NotificationType.Success)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .WithActionButton(Resources.OpenOutputFolder, _ => OpenOutputFolder(), true).Queue();
        } catch (Exception ex)
        {
            Logger.Error($"Failed to export current frame: {ex.Message}");
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Error)
                .WithContent($"{Resources.Failed}: {ex.Message}")
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
        }
    }
}