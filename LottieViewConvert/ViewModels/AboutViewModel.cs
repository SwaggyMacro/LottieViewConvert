using System;
using System.Diagnostics;
using System.Reactive;
using System.Runtime.InteropServices;
using Avalonia.Controls.Notifications;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using Material.Icons;
using ReactiveUI;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class AboutViewModel : Page
{
    public ReactiveCommand<string, Unit> OpenLinkCommand { get; }
    public AboutViewModel(): base(Resources.About, MaterialIconKind.About, 4)
    {
        OpenLinkCommand = ReactiveCommand.Create<string>(OpenLink);
    }

    private void OpenLink(string url)
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
            Global.GetToastManager().CreateToast()
                .WithTitle(Resources.Failed)
                .WithContent($"Failed to open link: {url}")
                .OfType(NotificationType.Error)
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
    }
}