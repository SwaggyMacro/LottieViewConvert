using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using LottieViewConvert.Utils;

namespace LottieViewConvert.Views;

public partial class SettingView : UserControl
{
    public SettingView()
    {
        InitializeComponent();
    }

    private void OnGifskiHintClicked(object? sender, PointerPressedEventArgs e)
    {
        UrlUtil.OpenUrl("https://gif.ski/");
    }
}