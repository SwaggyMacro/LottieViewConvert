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
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SukiUI.Dialogs;

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
        private readonly ObservableAsPropertyHelper<bool> _isLoading;
        private string _proxyAddress = string.Empty;
        private string _telegramBotToken = string.Empty;
        private string _statusMessage = string.Empty;
        private string _selectedLanguage = "auto";
        private string _originalLanguage = "auto"; // 新增：记录原始语言设置
        private bool _isInitialLoad = true;

        public SettingViewModel() : base(Resources.Setting, MaterialIconKind.Cog, 3)
        {
            _configService = new ConfigService();

            // Initialize available languages
            AvailableLanguages = new List<LanguageOption>
            {
                new() { Code = "auto", DisplayName = Resources.AutoDetect },
                new() { Code = "en", DisplayName = "English" },
                new() { Code = "zh", DisplayName = "中文" }
            };

            // Command
            SaveCommand = ReactiveCommand.CreateFromTask(SaveConfigAsync);
            ResetCommand = ReactiveCommand.CreateFromTask(ResetConfigAsync);
            LoadCommand = ReactiveCommand.CreateFromTask(LoadConfigAsync);

            // Set loading state
            _isLoading = SaveCommand.IsExecuting
                .Merge(ResetCommand.IsExecuting)
                .Merge(LoadCommand.IsExecuting)
                .ToProperty(this, x => x.IsLoading);

            // subscribe to save command to update the status message
            SaveCommand.Subscribe(_ => StatusMessage = Resources.SettingSaved);
            SaveCommand.ThrownExceptions.Subscribe(ex =>
            {
                StatusMessage = $"{Resources.SaveFailed}: {ex.Message}";
            });

            // subscribe to reset command to update the status message
            ResetCommand.Subscribe(_ => StatusMessage = Resources.SettingReset);
            ResetCommand.ThrownExceptions.Subscribe(ex =>
            {
                StatusMessage = $"{Resources.ResetFailed}: {ex.Message}";
            });

            // clear status message after 3 seconds
            this.WhenAnyValue(x => x.StatusMessage)
                .Where(msg => !string.IsNullOrEmpty(msg))
                .Delay(TimeSpan.FromSeconds(3))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => StatusMessage = string.Empty);

            // load configuration
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

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IsLoading => _isLoading.Value;

        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadCommand { get; }

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
                    .Dismiss().ByClicking()
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
            _originalLanguage = SelectedLanguage; // 记录原始语言设置
            _isInitialLoad = false;
        }
        
        private async Task SaveConfig()
        {
            var config = new AppConfig
            {
                ProxyAddress = ProxyAddress,
                TelegramBotToken = TelegramBotToken,
                Language = SelectedLanguage
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
                    .Dismiss().ByClicking()
                    .Queue();
                
                if (languageChanged)
                {
                    await ApplyLanguageChangeAsync(SelectedLanguage);
                    _originalLanguage = SelectedLanguage; // 更新原始语言设置
                    
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
                    .Dismiss().ByClicking()
                    .Queue();
            }
        }

        private async Task ResetConfigAsync()
        {
            ProxyAddress = string.Empty;
            TelegramBotToken = string.Empty;
            SelectedLanguage = "auto";
            await SaveConfig();
            _originalLanguage = SelectedLanguage; // 重置时也更新原始语言设置
        }
    }
}