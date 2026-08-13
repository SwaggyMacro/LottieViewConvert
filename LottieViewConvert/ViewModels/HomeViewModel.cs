using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lottie;
using LottieViewConvert.Helper.Convert;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Utils;
using LottieViewConvert.Telegram;
using ReactiveUI;
using SukiUI.Toasts;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace LottieViewConvert.ViewModels
{
    public class HomeViewModel : Page
    {
        private string? _lottieSource;

        public string? LottieSource
        {
            get => _lottieSource;
            set
            {
                this.RaiseAndSetIfChanged(ref _lottieSource, value);
                _ = RefreshTelegramValidationAsync();
            }
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
            set => this.RaiseAndSetIfChanged(ref _lottieViewTotalFrames, value);
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
            "webm",
            "tgs"
        ];

        public System.Collections.Generic.IReadOnlyList<string> AvailableTelegramPresets { get; } =
            TelegramPresetService.PresetNames;
        public System.Collections.Generic.IReadOnlyList<string> AvailableDurationHandling { get; } =
            TelegramPresetService.DurationHandlingNames;

        private string _selectedTelegramPreset = TelegramPresetNames.Default;
        public string SelectedTelegramPreset
        {
            get => _selectedTelegramPreset;
            set
            {
                if (_selectedTelegramPreset == value) return;
                _selectedTelegramPreset = value;
                this.RaisePropertyChanged();
                ApplyTelegramPreset();
            }
        }

        private string _selectedDurationHandling = "Trim to 3 s";
        public string SelectedDurationHandling
        {
            get => _selectedDurationHandling;
            set
            {
                if (_selectedDurationHandling == value) return;
                _selectedDurationHandling = value;
                this.RaisePropertyChanged();
                ApplyTelegramPreset();
            }
        }

        private string _telegramValidationText = "Select a Telegram preset to validate and export a Telegram-ready asset.";
        public string TelegramValidationText
        {
            get => _telegramValidationText;
            set => this.RaiseAndSetIfChanged(ref _telegramValidationText, value);
        }

        private bool _isTelegramPresetActive;
        public bool IsTelegramPresetActive
        {
            get => _isTelegramPresetActive;
            set => this.RaiseAndSetIfChanged(ref _isTelegramPresetActive, value);
        }

        private string? _lastOutputPath;
        public string? LastOutputPath
        {
            get => _lastOutputPath;
            set => this.RaiseAndSetIfChanged(ref _lastOutputPath, value);
        }

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
                if (LockAspect && !_isAspectAdjusting && _intrinsicWidth > 0)
                {
                    try { _isAspectAdjusting = true;
                        Height = (int)Math.Round(_width * (_intrinsicHeight / _intrinsicWidth));
                    } finally { _isAspectAdjusting = false; }
                }
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
                if (LockAspect && !_isAspectAdjusting && _intrinsicHeight > 0)
                {
                    try { _isAspectAdjusting = true;
                        Width = (int)Math.Round(_height * (_intrinsicWidth / _intrinsicHeight));
                    } finally { _isAspectAdjusting = false; }
                }
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
        
        private bool _useProportionalScaling;
        public bool UseProportionalScaling
        {
            get => _useProportionalScaling;
            set => this.RaiseAndSetIfChanged(ref _useProportionalScaling, value);
        }

        private double _scale;
        public double Scale
        {
            get => _scale;
            set => this.RaiseAndSetIfChanged(ref _scale, value);
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
        public ReactiveCommand<Unit, Unit> OpenReadmeCommand { get; }
        public ReactiveCommand<Unit, Unit> LottieViewPauseResumeCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportCurrentFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> RevealOutputCommand { get; }
        public ReactiveCommand<string, Unit> SelectTelegramPresetCommand { get; }
        public ReactiveCommand<string, Unit> SelectDurationHandlingCommand { get; }

        private readonly TelegramExportService _telegramExportService = new();
        
        private double _intrinsicWidth;
        private double _intrinsicHeight;
        private bool _isAspectAdjusting;
        
        private bool _lockAspect;
        public bool LockAspect
        {
            get => _lockAspect;
            set => this.RaiseAndSetIfChanged(ref _lockAspect, value);
        }

        private double _rotationAngle = 0.0;
        public double RotationAngle
        {
            get => _rotationAngle;
            set => this.RaiseAndSetIfChanged(ref _rotationAngle, value);
        }

        private bool _flipHorizontal;
        public bool FlipHorizontal
        {
            get => _flipHorizontal;
            set => this.RaiseAndSetIfChanged(ref _flipHorizontal, value);
        }

        private bool _flipVertical;
        public bool FlipVertical
        {
            get => _flipVertical;
            set => this.RaiseAndSetIfChanged(ref _flipVertical, value);
        }

        public HomeViewModel()
            : base(Resources.Home, Material.Icons.MaterialIconKind.Home)
        {
            var canConvert = this.WhenAnyValue(
                vm => vm.LottieSource,
                (src) => !string.IsNullOrWhiteSpace(src) && File.Exists(src)
            );

            ConvertCommand = ReactiveCommand.CreateFromTask(DoConvertAsync, canConvert);
            OpenReadmeCommand = ReactiveCommand.Create(() =>
            {
                try
                {
                    UrlUtil.OpenUrl("https://github.com/SwaggyMacro/LottieViewConvert/blob/master/readme.md#-getting-started");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to open README: {ex.Message}");
                    Global.GetToastManager().CreateToast()
                        .WithTitle(Resources.Error)
                        .WithContent($"{Resources.Failed}: {ex.Message}")
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
                }
            });

            LottieViewPauseResumeCommand = ReactiveCommand.Create(() => { IsLottieViewPaused = !IsLottieViewPaused; });
            ExportCurrentFrameCommand = ReactiveCommand.Create(ExportCurrentFrame);
            RevealOutputCommand = ReactiveCommand.Create(RevealLastOutput);
            SelectTelegramPresetCommand = ReactiveCommand.Create<string>(preset => SelectedTelegramPreset = preset);
            SelectDurationHandlingCommand = ReactiveCommand.Create<string>(value => SelectedDurationHandling = value);
        }

        private void ApplyTelegramPreset()
        {
            var preset = TelegramPresetNames.Parse(SelectedTelegramPreset);
            IsTelegramPresetActive = preset != TelegramPreset.Default;
            if (preset == TelegramPreset.Default)
            {
                TelegramValidationText = "Default mode: Telegram-specific constraints are not applied.";
                return;
            }

            if (preset is TelegramPreset.AnimatedSticker or TelegramPreset.AnimatedEmoji)
            {
                SelectedFormat = "tgs";
                Width = Height = 512;
                Fps = 60;
            }
            else
            {
                SelectedFormat = "webm";
            }
            _ = RefreshTelegramValidationAsync();
        }

        private async Task RefreshTelegramValidationAsync()
        {
            var source = LottieSource;
            var preset = TelegramPresetNames.Parse(SelectedTelegramPreset);
            if (preset == TelegramPreset.Default || string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return;
            try
            {
                await Task.Run(() =>
                {
                    if (preset is TelegramPreset.AnimatedSticker or TelegramPreset.AnimatedEmoji)
                    {
                        var validation = _telegramExportService.ValidateTgsSource(source, preset);
                        TelegramValidationText = validation.ToDisplayText($"Source · {preset.DisplayName()}");
                    }
                    else
                    {
                        var handling = TelegramPresetService.ParseDurationHandling(SelectedDurationHandling);
                        var description = _telegramExportService.DescribeVideoSource(source, preset, handling);
                        var facts = new TelegramMediaValidator().InspectLottieSource(source);
                        var plan = new TelegramPresetService().CreateVideoPlan(facts, preset, handling);
                        Width = plan.Width;
                        Height = plan.Height;
                        Fps = plan.Fps;
                        TelegramValidationText = description + Environment.NewLine + Environment.NewLine +
                            "Output status: convert to run ffprobe and real alpha validation.";
                    }
                });
            }
            catch (Exception ex)
            {
                TelegramValidationText = $"INVALID{Environment.NewLine}{ex.Message}";
            }
        }
        
        private void ExportCurrentFrame()
        {
            try
            {
                // Compute output dimensions using intrinsic size when scaling
                int effWidth, effHeight;
                if (UseProportionalScaling && Scale > 0)
                {
                    using var skStream = new SKManagedStream(LottieUtil.OpenLottieStream(LottieSource!));
                    if (!Animation.TryCreate(skStream, out var anim))
                        throw new InvalidOperationException($"Failed to load Lottie animation for scaling: {LottieSource}");
                    effWidth = (int)Math.Round(anim.Size.Width * Scale);
                    effHeight = (int)Math.Round(anim.Size.Height * Scale);
                    anim.Dispose();
                }
                else
                {
                    effWidth = Width;
                    effHeight = Height;
                }
                PngExporter.ExportSingleFrame(
                    LottieSource!,
                    Path.Combine(OutputFolder!, $"{Path.GetFileNameWithoutExtension(LottieSource)}_frame{LottieViewCurrentFrame}.png"),
                    LottieViewCurrentFrame,
                    Fps,
                    PlaySpeed,
                    effWidth,
                    effHeight
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

        private async Task DoConvertAsync()
        {
            if (string.IsNullOrWhiteSpace(LottieSource) || !File.Exists(LottieSource))
            {
                StatusText = Resources.InvalidLottieFile;
                return;
            }

            Directory.CreateDirectory(OutputFolder!);
            var telegramPreset = TelegramPresetNames.Parse(SelectedTelegramPreset);
            if (telegramPreset != TelegramPreset.Default)
            {
                ProgressValue = 1;
                try
                {
                    var durationHandling = TelegramPresetService.ParseDurationHandling(SelectedDurationHandling);
                    var progress = new Progress<double>(value => ProgressValue = value);
                    var result = await Task.Run(() => _telegramExportService.ExportAsync(
                        LottieSource!, OutputFolder!, telegramPreset, durationHandling, progress));
                    LastOutputPath = result.OutputPath;
                    TelegramValidationText = result.SourcePlan + Environment.NewLine + Environment.NewLine +
                        result.Validation.ToDisplayText($"Output · {telegramPreset.DisplayName()}");
                    StatusText = result.IsValid
                        ? $"Telegram-ready: {result.OutputPath}"
                        : $"INVALID: {string.Join("; ", result.Validation.Errors.Select(issue => issue.Message))}";
                    ProgressValue = 100;
                    Dispatcher.UIThread.Post(() =>
                    {
                        Global.GetToastManager().CreateToast()
                            .WithTitle(result.IsValid ? "Telegram-ready" : "Telegram output is INVALID")
                            .WithContent(StatusText)
                            .OfType(result.IsValid ? NotificationType.Success : NotificationType.Error)
                            .WithActionButton("Reveal in Finder", _ => RevealLastOutput(), true)
                            .Dismiss().ByClicking()
                            .Dismiss().After(TimeSpan.FromSeconds(8)).Queue();
                    });
                }
                catch (Exception ex)
                {
                    ProgressValue = 0;
                    StatusText = $"Telegram export failed: {ex.Message}";
                    TelegramValidationText = $"INVALID{Environment.NewLine}{ex.Message}";
                }
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    StatusText = "Starting conversion...";

                    // Calculate effective dimensions
                    var widthForThis = Width;
                    var heightForThis = Height;
                    if (UseProportionalScaling)
                    {
                        try
                        {
                            using var skStream = new SKManagedStream(LottieUtil.OpenLottieStream(LottieSource));
                            if (Animation.TryCreate(skStream, out var anim))
                            {
                                widthForThis = (int)Math.Round(anim.Size.Width * Scale);
                                heightForThis = (int)Math.Round(anim.Size.Height * Scale);
                                anim.Dispose();
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    else if (LockAspect)
                    {
                        try
                        {
                            using var skStream = new SKManagedStream(LottieUtil.OpenLottieStream(LottieSource));
                            if (Animation.TryCreate(skStream, out var anim))
                            {
                                heightForThis = (int)Math.Round(widthForThis * (anim.Size.Height / anim.Size.Width));
                                anim.Dispose();
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var converter = new Converter(
                        LottieSource!,
                        OutputFolder!,
                        new ConversionOptions
                        {
                            PlaySpeed = PlaySpeed,
                            Width = widthForThis,
                            Height = heightForThis,
                            Fps = Fps,
                            Quality = Quality,
                            RotationAngle = RotationAngle,
                            FlipHorizontal = FlipHorizontal,
                            FlipVertical = FlipVertical
                        }
                    );

                    var cts = new CancellationTokenSource();
                    converter.DetailedProgressUpdated += (progress) =>
                    {
                        Dispatcher.UIThread.Post(() => { ProgressValue = progress.OverallProgress; });
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

                    StatusText = success
                        ? $"Conversion succeeded. Output in {OutputFolder}"
                        : "Conversion failed or cancelled.";
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
                if (uri is { IsAbsoluteUri: true, IsFile: true })
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

        private void RevealLastOutput()
        {
            if (string.IsNullOrWhiteSpace(LastOutputPath) || !File.Exists(LastOutputPath)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { "-R", LastOutputPath },
                UseShellExecute = false
            });
        }

        public async Task HandleFileDrop(System.Collections.Generic.IEnumerable<IStorageItem> files)
        {
            var file = files.FirstOrDefault();
            if (file is IStorageFile storageFile)
            {
                var extension = Path.GetExtension(storageFile.Name).ToLower();
                if (extension is ".json" or ".tgs" or ".lottie")
                {
                    try
                    {
                        var uri = storageFile.Path;
                        var stream = uri is { IsAbsoluteUri: true, IsFile: true }
                            ? LottieUtil.OpenLottieStream(uri.LocalPath)
                            : LottieUtil.OpenLottieStream(storageFile.Name);
                        string content = await new StreamReader(stream).ReadToEndAsync();
                        
                        if (LottieUtil.IsValidLottieJson(content))
                        {
                            LottieSource = uri.LocalPath;
                            StatusText = Resources.FileLoadedSuccessfully;
                            GenerateOutputFolder();
                            // Load intrinsic animation size for aspect ratio
                            try
                            {
                                using var skStream = new SKManagedStream(LottieUtil.OpenLottieStream(LottieSource));
                                if (Animation.TryCreate(skStream, out var anim))
                                {
                                    _intrinsicWidth = anim.Size.Width;
                                    _intrinsicHeight = anim.Size.Height;
                                    anim.Dispose();
                                }
                            }
                            catch
                            {
                                // ignored
                            }
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
