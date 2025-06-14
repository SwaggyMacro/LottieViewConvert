using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LottieViewConvert.ViewModels;

namespace LottieViewConvert.Views;

public partial class FactoryView : UserControl
{
    private bool _lastLottieViewPauseState;
    
    [Obsolete("Obsolete")]
    public FactoryView()
    {
        InitializeComponent();
        SetupDragDrop();
        Slider.AddHandler(PointerPressedEvent, OnSliderPointerPressed, RoutingStrategies.Tunnel);
        Slider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased, RoutingStrategies.Tunnel);
    }
    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            // Restore the pause state of the LottieView
            vm.IsLottieViewPaused = _lastLottieViewPauseState;
        }
        // Reset the last pause state
        _lastLottieViewPauseState = false;
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastLottieViewPauseState = LottieView.IsPaused;
        if (DataContext is HomeViewModel vm)
        {
            vm.IsLottieViewPaused = true; 
        }
    }
    

    [Obsolete("Obsolete")]
    private void SetupDragDrop()
    {
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    [Obsolete("Obsolete")]
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var paths = e.Data.GetFileNames()?.ToArray() ?? [];
            if (paths.Any(Directory.Exists))
                e.DragEffects = DragDropEffects.Copy;
            else
                e.DragEffects = DragDropEffects.None;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    [Obsolete("Obsolete")]
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var paths = e.Data.GetFileNames()?.ToArray() ?? [];
            var folder = paths.FirstOrDefault(Directory.Exists);
            if (folder != null && DataContext is FactoryViewModel vm)
            {
                await vm.HandleFolderDrop(folder);
            }
        }

        Classes.Remove("drag-over");
        e.Handled = true;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        Classes.Add("drag-over");
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        Classes.Remove("drag-over");
        e.Handled = true;
    }
}