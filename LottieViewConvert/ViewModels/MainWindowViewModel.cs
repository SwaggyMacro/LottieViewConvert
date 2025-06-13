﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using LottieViewConvert.Services.Dependency;
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
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

        var ffmpegPath = await ffmpegService.DetectFFmpegPathAsync();
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
        var gifskiPath = await gifskiService.DetectGifskiPathAsync();
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
    
    public void ChangeTheme(SukiColorTheme theme) => _theme.ChangeColorTheme(theme);
}