using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using LottieViewConvert.Helper.LogHelper;
using ReactiveUI;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using Material.Icons;
using SukiUI.Toasts;
using Avalonia.Platform.Storage;
using SukiUI.Dialogs;
using System.IO;
using LottieViewConvert.Services.Dependency;

namespace LottieViewConvert.ViewModels
{
    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class SettingViewModel : Page
    {
        private readonly ConfigService _configService;
        private readonly FFmpegService _ffmpegService;
        private readonly GifskiService _gifskiService;
        private readonly ObservableAsPropertyHelper<bool> _isLoading;
        private readonly ObservableAsPropertyHelper<bool> _isFFmpegInstalling;
        private readonly ObservableAsPropertyHelper<bool> _isGifskiInstalling;
        private string _proxyAddress = string.Empty;
        private string _telegramBotToken = string.Empty;
        private string _statusMessage = string.Empty;
        private string _selectedLanguage = "auto";
        private string _originalLanguage = "auto";
        private string _ffmpegPath = string.Empty;
        private string _ffmpegStatus = string.Empty;
        private string _ffmpegVersion = string.Empty;
        private double _ffmpegInstallProgress;
        private bool _isInitialLoad = true;
        private string _ffmpegSource = string.Empty;
        private bool _isFFmpegInPath;
        
        // Gifski properties
        private string _gifskiPath = string.Empty;
        private string _gifskiStatus = string.Empty;
        private string _gifskiVersion = string.Empty;
        private double _gifskiInstallProgress;
        private string _gifskiSource = string.Empty;
        private bool _isGifskiInPath;

        public SettingViewModel() : base(Resources.Setting, MaterialIconKind.Cog, 3)
        {
            _configService = new ConfigService();
            _ffmpegService = new FFmpegService();
            _gifskiService = new GifskiService();

            // Initialize available languages
            AvailableLanguages = new List<LanguageOption>
            {
                new() { Code = "auto", DisplayName = Resources.AutoDetect },
                new() { Code = "en", DisplayName = "English" },
                new() { Code = "zh", DisplayName = "中文" }
            };

            // Commands
            SaveCommand = ReactiveCommand.CreateFromTask(SaveConfigAsync);
            ResetCommand = ReactiveCommand.CreateFromTask(ResetConfigAsync);
            LoadCommand = ReactiveCommand.CreateFromTask(LoadConfigAsync);
            BrowseFFmpegCommand = ReactiveCommand.CreateFromTask(BrowseFFmpegAsync);
            InstallFFmpegCommand = ReactiveCommand.CreateFromTask(InstallFFmpegAsync);
            CheckFFmpegCommand = ReactiveCommand.CreateFromTask(CheckFFmpegAsync);
            AddToPathCommand = ReactiveCommand.CreateFromTask(AddFFmpegToPathAsync);
            RemoveFromPathCommand = ReactiveCommand.CreateFromTask(RemoveFFmpegFromPathAsync);
            
            // Gifski commands
            BrowseGifskiCommand = ReactiveCommand.CreateFromTask(BrowseGifskiAsync);
            InstallGifskiCommand = ReactiveCommand.CreateFromTask(InstallGifskiAsync);
            CheckGifskiCommand = ReactiveCommand.CreateFromTask(CheckGifskiAsync);
            AddGifskiToPathCommand = ReactiveCommand.CreateFromTask(AddGifskiToPathAsync);
            RemoveGifskiFromPathCommand = ReactiveCommand.CreateFromTask(RemoveGifskiFromPathAsync);

            // Set loading state
            _isLoading = SaveCommand.IsExecuting
                .Merge(ResetCommand.IsExecuting)
                .Merge(LoadCommand.IsExecuting)
                .ToProperty(this, x => x.IsLoading);

            _isFFmpegInstalling = InstallFFmpegCommand.IsExecuting
                .ToProperty(this, x => x.IsFFmpegInstalling);
                
            _isGifskiInstalling = InstallGifskiCommand.IsExecuting
                .ToProperty(this, x => x.IsGifskiInstalling);

            // Subscribe to command results
            SaveCommand.Subscribe(_ => StatusMessage = Resources.SettingSaved);
            SaveCommand.ThrownExceptions.Subscribe(ex => { StatusMessage = $"{Resources.SaveFailed}: {ex.Message}"; });

            ResetCommand.Subscribe(_ => StatusMessage = Resources.SettingReset);
            ResetCommand.ThrownExceptions.Subscribe(ex =>
            {
                StatusMessage = $"{Resources.ResetFailed}: {ex.Message}";
            });

            // Clear status message after 3 seconds
            this.WhenAnyValue(x => x.StatusMessage)
                .Where(msg => !string.IsNullOrEmpty(msg))
                .Delay(TimeSpan.FromSeconds(3))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => StatusMessage = string.Empty);

            // Auto-check FFmpeg when path changes
            this.WhenAnyValue(x => x.FFmpegPath)
                .Where(_ => !_isInitialLoad)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => CheckFFmpegCommand.Execute().Subscribe());
                
