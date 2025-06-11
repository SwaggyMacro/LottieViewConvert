using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using LottieViewConvert.Helper;
using LottieViewConvert.ViewModels;

namespace LottieViewConvert.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        SetupDragDrop();
    }

    private void SetupDragDrop()
    {
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        DropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        DropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private async void SourceBrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = this.GetVisualRoot() as Window;
        if (topLevel == null) return;

        var path = await FilePickerHelper.SelectFileAsync(topLevel, Lang.Resources.SelectLottieFile, new FilePickerFileType(Lang.Resources.LottieJsonTgs)
        {
            Patterns = new[] {"*.json", "*.tgs"}
        }, allowMultiple: false);
        if (string.IsNullOrEmpty(path)) return;

        if (DataContext is HomeViewModel vm)
        {
            vm.LottieSource = path;
            vm.GenerateOutputFolder();
        }
    }
    
    private async void OutputBrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = this.GetVisualRoot() as Window;
        if (topLevel == null) return;

        var path = await FilePickerHelper.SelectFolderAsync(topLevel, Lang.Resources.SelectOutputFolder);
        if (string.IsNullOrEmpty(path)) return;

        if (DataContext is HomeViewModel vm)
        {
            vm.OutputFolder = path; 
        }
    }
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && DataContext is HomeViewModel viewModel)
            {
                await viewModel.HandleFileDrop(files);
            }
        }
        
        DropZone.Classes.Remove("drag-over");
        e.Handled = true;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Add("drag-over");
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Remove("drag-over");
        e.Handled = true;
    }

}