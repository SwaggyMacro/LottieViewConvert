using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LottieViewConvert.ViewModels;
using SukiUI.Controls;
using SukiUI.Models;

namespace LottieViewConvert.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Global.SetMainWindow(this);
    }
    
    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        IsMenuVisible = !IsMenuVisible;
    }
    
    private void ThemeMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Source is not MenuItem mItem) return;
        if (mItem.DataContext is not SukiColorTheme cTheme) return;
        vm.ChangeTheme(cTheme);
    }
    
    private void MakeFullScreenPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        IsTitleBarVisible = WindowState != WindowState.FullScreen;
    }
}