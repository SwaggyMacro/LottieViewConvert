using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LottieViewConvert.Common;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using LottieViewConvert.ViewModels;
using LottieViewConvert.Views;
using Microsoft.Extensions.DependencyInjection;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LottieViewConvert;

public class App : Application
{
    private IServiceProvider? _provider;
    public override void Initialize()
    {
        Logger.Info("Initializing...");
        AvaloniaXamlLoader.Load(this);
        _provider = ConfigureServices();
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            BindingPlugins.DataValidators.RemoveAt(0);
            // var viewLocator = _provider?.GetRequiredService<IDataTemplate>();
            var mainViewModel = _provider?.GetRequiredService<MainWindowViewModel>();
            var mainView = _provider?.GetRequiredService<MainWindow>();
            mainView!.DataContext = mainViewModel;
            desktop.MainWindow = mainView;
        }

        base.OnFrameworkInitializationCompleted();
        Logger.Info("Initialization completed.");
    }
    
    private static ServiceProvider ConfigureServices()
    {
        var viewLocator = Current?.DataTemplates.First(x => x is ViewLocator);
        var services = new ServiceCollection();

        // Views
        services.AddSingleton<MainWindow>();

        // Services
        if (viewLocator is not null)
            services.AddSingleton(viewLocator);
        services.AddSingleton<PageNavigationService>();
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<ISukiDialogManager, SukiDialogManager>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => !p.IsAbstract && typeof(Page).IsAssignableFrom(p));
        foreach (var type in types)
            services.AddSingleton(typeof(Page), type);


        return services.BuildServiceProvider();
    }
}