using System.Reactive;
using Avalonia.Media;
using ReactiveUI;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Models;

namespace LottieViewConvert.Controls.CustomTheme;

public class CustomThemeDialogViewModel : ReactiveObject
{
    private string _displayName = "Pink";
    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    private Color _primaryColor = Colors.DeepPink;

    public Color PrimaryColor
    {
        get => _primaryColor;
        set => this.RaiseAndSetIfChanged(ref _primaryColor, value);
    }
    
    private Color _accentColor = Colors.Pink;
    private readonly ISukiDialog _dialog;
    private readonly SukiTheme _theme;

    public Color AccentColor
    {
        get => _accentColor;
        set => this.RaiseAndSetIfChanged(ref _accentColor, value);
    }

    public ReactiveCommand<Unit,Unit> TryCreateThemeCommand { get; }
    public ReactiveCommand<Unit,Unit> CancelCommand { get; }

    public  CustomThemeDialogViewModel(SukiTheme theme, ISukiDialog dialog)
    {
        this._theme = theme;
        this._dialog = dialog;
        TryCreateThemeCommand = ReactiveCommand.Create(TryCreateTheme);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    private void TryCreateTheme()
    {
        if (string.IsNullOrEmpty(DisplayName)) return;
        var theme1 = new SukiColorTheme(DisplayName, PrimaryColor, AccentColor);
        _theme.AddColorTheme(theme1);
        _theme.ChangeColorTheme(theme1);
        _dialog.Dismiss();
    }

    private void Cancel()
    {
        _dialog.Dismiss();
    }
}