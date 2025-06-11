using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;
using ReactiveUI;

namespace LottieViewConvert.ViewModels;

public class FactoryViewModel : Page
{
    private string? _selectedFolder;

    public string? SelectedFolder
    {
        get => _selectedFolder;
        set => this.RaiseAndSetIfChanged(ref _selectedFolder, value);
    }

    public ObservableCollection<string?> Files { get; } = [];

    private string? _selectedFile;

    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            if (SelectedFolder != null && value != null) SelectedFilePath = Path.Combine(SelectedFolder, value);
        }
    }

    private string _selectedFilePath = "/Assets/CarBrandsSticker_Cadillac.tgs";

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
    }

    private int _fps = 60;

    public int Fps
    {
        get => _fps;
        set => this.RaiseAndSetIfChanged(ref _fps, value);
    }

    private int _speed = 1;

    public int Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    // Conversion formats
    public ObservableCollection<string> ConversionFormats { get; } =
    [
        "GIF",
        "Png",
        "Webp",
        "Apng"
    ];

    private string _selectedFormat = "GIF";

    public string SelectedFormat
    {
        get => _selectedFormat;
        set => this.RaiseAndSetIfChanged(ref _selectedFormat, value);
    }

    // Quality and output size
    private int _quality = 75;

    public int Quality
    {
        get => _quality;
        set => this.RaiseAndSetIfChanged(ref _quality, value);
    }

    private int _outputWidth = 800;

    public int OutputWidth
    {
        get => _outputWidth;
        set => this.RaiseAndSetIfChanged(ref _outputWidth, value);
    }

    private int _outputHeight = 600;

    public int OutputHeight
    {
        get => _outputHeight;
        set => this.RaiseAndSetIfChanged(ref _outputHeight, value);
    }
    
    private bool _isFileListVisible;
    public bool IsFileListVisible
    {
        get => _isFileListVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileListVisible, value);
    }
    

    // Commands
    public ReactiveCommand<Unit, Unit> BrowseFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> StartConversionCommand { get; }
    public ReactiveCommand<Unit, Unit> StopConversionCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }

    [Obsolete("Obsolete")]
    private async Task BrowseFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return;
        var window = lifetime.MainWindow;
        if (window == null)
            return;
        
        var dlg = new OpenFolderDialog
        {
            Title = "Select Lottie Folder"
        };
        var result = await dlg.ShowAsync(window);
        if (string.IsNullOrWhiteSpace(result) || !Directory.Exists(result))
            return;

        SelectedFolder = result;
        
        Files.Clear();
        var files = Directory.GetFiles(result)
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".tgs", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OrderBy(name => name);
        foreach (var f in files)
            if (f != null)
                Files.Add(f);

        if (Files.Any())
            SelectedFile = Files.First();
        
        IsFileListVisible = Files.Count >= 1;
    }

    private void OnStartConversion()
    {
        // TODO: start conversion logic
    }

    private void OnStopConversion()
    {
        // TODO: stop conversion logic
    }

    private void OnNext()
    {
        var idx = Files.IndexOf(SelectedFile);
        if (idx < Files.Count - 1)
            SelectedFile = Files[idx + 1];
    }

    private void OnPrevious()
    {
        var idx = Files.IndexOf(SelectedFile);
        if (idx > 0)
            SelectedFile = Files[idx - 1];
    }

    [Obsolete("Obsolete")]
    public FactoryViewModel() : base(Resources.Factory, Material.Icons.MaterialIconKind.HammerScrewdriver, 1)
    {
        BrowseFolderCommand = ReactiveCommand.CreateFromTask(BrowseFolderAsync);
        StartConversionCommand = ReactiveCommand.Create(OnStartConversion);
        StopConversionCommand = ReactiveCommand.Create(OnStopConversion);
        NextCommand = ReactiveCommand.Create(OnNext);
        PreviousCommand = ReactiveCommand.Create(OnPrevious);
    }
}