using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LottieViewConvert.Helper.Convert;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Utils;
using ReactiveUI;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels
{
    public class HomeViewModel : Page
    {
        private string? _lottieSource;
        public string? LottieSource
        {
            get => _lottieSource;
            set => this.RaiseAndSetIfChanged(ref _lottieSource, value);
        }

        private string _statusText = Resources.DragHereToConvert;
        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }
        
        public ObservableCollection<string> AvailableFormats { get; } =
        [
            "gif",
            "webp",
            "apng",
            "mp4",
            "mkv",
            "avif",
            "webm"
        ];

        private string _selectedFormat = "gif";
        public string SelectedFormat
        {
            get => _selectedFormat;
            set => this.RaiseAndSetIfChanged(ref _selectedFormat, value);
        }

        private int _quality = 100;
        public int Quality
        {
            get => _quality;
            set
            {
                if (value < 1) value = 1;
                if (value > 100) value = 100;
                this.RaiseAndSetIfChanged(ref _quality, value);
            }
        }

        private int _width = 512;
        public int Width
        {
            get => _width;
            set
            {
                if (value < 1) value = 1;
                this.RaiseAndSetIfChanged(ref _width, value);
            }
        }

        private int _height = 512;
        public int Height
        {
            get => _height;
            set
            {
                if (value < 1) value = 1;
                this.RaiseAndSetIfChanged(ref _height, value);
            }
        }

        private int _fps = 90;
        public int Fps
        {
            get => _fps;
            set
            {
                if (value < 1) value = 1;
                this.RaiseAndSetIfChanged(ref _fps, value);
            }
        }

        private double _playSpeed = 1.0;
        public double PlaySpeed
        {
            get => _playSpeed;
            set
            {
                if (value <= 0) value = 1.0;
                this.RaiseAndSetIfChanged(ref _playSpeed, value);
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => this.RaiseAndSetIfChanged(ref _progressValue, value);
        }

        private string? _outputFolder;
        public string? OutputFolder
        {
            get => _outputFolder;
            set => this.RaiseAndSetIfChanged(ref _outputFolder, value);
        }
        
        public ReactiveCommand<Unit, Unit> ConvertCommand { get; }

        public HomeViewModel()
            : base(Resources.Home, Material.Icons.MaterialIconKind.Home)
        {
            var canConvert = this.WhenAnyValue(
                vm => vm.LottieSource,
                (src) => !string.IsNullOrWhiteSpace(src) && File.Exists(src)
            );

            ConvertCommand = ReactiveCommand.CreateFromTask(DoConvertAsync, canConvert);
        }

        private async Task DoConvertAsync()
        {
            if (string.IsNullOrWhiteSpace(LottieSource) || !File.Exists(LottieSource))
            {
                StatusText = Resources.InvalidLottieFile;
                return;
            }
            Directory.CreateDirectory(OutputFolder!);
            await Task.Run(async () => {
                try
                {
                    StatusText = "Starting conversion...";
                    
                    var converter = new Converter(
                        LottieSource,
                        OutputFolder,
                        new ConversionOptions
                        {
                            PlaySpeed = PlaySpeed,
                            Width = Width,
                            Height = Height,
                            Fps = Fps,
                            Quality = Quality
                        }
                    );

                    var cts = new CancellationTokenSource();
                    converter.DetailedProgressUpdated += (progress) =>
                    {
                        Dispatcher.UIThread.Post(() => {
                            ProgressValue = progress.OverallProgress;
                        });
                    };
                    
                    var success = false;
                    switch (SelectedFormat.ToLowerInvariant())
                    {
                        case "gif":
                            success = await converter.ToGif(null, cts.Token);
                            break;
                        case "webp":
                            success = await converter.ToWebp(null, cts.Token);
                            break;
                        case "mp4":
                            success = await converter.ToMp4(null, cts.Token);
                            break;
                        case "apng":
                            success = await converter.ToApng(null, cts.Token);
                            break;
                        case "webm":
                            success = await converter.ToWebm(null, cts.Token);
                            break;
                        case "avif":
                            success = await converter.ToAvif(null, cts.Token);
                            break;
                        case "mkv":
                            success = await converter.ToMkv(null, cts.Token);
                            break;
                    }

                    if (success)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Global.GetToastManager().CreateToast()
                                .WithTitle(Resources.Convert)
                                .WithContent(Resources.ConvertSucceeded)
                                .OfType(NotificationType.Success)
                                .Dismiss().ByClicking()
                                .Dismiss().After(TimeSpan.FromSeconds(3))
                                .WithActionButton(Resources.OpenOutputFolder, _ => OpenOutputFolder(), true).Queue();
                        });

                    }
                    else
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Global.GetToastManager().CreateToast()
                                .WithTitle(Resources.Convert)
                                .WithContent(Resources.ConvertFailed)
                                .OfType(NotificationType.Error)
                                .Dismiss().ByClicking()
                                .Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
                        });
                    }
                    StatusText = success ? $"Conversion succeeded. Output in {OutputFolder}" : "Conversion failed or cancelled.";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error: {ex.Message}";
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
        
        public async Task HandleFileDrop(System.Collections.Generic.IEnumerable<IStorageItem> files)
        {
            var file = files.FirstOrDefault();
            if (file is IStorageFile storageFile)
            {
                var extension = Path.GetExtension(storageFile.Name).ToLower();
                if (extension is ".json" or ".tgs")
                {
                    try
                    {
                        var uri = storageFile.Path;
                        var stream = uri is { IsAbsoluteUri: true, IsFile: true }
                            ? File.OpenRead(uri.LocalPath)
                            : AssetLoader.Open(uri);
                        string content;

                        if (LottieUtil.IsGzipCompressed(stream))
                        {
                            await using var uncompressedStream = LottieUtil.UncompressGzip(stream);
                            using var uncompressedReader = new StreamReader(uncompressedStream);
                            content = await uncompressedReader.ReadToEndAsync();
                        }
                        else
                        {
                            using var reader = new StreamReader(stream);
                            content = await reader.ReadToEndAsync();
                        }

                        if (LottieUtil.IsValidLottieJson(content))
                        {
                            LottieSource = uri.LocalPath;
                            StatusText = Resources.FileLoadedSuccessfully;
                            GenerateOutputFolder();
                        }
                        else
                        {
                            StatusText = Resources.InvalidLottieFile;
                        }
                    }
                    catch
                    {
                        StatusText = Resources.FileLoadFailed;
                    }
                }
                else
                {
                    StatusText = Resources.PleaseDragDotTgsOrDotJsonFile;
                }
            }
        }

        public void GenerateOutputFolder()
        {
            // output_{filename_without_ext}_{DateTime.Now:yyyyMMddHHmmss}
            if (string.IsNullOrWhiteSpace(LottieSource)) return;

            var dir = Path.GetDirectoryName(LottieSource);
            if (dir == null) return;

            var baseName = Path.GetFileNameWithoutExtension(LottieSource);
            OutputFolder = Path.Combine(dir, $"output_{baseName}_{DateTime.Now:yyyyMMddHHmmss}");
        }
    }
}