            // Auto-check Gifski when path changes
            this.WhenAnyValue(x => x.GifskiPath)
                .Where(_ => !_isInitialLoad)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => CheckGifskiCommand.Execute().Subscribe());

            // Load configuration
            LoadCommand.Execute().Subscribe();
        }

        public List<LanguageOption> AvailableLanguages { get; }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
        }

        public string ProxyAddress
        {
            get => _proxyAddress;
            set => this.RaiseAndSetIfChanged(ref _proxyAddress, value);
        }

        public string TelegramBotToken
        {
            get => _telegramBotToken;
            set => this.RaiseAndSetIfChanged(ref _telegramBotToken, value);
        }

        public string FFmpegPath
        {
            get => _ffmpegPath;
            set => this.RaiseAndSetIfChanged(ref _ffmpegPath, value);
        }

        public string FFmpegStatus
        {
            get => _ffmpegStatus;
            set => this.RaiseAndSetIfChanged(ref _ffmpegStatus, value);
        }

        public string FFmpegVersion
        {
            get => _ffmpegVersion;
            set => this.RaiseAndSetIfChanged(ref _ffmpegVersion, value);
        }

        public double FFmpegInstallProgress
        {
            get => _ffmpegInstallProgress;
            set => this.RaiseAndSetIfChanged(ref _ffmpegInstallProgress, value);
        }

        public string FFmpegSource
        {
            get => _ffmpegSource;
            set => this.RaiseAndSetIfChanged(ref _ffmpegSource, value);
        }

        public bool IsFFmpegInPath
        {
            get => _isFFmpegInPath;
            set => this.RaiseAndSetIfChanged(ref _isFFmpegInPath, value);
        }

        // Gifski properties
        public string GifskiPath
        {
            get => _gifskiPath;
            set => this.RaiseAndSetIfChanged(ref _gifskiPath, value);
        }

        public string GifskiStatus
        {
            get => _gifskiStatus;
            set => this.RaiseAndSetIfChanged(ref _gifskiStatus, value);
        }

        public string GifskiVersion
        {
            get => _gifskiVersion;
            set => this.RaiseAndSetIfChanged(ref _gifskiVersion, value);
        }

        public double GifskiInstallProgress
        {
            get => _gifskiInstallProgress;
            set => this.RaiseAndSetIfChanged(ref _gifskiInstallProgress, value);
        }

        public string GifskiSource
        {
            get => _gifskiSource;
            set => this.RaiseAndSetIfChanged(ref _gifskiSource, value);
        }

        public bool IsGifskiInPath
        {
            get => _isGifskiInPath;
            set => this.RaiseAndSetIfChanged(ref _isGifskiInPath, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IsLoading => _isLoading.Value;
        public bool IsFFmpegInstalling => _isFFmpegInstalling.Value;
        public bool IsGifskiInstalling => _isGifskiInstalling.Value;
        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseFFmpegCommand { get; }
        public ReactiveCommand<Unit, Unit> InstallFFmpegCommand { get; }
        public ReactiveCommand<Unit, Unit> CheckFFmpegCommand { get; }
        public ReactiveCommand<Unit, Unit> AddToPathCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveFromPathCommand { get; }
        
        // Gifski commands
        public ReactiveCommand<Unit, Unit> BrowseGifskiCommand { get; }
        public ReactiveCommand<Unit, Unit> InstallGifskiCommand { get; }
        public ReactiveCommand<Unit, Unit> CheckGifskiCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGifskiToPathCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveGifskiFromPathCommand { get; }

        // Gifski methods
        private async Task BrowseGifskiAsync()
        {
            try
            {
                var topLevel = Global.GetMainWindow();

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Gifski Executable",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Executable Files")
                        {
                            Patterns = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                                System.Runtime.InteropServices.OSPlatform.Windows)
                                ? new[] { "*.exe" }
                                : new[] { "*" }
                        }
                    ]
                });

                if (files.Count > 0)
                {
                    GifskiPath = files[0].Path.LocalPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to browse Gifski: {ex.Message}");
                Global.GetToastManager().CreateToast()
                    .WithTitle("Gifski")
                    .WithContent($"Failed to browse: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        private async Task InstallGifskiAsync()
        {
            try
            {
                // Check if Gifski is already installed
                var existingPath = await _gifskiService.DetectGifskiPathAsync();
                if (!string.IsNullOrEmpty(existingPath))
                {
                    var dialogManager = Global.GetDialogManager();
                    var dialog = dialogManager.CreateDialog()
                        .WithTitle("Gifski")
                        .WithContent(
                            $"{Resources.GifskiIsAlreadyAvailableOnYourSystemAt}:\n{existingPath}\n\n{Resources.DoYouStillWantToInstallALocalCopyAndAddItToPATH}")
                        .OfType(NotificationType.Information)
                        .WithYesNoResult(Resources.YesInstallAndAddToPATH, Resources.NoUseSystemGifski)
                        .TryShowAsync();

                    var result = await dialog;

                    if (result == false)
                    {
                        GifskiPath = existingPath;
                        await CheckGifskiAsync();
                        return;
                    }
                }

                GifskiInstallProgress = 0;
                GifskiStatus = $"{Resources.Installing}...";

                var progress = new Progress<double>(value => GifskiInstallProgress = value);
                var installResult = await _gifskiService.DownloadAndInstallGifskiAsync(progress, addToPath: true);

                if (installResult.Success)
                {
                    // Update GifskiPath to the local installation path when installation is successful
                    var localPath = _gifskiService.GetLocalGifskiPath();
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        GifskiPath = localPath;
                    }

                    await CheckGifskiAsync();
                    
                    var dialog = Global.GetDialogManager().CreateDialog()
                        .WithTitle("Gifski")
                        .WithContent($"{Resources.GifskiInstalledSuccessfully}! {Resources.YouShouldRestartTheApplicationToApplyChanges}. {Resources.DoYouWantToRestartNow}?")
                        .OfType(NotificationType.Success)
                        .WithYesNoResult(Resources.YesRestart, Resources.NoLater)
                        .TryShowAsync();
                    var dialogResult = await dialog;
                    if (dialogResult)
                    {
                        RestartApplication();
                    }
                }
                else
                {
                    GifskiStatus = $"{Resources.InstallationFailed}: {installResult.Message}";

                    Global.GetToastManager().CreateToast()
                        .WithTitle("Gifski")
                        .WithContent(installResult.Message)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to install Gifski: {ex.Message}");
                GifskiStatus = $"{Resources.InstallationFailed}: {ex.Message}";

                Global.GetToastManager().CreateToast()
                    .WithTitle("Gifski")
                    .WithContent($"{Resources.InstallationFailed}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(5))
                    .Queue();
            }
            finally
            {
                GifskiInstallProgress = 0;
            }
        }

        private async Task CheckGifskiAsync()
        {
            try
            {
                string? gifskiPath;

                // If GifskiPath is set, use it directly
                if (!string.IsNullOrEmpty(GifskiPath))
                {
                    gifskiPath = GifskiPath;
                }
                else
                {
                    // Auto detect Gifski
                    gifskiPath = await _gifskiService.DetectGifskiPathAsync();
                    if (!string.IsNullOrEmpty(gifskiPath))
                    {
                        GifskiPath = gifskiPath; 
                    }
                }

                if (string.IsNullOrEmpty(gifskiPath))
                {
                    GifskiStatus = Resources.Notfound;
                    GifskiVersion = "N/A";
                    GifskiSource = "N/A";
                    IsGifskiInPath = false;
                    return;
                }

                var isValid = await _gifskiService.IsGifskiValidAsync(gifskiPath);
                if (isValid)
                {
                    GifskiStatus = Resources.Available;
                    GifskiVersion = await _gifskiService.GetGifskiVersionAsync(gifskiPath);
                    GifskiSource = await _gifskiService.GetGifskiSourceAsync(gifskiPath);
                    
                    // Check if the Gifski in path (only for local installed Gifski)
                    var localPath = _gifskiService.GetLocalGifskiPath();
                    if (!string.IsNullOrEmpty(localPath) && Path.GetFullPath(localPath) == Path.GetFullPath(gifskiPath))
                    {
                        IsGifskiInPath = _gifskiService.IsLocalGifskiInPath();
                    }
                    else
                    {
                        IsGifskiInPath = await _gifskiService.GetGifskiFromSystemPathAsync() != null;
                    }
                }
                else
                {
                    GifskiStatus = Resources.InvalidOrNotWorking;
                    GifskiVersion = "N/A";
                    GifskiSource = "N/A";
                    IsGifskiInPath = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to check Gifski: {ex.Message}");
                GifskiStatus = $"{Resources.CheckFailed}: {ex.Message}";
                GifskiVersion = "N/A";
                GifskiSource = "N/A";
                IsGifskiInPath = false;
            }
        }

        private async Task AddGifskiToPathAsync()
        {
            try
            {
                var result = await _gifskiService.AddToPathAsync();

                if (result.Success)
                {
                    await CheckGifskiAsync(); // Re-check Gifski status after adding to PATH

                    Global.GetToastManager().CreateToast()
                        .WithTitle("Gifski")
                        .WithContent(result.Message)
                        .OfType(NotificationType.Success)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
                else
                {
                    Global.GetToastManager().CreateToast()
                        .WithTitle("Gifski")
                        .WithContent(result.Message)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add Gifski to PATH: {ex.Message}");

                Global.GetToastManager().CreateToast()
                    .WithTitle("Gifski")
                    .WithContent($"{Resources.FailedToAddToPath}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        private async Task RemoveGifskiFromPathAsync()
        {
            try
            {
                var result = await _gifskiService.RemoveFromPathAsync();

                if (result.Success)
                {
                    await CheckGifskiAsync(); // Check Gifski status again

                    Global.GetToastManager().CreateToast()
                        .WithTitle("Gifski")
                        .WithContent(Resources.GifskiDirectoryRemovedFromPathSuccessfully)
                        .OfType(NotificationType.Success)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
                else
                {
                    Global.GetToastManager().CreateToast()
                        .WithTitle("Gifski")
                        .WithContent(Resources.FailedToRemoveFromPath)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove Gifski from PATH: {ex.Message}");

                Global.GetToastManager().CreateToast()
                    .WithTitle("Gifski")
                    .WithContent(Resources.FailedToRemoveFromPath)
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        // Keep all existing FFmpeg methods...
        private async Task BrowseFFmpegAsync()
        {
            try
            {
                var topLevel = Global.GetMainWindow();

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = Resources.SelectFFmpegExecutable,
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(Resources.ExecutableFiles)
                        {
                            Patterns = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                                System.Runtime.InteropServices.OSPlatform.Windows)
                                ? new[] { "*.exe" }
                                : new[] { "*" }
                        }
                    ]
                });

                if (files.Count > 0)
                {
                    FFmpegPath = files[0].Path.LocalPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to browse FFmpeg: {ex.Message}");
                Global.GetToastManager().CreateToast()
                    .WithTitle("FFmpeg")
                    .WithContent($"{Resources.FailedTobrowse}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        private async Task RemoveFFmpegFromPathAsync()
        {
            try
            {
                var result = await _ffmpegService.RemoveFromPathAsync();

                if (result.Success)
                {
                    await CheckFFmpegAsync(); // check FFmpeg status again

                    Global.GetToastManager().CreateToast()
                        .WithTitle("FFmpeg")
                        .WithContent(Resources.FFmpegDirectoryRemoved)
                        .OfType(NotificationType.Success)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
                else
                {
                    Global.GetToastManager().CreateToast()
                        .WithTitle("FFmpeg")
                        .WithContent(Resources.FailedToRemove)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove FFmpeg from PATH: {ex.Message}");

                Global.GetToastManager().CreateToast()
                    .WithTitle("FFmpeg")
                    .WithContent(Resources.FailedToRemove)
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        private async Task InstallFFmpegAsync()
        {
            try
            {
                // check if FFmpeg is already installed
                var existingPath = await _ffmpegService.DetectFFmpegPathAsync();
                if (!string.IsNullOrEmpty(existingPath))
                {
                    var dialogManager = Global.GetDialogManager();
                    var dialog = dialogManager.CreateDialog()
                        .WithTitle("FFmpeg")
                        .WithContent(
                            $"{Resources.FFmpegIsAlreadyAvailableOnYourSystemAt}:\n{existingPath}\n\n{Resources.DoYouStillWantToInstallALocalCopyAndAddItToPATH}")
                        .OfType(NotificationType.Information)
                        .WithYesNoResult(Resources.YesInstallAndAddToPATH, Resources.NoUseSystemFFmpeg)
                        .TryShowAsync();

                    var result = await dialog;

                    if (result == false)
                    {
                        FFmpegPath = existingPath;
                        await CheckFFmpegAsync();
                        return;
                    }
                }

                FFmpegInstallProgress = 0;
                FFmpegStatus = $"{Resources.Installing}...";

                var progress = new Progress<double>(value => FFmpegInstallProgress = value);
                var installResult = await _ffmpegService.DownloadAndInstallFFmpegAsync(progress, addToPath: true);

                if (installResult.Success)
                {
                    // update FFmpegPath to the local installation path when installation is successful
                    var localPath = _ffmpegService.GetLocalFFmpegPath();
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        FFmpegPath = localPath;
                    }

                    await CheckFFmpegAsync();
                    
                    var dialog = Global.GetDialogManager().CreateDialog()
                        .WithTitle("FFmpeg")
                        .WithContent($"{Resources.FFmpegInstalledSuccessfully} {Resources.YouShouldRestartTheApplicationToApplyChanges}, {Resources.DoYouWantToRestartNow}")
                        .OfType(NotificationType.Success)
                        .WithYesNoResult(Resources.YesRestart, Resources.NoLater)
                        .TryShowAsync();
                    var dialogResult = await dialog;
                    if (dialogResult)
                    {
                        RestartApplication();
                    }
                }
                else
                {
                    FFmpegStatus = $"Installation failed: {installResult.Message}";

                    Global.GetToastManager().CreateToast()
                        .WithTitle("FFmpeg")
                        .WithContent(installResult.Message)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to install FFmpeg: {ex.Message}");
                FFmpegStatus = $"{Resources.InstallationFailed}: {ex.Message}";

                Global.GetToastManager().CreateToast()
                    .WithTitle("FFmpeg")
                    .WithContent($"{Resources.InstallationFailed}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(5))
                    .Queue();
            }
            finally
            {
                FFmpegInstallProgress = 0;
            }
        }

        private async Task AddFFmpegToPathAsync()
        {
            try
            {
                var result = await _ffmpegService.AddToPathAsync();

                if (result.Success)
                {
                    await CheckFFmpegAsync(); // Re-check FFmpeg status after adding to PATH

                    Global.GetToastManager().CreateToast()
                        .WithTitle("FFmpeg")
                        .WithContent(result.Message)
                        .OfType(NotificationType.Success)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
                else
                {
                    Global.GetToastManager().CreateToast()
                        .WithTitle("FFmpeg")
                        .WithContent(result.Message)
                        .OfType(NotificationType.Error)
                        .Dismiss().ByClicking()
                        .Dismiss().After(TimeSpan.FromSeconds(5))
                        .Queue();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add FFmpeg to PATH: {ex.Message}");

                Global.GetToastManager().CreateToast()
                    .WithTitle("FFmpeg")
                    .WithContent($"{Resources.FailedToAddToPath}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }

        private async Task CheckFFmpegAsync()
        {
            try
            {
                string? ffmpegPath;

                // if FFmpegPath is set, use it directly
                if (!string.IsNullOrEmpty(FFmpegPath))
                {
                    ffmpegPath = FFmpegPath;
                }
                else
                {
                    // auto detect FFmpeg
                    ffmpegPath = await _ffmpegService.DetectFFmpegPathAsync();
                    if (!string.IsNullOrEmpty(ffmpegPath))
                    {
                        FFmpegPath = ffmpegPath; 
                    }
                }

                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    FFmpegStatus = Resources.Notfound;
                    FFmpegVersion = "N/A";
                    FFmpegSource = "N/A";
                    IsFFmpegInPath = false;
                    return;
                }

                var isValid = await _ffmpegService.IsFFmpegValidAsync(ffmpegPath);
                if (isValid)
                {
                    FFmpegStatus = Resources.Available;
                    FFmpegVersion = await _ffmpegService.GetFFmpegVersionAsync(ffmpegPath);
                    FFmpegSource = await _ffmpegService.GetFFmpegSourceAsync(ffmpegPath);
                    
                    // check if the FFmpeg in path (only for local installed FFmpeg)
                    var localPath = _ffmpegService.GetLocalFFmpegPath();
                    if (!string.IsNullOrEmpty(localPath) && Path.GetFullPath(localPath) == Path.GetFullPath(ffmpegPath))
                    {
                        IsFFmpegInPath = _ffmpegService.IsLocalFFmpegInPath();
                    }
                    else
                    {
                        IsFFmpegInPath = await _ffmpegService.GetFFmpegFromSystemPathAsync() != null;
                    }
                }
                else
                {
                    FFmpegStatus = Resources.InvalidOrNotWorking;
                    FFmpegVersion = "N/A";
                    FFmpegSource = "N/A";
                    IsFFmpegInPath = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to check FFmpeg: {ex.Message}");
                FFmpegStatus = $"{Resources.CheckFailed}: {ex.Message}";
                FFmpegVersion = "N/A";
                FFmpegSource = "N/A";
                IsFFmpegInPath = false;
            }
        }

        private async Task ApplyLanguageChangeAsync(string languageCode)
        {
            try
            {
                CultureInfo culture = languageCode switch
                {
                    "en" => new CultureInfo("en"),
                    "zh" => new CultureInfo("zh-CN"),
                    "auto" => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                        ? new CultureInfo("zh-CN")
                        : new CultureInfo("en"),
                    _ => new CultureInfo("en")
                };

                Resources.Culture = culture;
                StatusMessage = Resources.LanguageChanged;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to change language: {ex.Message}");
            }
        }

        private void RestartApplication()
        {
            try
            {
                var currentExecutable = Environment.ProcessPath ??
                                        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                if (!string.IsNullOrEmpty(currentExecutable))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = currentExecutable,
                        UseShellExecute = true
                    });

                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to restart application: {ex.Message}");

                Global.GetToastManager().CreateToast()
                    .WithTitle(Resources.LanguageSettings)
                    .WithContent(Resources.ManualRestartMessage)
                    .OfType(NotificationType.Warning)
                    .Dismiss().After(TimeSpan.FromSeconds(5))
                    .Queue();
            }
        }

        private async Task LoadConfigAsync()
        {
            _isInitialLoad = true;
            var config = await _configService.LoadConfigAsync();
            ProxyAddress = config.ProxyAddress;
            TelegramBotToken = config.TelegramBotToken;
            SelectedLanguage = config.Language ?? "auto";
            FFmpegPath = config.FFmpegPath;
            GifskiPath = config.GifskiPath; // Load Gifski path if it exists in config
            _originalLanguage = SelectedLanguage;

            // check FFmpeg and Gifski status
            await CheckFFmpegAsync();
            await CheckGifskiAsync();
            _isInitialLoad = false;
        }

        private async Task SaveConfig()
        {
            var config = new AppConfig
            {
                ProxyAddress = ProxyAddress,
                TelegramBotToken = TelegramBotToken,
                Language = SelectedLanguage,
                FFmpegPath = FFmpegPath,
                GifskiPath = GifskiPath // Save Gifski path
            };
            await _configService.SaveConfigAsync(config);
        }

        private async Task SaveConfigAsync()
        {
            try
            {
                await SaveConfig();

                bool languageChanged = _originalLanguage != SelectedLanguage;

                await Task.Delay(1000);

                Global.GetToastManager().CreateToast()
                    .WithTitle(Resources.Setting)
                    .WithContent(Resources.SettingSaved)
                    .OfType(NotificationType.Success)
                    .Dismiss().After(TimeSpan.FromSeconds(2))
                    .Queue();

                if (languageChanged)
                {
                    await ApplyLanguageChangeAsync(SelectedLanguage);
                    _originalLanguage = SelectedLanguage;

                    Global.GetDialogManager().CreateDialog()
                        .WithTitle(Resources.LanguageSettings)
                        .WithContent(Resources.LanguageChangedMessage)
                        .OfType(NotificationType.Information)
                        .WithActionButton(Resources.Yes, _ => { RestartApplication(); })
                        .WithActionButton(Resources.No, _ => { })
                        .TryShow();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Global.GetToastManager().CreateToast()
                    .WithTitle(Resources.Setting)
                    .WithContent($"{Resources.SaveFailed}: {ex.Message}")
                    .OfType(NotificationType.Error)
                    .Dismiss().After(TimeSpan.FromSeconds(2))
                    .Queue();
            }
        }

        private async Task ResetConfigAsync()
        {
            ProxyAddress = string.Empty;
            TelegramBotToken = string.Empty;
            SelectedLanguage = "auto";
            FFmpegPath = string.Empty;
            GifskiPath = string.Empty;
            await SaveConfig();
            _originalLanguage = SelectedLanguage;
            await CheckFFmpegAsync();
            await CheckGifskiAsync();
        }
    }
}