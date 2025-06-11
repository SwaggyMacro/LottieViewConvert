using System;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LottieViewConvert;

public class Global
{
    private static readonly Lazy<Global> LazyInstance = new(() => new Global());
    private static readonly object Lock = new();

    private ISukiDialogManager? _dialogManager;
    private ISukiToastManager? _toastManager;
    private MainWindow? _mainWindow;
    
    private static Global Instance => LazyInstance.Value;
    
    /// <summary>
    /// Set main window
    /// </summary>
    /// <param name="mainWindow"></param>
    public static void SetMainWindow(MainWindow mainWindow)
    {
        lock (Lock)
        {
            Instance._mainWindow = mainWindow;
        }
    }

    /// <summary>
    /// Get main window
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static MainWindow GetMainWindow()
    {
        if (Instance._mainWindow != null) return Instance._mainWindow;

        Logger.Fatal("Main window is not set");
        throw new Exception("Main window is not set");
    }
    
    /// <summary>
    /// Set toast manager
    /// </summary>
    /// <param name="toastManager"></param>
    public static void SetToastManager(ISukiToastManager toastManager)
    {
        lock (Lock)
        {
            Instance._toastManager = toastManager;
        }
    }

    /// <summary>
    /// Set dialog manager
    /// </summary>
    /// <param name="dialogManager"></param>
    public static void SetDialogManager(ISukiDialogManager dialogManager)
    {
        lock (Lock)
        {
            Instance._dialogManager = dialogManager;
        }
    }

    /// <summary>
    /// Get toast manager
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ISukiToastManager GetToastManager()
    {
        if (Instance._toastManager != null) return Instance._toastManager;

        Logger.Fatal("Toast manager is not set");
        throw new Exception("Toast manager is not set");
    }

    /// <summary>
    /// Get dialog manager
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ISukiDialogManager GetDialogManager()
    {
        if (Instance._dialogManager != null) return Instance._dialogManager;

        Logger.Fatal("Dialog manager is not set");
        throw new Exception("Dialog manager is not set");
    }
}