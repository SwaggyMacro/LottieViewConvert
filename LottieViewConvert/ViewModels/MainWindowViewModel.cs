using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using LottieViewConvert.Services.Dependency;
using LottieViewConvert.Utils;
using ReactiveUI;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Models;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private const string ScamWarningRepoUrl = "https://github.com/SwaggyMacro/LottieViewConvert";
    private static readonly string[] ParagraphSeparators =
    [
        $"{Environment.NewLine}{Environment.NewLine}",
        "\n\n",
        "\r\n\r\n"
    ];
    
    public ISukiDialogManager DialogManager { get; }
    public ISukiToastManager ToastManager { get; set; }

    private Page _activePage = null!;
    private IAvaloniaReadOnlyList<Page>? _pages;

    public Page ActivePage
    {
        get => _activePage;
        set => this.RaiseAndSetIfChanged(ref _activePage, value);
    }

    public IAvaloniaReadOnlyList<Page>? Pages
    {
        get => _pages;
        set => this.RaiseAndSetIfChanged(ref _pages, value);
    }

    private readonly SukiTheme _theme;
    public IAvaloniaReadOnlyList<SukiBackgroundStyle> BackgroundStyles { get; } = null!;
    public IAvaloniaReadOnlyList<SukiColorTheme> Themes { get; }
    private ThemeVariant _baseTheme = null!;
    private bool _scamWarningShown;
    private readonly ConfigService _configService;

    public ThemeVariant BaseTheme
    {
        get => _baseTheme;
        set => this.RaiseAndSetIfChanged(ref _baseTheme, value);
    }

    private bool _titleBarVisible = true;

    public bool TitleBarVisible
    {
        get => _titleBarVisible;
        set => this.RaiseAndSetIfChanged(ref _titleBarVisible, value);
    }

    private SukiBackgroundStyle _backgroundStyle = SukiBackgroundStyle.Gradient;

    public SukiBackgroundStyle BackgroundStyle
    {
        get => _backgroundStyle;
        set => this.RaiseAndSetIfChanged(ref _backgroundStyle, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleBaseThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateCustomThemeCommand { get; }
    public ReactiveCommand<string, Unit> OpenUrlCommand { get; }
    
    
    public MainWindowViewModel(IEnumerable<Page> pages, PageNavigationService pageNavigationService,
        ISukiToastManager toastManager, ISukiDialogManager dialogManager, ConfigService configService)
    {
        Pages = new AvaloniaList<Page>(pages.OrderBy(x => x.Index).ThenBy(x => x.DisplayName));

        ToastManager = toastManager;
        DialogManager = dialogManager;
        _configService = configService;

        Global.SetToastManager(toastManager);
        Global.SetDialogManager(dialogManager);

        _theme = SukiTheme.GetInstance();
        Themes = _theme.ColorThemes;
        BaseTheme = _theme.ActiveBaseTheme;

        _ = CheckDependencies();
        
        pageNavigationService.NavigationRequested += pageType =>
        {
            var page = Pages.FirstOrDefault(x => x.GetType() == pageType);
            if (page is null || ActivePage.GetType() == pageType) return;
            ActivePage = page;
        };

        _theme.OnBaseThemeChanged += variant =>
        {
            BaseTheme = variant;
            ToastManager.CreateSimpleInfoToast()
                .WithTitle(Resources.ThemeChangedTitle)
                .WithContent($"{Resources.ThemeChangedContent} {variant}.")
                .Queue();
        };

        _theme.OnColorThemeChanged += theme =>
        {
            ToastManager.CreateSimpleInfoToast()
                .WithTitle(Resources.ColorChangedTitle)
                .WithContent($"{Resources.ColorChangedContent} {theme.DisplayName}.")
                .Queue();
        };

        ToggleBaseThemeCommand = ReactiveCommand.Create(() => { _theme.SwitchBaseTheme(); });

        CreateCustomThemeCommand = ReactiveCommand.Create(() =>
        {
            DialogManager.CreateDialog()
                .WithViewModel(dialog => new Controls.CustomTheme.CustomThemeDialogViewModel(_theme, dialog))
                .TryShow();
        });

        OpenUrlCommand = ReactiveCommand.Create<string>(url =>
        {
            try
            {
                UrlUtil.OpenUrl(url);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to open link: {url}, {ex.Message}");
                ToastManager.CreateToast()
                    .WithTitle(Resources.Failed)
                    .WithContent($"Failed to open link: {url}")
                    .OfType(NotificationType.Error)
                    .Dismiss().ByClicking()
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        });
    }
    
    private async Task CheckDependencies()
    {
        // Check if FFmpeg is installed
        var ffmpegService = new FFmpegService();
        var gifskiService = new GifskiService();

        var ffmpegPath = await ffmpegService.GetFFmpegFromSystemPathAsync();
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            ToastManager.CreateToast()
                .WithTitle("FFmpeg")
                .WithContent(Resources.FFmpegNotFoundContent)
                .OfType(NotificationType.Warning)
                .WithActionButton(Resources.GotIt, _ => { }, dismissOnClick: true)
                .Dismiss().ByClicking()
                .Queue();
        }
        else
        {
            Logger.Info("FFmpeg is installed and detected.");
        }
        var gifskiPath = await gifskiService.GetGifskiFromSystemPathAsync();
        if (string.IsNullOrEmpty(gifskiPath))
        {
            ToastManager.CreateToast()
                .WithTitle("Gifski")
                .WithContent(Resources.GifskiNotFoundContent)
                .OfType(NotificationType.Warning)
                .WithActionButton(Resources.GotIt, _ => {}, dismissOnClick: true)
                .Dismiss().ByClicking()
                .Queue();
        }
        else
        {
            Logger.Info("Gifski is installed and detected.");
        }
    }
    
    public async Task ShowScamWarningIfNeededAsync()
    {
        if (_scamWarningShown)
        {
            return;
        }

        _scamWarningShown = true;

        try
        {
            var config = await _configService.LoadConfigAsync();
            if (!config.ShowScamWarningDialog)
            {
                return;
            }

            DialogManager.CreateDialog()
                .WithTitle(Resources.ScamWarningTitle)
                .WithContent(BuildScamWarningContent())
                .OfType(NotificationType.Information)
                .WithActionButton(Resources.GotIt, _ => { }, dismissOnClick: true)
                .WithActionButton(Resources.DontShowAgain, async _ => await DisableScamWarningAsync(), dismissOnClick: true)
                .TryShow();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to show scam warning dialog: {ex.Message}");
        }
    }
    
    private async Task DisableScamWarningAsync()
    {
        try
        {
            var currentConfig = await _configService.LoadConfigAsync();
            currentConfig.ShowScamWarningDialog = false;
            await _configService.SaveConfigAsync(currentConfig);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to update scam warning preference: {ex.Message}");
        }
    }
    
    private Control BuildScamWarningContent()
    {
        var contentPanel = new StackPanel { Spacing = 8 };
        var paragraphs = Resources.ScamWarningContent.Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries);
        var header = paragraphs.Length > 0 ? paragraphs[0] : Resources.ScamWarningContent;
        var headerPrefix = header;
        var headerSuffix = string.Empty;

        var urlIndex = header.IndexOf(ScamWarningRepoUrl, StringComparison.Ordinal);
        if (urlIndex >= 0)
        {
            headerPrefix = header[..urlIndex];
            headerSuffix = header[(urlIndex + ScamWarningRepoUrl.Length)..];
        }

        if (!string.IsNullOrWhiteSpace(headerPrefix))
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = headerPrefix.TrimEnd(),
                TextWrapping = TextWrapping.Wrap
            });
        }

        contentPanel.Children.Add(CreateRepoLinkTextBlock());

        if (!string.IsNullOrWhiteSpace(headerSuffix))
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = headerSuffix.TrimStart(),
                TextWrapping = TextWrapping.Wrap
            });
        }

        for (var index = 1; index < paragraphs.Length; index++)
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = paragraphs[index],
                TextWrapping = TextWrapping.Wrap
            });
        }

        return contentPanel;
    }

    private TextBlock CreateRepoLinkTextBlock()
    {
        var linkTextBlock = new TextBlock
        {
            Text = ScamWarningRepoUrl,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DeepSkyBlue,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        linkTextBlock.PointerPressed += (_, _) =>
        {
            using var subscription = OpenUrlCommand.Execute(ScamWarningRepoUrl).Subscribe(
                _ => { },
                ex => Logger.Error($"Failed to open scam warning link: {ex.Message}"));
        };
        return linkTextBlock;
    }
    
    public void ChangeTheme(SukiColorTheme theme) => _theme.ChangeColorTheme(theme);
}
