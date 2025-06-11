using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Styling;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using ReactiveUI;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Models;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    
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
    public ReactiveCommand<Unit, Unit> OpenDevDebuggingCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ShutdownCommand { get; }
    
    
    public MainWindowViewModel(IEnumerable<Page> pages, PageNavigationService pageNavigationService,
        ISukiToastManager toastManager, ISukiDialogManager dialogManager)
    {
        Pages = new AvaloniaList<Page>(pages.OrderBy(x => x.Index).ThenBy(x => x.DisplayName));

        ToastManager = toastManager;
        DialogManager = dialogManager;

        Global.SetToastManager(toastManager);
        Global.SetDialogManager(dialogManager);

        _theme = SukiTheme.GetInstance();
        Themes = _theme.ColorThemes;
        BaseTheme = _theme.ActiveBaseTheme;

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
    }
    
    public void ChangeTheme(SukiColorTheme theme) => _theme.ChangeColorTheme(theme);
}