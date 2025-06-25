using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive;
using System.IO;
using System.Net;
using LottieViewConvert.Models;
using LottieViewConvert.Services;
using Material.Icons;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using LottieViewConvert.Helper;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models.Discord;
using LottieViewConvert.Utils;
using SukiUI.Toasts;

namespace LottieViewConvert.ViewModels;

public class DiscordStickerDownloadViewModel : Page
{
    private readonly HttpClient _httpClient;
    private bool _isLoading;
    private bool _isIndeterminateProgress = true;
    private double _loadingProgress;
    private string _loadingText = Resources.LoadingStickerPacksDotDotDot;
    private string _loadingDetailText = "";
    private readonly string _tempDirectory;
    private DiscordStickerPackViewModel? _selectedStickerPack;

    public DiscordStickerDownloadViewModel() : base(Resources.Discord, MaterialIconKind.Robot, 3)
    {
        var configService = new ConfigService();
        configService.LoadConfig();
        if (!string.IsNullOrEmpty(configService.GetConfig().ProxyAddress))
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(configService.GetConfig().ProxyAddress),
                UseProxy = true
            });
        }
        else
        {
            _httpClient = new HttpClient();
        }

        // Create temporary directory for storing downloaded sticker files
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DiscordStickers");
        if (!Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
        }

        StickerPacks = new ObservableCollection<DiscordStickerPackViewModel>();
            
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadStickerPacksAsync);
            
        // Auto-load on startup
        _ = LoadStickerPacksAsync();
    }

    public ObservableCollection<DiscordStickerPackViewModel> StickerPacks { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public DiscordStickerPackViewModel? SelectedStickerPack
    {
        get => _selectedStickerPack;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _selectedStickerPack, value);
            this.RaisePropertyChanged(nameof(HasSelectedPack));
            this.RaisePropertyChanged(nameof(SelectedPackInfoText));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsIndeterminateProgress
    {
        get => _isIndeterminateProgress;
        set => this.RaiseAndSetIfChanged(ref _isIndeterminateProgress, value);
    }

    public double LoadingProgress
    {
        get => _loadingProgress;
        set => this.RaiseAndSetIfChanged(ref _loadingProgress, value);
    }

    public string LoadingText
    {
        get => _loadingText;
        set => this.RaiseAndSetIfChanged(ref _loadingText, value);
    }

    public string LoadingDetailText
    {
        get => _loadingDetailText;
        set => this.RaiseAndSetIfChanged(ref _loadingDetailText, value);
    }

    public bool HasStickerPacks => StickerPacks.Any();
    public bool HasSelectedPack => SelectedStickerPack != null;

    public string PacksCountText => $"{StickerPacks.Count} {Resources.StickerPacksAvailable}";

    public string SelectedPackInfoText => SelectedStickerPack != null 
        ? $"{SelectedStickerPack.Stickers.Count} {Resources.Stickers}"
        : "";

    private async Task LoadStickerPacksAsync()
    {
        try
        {
            IsLoading = true;
            LoadingText = $"{Resources.FetchingDiscordStickerPacks}...";
            LoadingDetailText = Resources.ConnectingToDiscordAPI;
            IsIndeterminateProgress = true;

            var response = await _httpClient.GetStringAsync("https://discord.com/api/v9/sticker-packs?locale=en-US");
            var stickerPacksData = JsonSerializer.Deserialize<DiscordStickerPacksResponse>(response);

            StickerPacks.Clear();
            SelectedStickerPack = null;

            if (stickerPacksData?.StickerPacks != null)
            {
                IsIndeterminateProgress = false;
                LoadingText = $"{Resources.ProcessingStickerPacks}...";
                    
                for (var i = 0; i < stickerPacksData.StickerPacks.Length; i++)
                {
                    var packData = stickerPacksData.StickerPacks[i];
                    LoadingDetailText = $"{Resources.Processing} {packData.Name}...";
                    LoadingProgress = (double)(i + 1) / stickerPacksData.StickerPacks.Length * 100;

                    // Only process packs that contain Lottie stickers
                    if (packData.Stickers != null)
                    {
                        var lottieStickers = packData.Stickers.Where(s => s.FormatType == 3).ToArray();
                        if (lottieStickers.Length > 0)
                        {
                            var packViewModel = new DiscordStickerPackViewModel(packData, _httpClient, _tempDirectory);
                            StickerPacks.Add(packViewModel);
                        }
                    }

                    // Small delay to show progress
                    await Task.Delay(50);
                }
            }

            this.RaisePropertyChanged(nameof(HasStickerPacks));
            this.RaisePropertyChanged(nameof(PacksCountText));
        }
        catch (Exception ex)
        {
            LoadingText = Resources.FailedToLoadStickerPacks;
            LoadingDetailText = ex.Message;
            Logger.Error($"Failed to load Discord sticker packs: {ex.Message}", ex.StackTrace);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class DiscordStickerPackViewModel : ViewModelBase
{
    private string _saveLocation = Path.Combine(AppContext.BaseDirectory, "SavedStickers");
    private bool _isLoadingAll;

    public DiscordStickerPackViewModel(DiscordStickerPackData data, HttpClient httpClient, string tempDirectory)
    {
        Id = data.Id;
        Name = data.Name;
        Description = data.Description;
            
        Stickers = new ObservableCollection<DiscordStickerViewModel>();
            
        // Only include format_type 3 (Lottie) stickers
        if (data.Stickers != null)
            foreach (var stickerData in data.Stickers.Where(s => s.FormatType == 3))
            {
                var stickerViewModel = new DiscordStickerViewModel(stickerData, httpClient, tempDirectory);
                stickerViewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DiscordStickerViewModel.IsSelected))
                    {
                        this.RaisePropertyChanged(nameof(IsAllSelected));
                        this.RaisePropertyChanged(nameof(HasSelectedStickers));
                        this.RaisePropertyChanged(nameof(SelectionCountText));
                        this.RaisePropertyChanged(nameof(SelectedCountText));
                        this.RaisePropertyChanged(nameof(CanSave));
                    }

                    if (e.PropertyName == nameof(DiscordStickerViewModel.IsLoading) ||
                        e.PropertyName == nameof(DiscordStickerViewModel.IsLottieLoaded))
                    {
                        // Ensure UI thread updates for properties
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            this.RaisePropertyChanged(nameof(LoadAllText));
                            this.RaisePropertyChanged(nameof(CanLoadAll));
                        });
                    }
                };
                Stickers.Add(stickerViewModel);
            }

        ToggleAllSelectionCommand = ReactiveCommand.Create(ToggleAllSelection);
        ToggleStickerSelectionCommand = ReactiveCommand.Create<DiscordStickerViewModel>(ToggleStickerSelection);
        SelectSaveLocationCommand = ReactiveCommand.CreateFromTask(SelectSaveLocationAsync);
        SaveSelectedStickersCommand = ReactiveCommand.CreateFromTask(SaveSelectedStickersAsync, 
            this.WhenAnyValue(x => x.CanSave));
        LoadAllStickersCommand = ReactiveCommand.CreateFromTask(LoadAllStickersAsync,
            this.WhenAnyValue(x => x.CanLoadAll));
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public ObservableCollection<DiscordStickerViewModel> Stickers { get; }

    public ReactiveCommand<Unit, Unit> ToggleAllSelectionCommand { get; }
    public ReactiveCommand<DiscordStickerViewModel, Unit> ToggleStickerSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSaveLocationCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSelectedStickersCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAllStickersCommand { get; }

    public bool IsLoadingAll
    {
        get => _isLoadingAll;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _isLoadingAll, value);
            this.RaisePropertyChanged(nameof(LoadAllText));
            this.RaisePropertyChanged(nameof(CanLoadAll));
        }
    }

    public string StickerCountText => $"{Stickers.Count} {Resources.Stickers}";
    public bool IsAllSelected => Stickers.Any() && Stickers.All(s => s.IsSelected);
    public bool HasSelectedStickers => Stickers.Any(s => s.IsSelected);
    public string SelectionCountText => $"{Stickers.Count(s => s.IsSelected)}/{Stickers.Count} {Resources.Selected}";
    public string SelectedCountText => $"{Stickers.Count(s => s.IsSelected)} {Resources.Stickers} {Resources.Selected}";
    public string SaveLocationText => $"{Resources.SaveTo}: {_saveLocation}";

    public bool CanSave => HasSelectedStickers && !string.IsNullOrEmpty(_saveLocation);

    public bool CanLoadAll => !IsLoadingAll && Stickers.Any(s => !s.IsLottieLoaded && !s.IsLoading);
        
    public string LoadAllText
    {
        get
        {
            if (IsLoadingAll) return $"{Resources.Loading}...";
                
            var unloadedCount = Stickers.Count(s => !s.IsLottieLoaded && !s.IsLoading);
            var loadedCount = Stickers.Count(s => s.IsLottieLoaded);
                
            if (unloadedCount == 0)
                return $"{Resources.AllLoaded} ({loadedCount})";
                
            return $"{Resources.LoadAll} ({unloadedCount})";
        }
    }

    private void ToggleAllSelection()
    {
        var newState = !IsAllSelected;
        foreach (var sticker in Stickers)
        {
            sticker.IsSelected = newState;
        }
    }

    private void ToggleStickerSelection(DiscordStickerViewModel sticker)
    {
        sticker.IsSelected = !sticker.IsSelected;
    }

    private async Task LoadAllStickersAsync()
    {
        try
        {
            IsLoadingAll = true;
            Logger.Debug("Starting batch sticker loading");
                
            var unloadedStickers = Stickers.Where(s => !s.IsLottieLoaded && !s.IsLoading).ToList();
            Logger.Debug($"Number of stickers to load: {unloadedStickers.Count}");
                
            if (unloadedStickers.Count == 0) return;

            // Limit concurrency to avoid too many simultaneous requests
            const int maxConcurrency = 3; // Reduce concurrency count
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                
            var tasks = unloadedStickers.Select(async sticker =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Logger.Debug($"Starting to load sticker: {sticker.Name}");
                    await sticker.LoadAndDownloadStickerAsync();
                    Logger.Debug($"Completed loading sticker: {sticker.Name}, status: {sticker.IsLottieLoaded}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load sticker: {sticker.Name}, error: {ex.Message}", ex.StackTrace);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            Logger.Debug("All sticker loading tasks completed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Batch loading error: {ex.Message}", ex.StackTrace);
        }
        finally
        {
            // Ensure state is updated on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingAll = false;
                Logger.Debug("IsLoadingAll set to false");
            });
        }
    }

    private async Task SelectSaveLocationAsync()
    {
        try
        {
            Window mainWindow = Global.GetMainWindow();

            var selectedFolder = await FilePickerHelper.SelectFolderAsync(
                mainWindow, 
                Resources.ChooseSaveLocation);

            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                _saveLocation = selectedFolder;
                // Refresh SaveLocationText
                this.RaisePropertyChanged(nameof(SaveLocationText));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Choose save location failed: {ex.Message}", ex.StackTrace);
        }
    }

    private async Task SaveSelectedStickersAsync()
    {
        var selectedStickers = Stickers.Where(s => s.IsSelected).ToList();
            
        foreach (var sticker in selectedStickers)
        {
            await sticker.SaveToLocationAsync(_saveLocation);
        }
        var content = selectedStickers.Count == 1 
            ? $"{Resources.Sticker} {selectedStickers[0].Name} {Resources.SaveTo} {_saveLocation}" 
            : $"{selectedStickers.Count} {Resources.Stickers} {Resources.SaveTo} {_saveLocation}";
            
        Global.GetToastManager().CreateToast()
            .WithTitle(Resources.SaveSucceeded)
            .WithContent(content)
            .OfType(NotificationType.Success)
            .WithActionButton(Resources.Open, _ => { FolderUtil.OpenSavedFolder(_saveLocation); })
            .Dismiss().ByClicking()
            .Dismiss().After(TimeSpan.FromSeconds(5))
            .Queue();
    }

    // Override ToString for ComboBox display
    public override string ToString()
    {
        return Name;
    }
}

public class DiscordStickerViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient;
    private readonly string _tempDirectory;
    private bool _isSelected;
    private bool _isLoading;
    private bool _loadFailed;
    private bool _isLottieLoaded;
    private string? _localFilePath;

    public DiscordStickerViewModel(DiscordStickerData data, HttpClient httpClient, string tempDirectory)
    {
        _httpClient = httpClient;
        _tempDirectory = tempDirectory;
        Id = data.Id;
        Name = data.Name;
        Tags = data.Tags;
        Description = data.Description;

        // Create load command
        LoadStickerCommand = ReactiveCommand.CreateFromTask(LoadAndDownloadStickerAsync, 
            this.WhenAnyValue(x => x.IsLoading, x => x.IsLottieLoaded, (loading, loaded) => !loading && !loaded));
    }

    public string Id { get; }
    public string Name { get; }
    public string Tags { get; }
    public string? Description { get; }

    public ReactiveCommand<Unit, Unit> LoadStickerCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool LoadFailed
    {
        get => _loadFailed;
        set => this.RaiseAndSetIfChanged(ref _loadFailed, value);
    }

    public bool IsLottieLoaded
    {
        get => _isLottieLoaded;
        set => this.RaiseAndSetIfChanged(ref _isLottieLoaded, value);
    }

    public string? LocalFilePath
    {
        get => _localFilePath;
        set => this.RaiseAndSetIfChanged(ref _localFilePath, value);
    }

    public string TagsText => Tags;

    // Lazy loading - only start downloading when selected
    public async Task LoadAndDownloadStickerAsync()
    {
        if (IsLottieLoaded || IsLoading) return; // Avoid duplicate loading

        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                LoadFailed = false;
            });

            // Check if already downloaded
            var fileName = $"{Name}_{Id}.json";
            var tempFilePath = Path.Combine(_tempDirectory, fileName);
                
            if (!File.Exists(tempFilePath))
            {
                // Download sticker data
                var url = $"https://discord.com/stickers/{Id}.json";
                var jsonData = await _httpClient.GetStringAsync(url);
                    
                // Save to temporary file
                await File.WriteAllTextAsync(tempFilePath, jsonData);
            }

            // Set properties on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LocalFilePath = tempFilePath;
                IsLottieLoaded = true;
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadFailed = true;
            });
            Logger.Error($"Failed to load sticker {Name}: {ex.Message}", ex.StackTrace);
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    public async Task SaveToLocationAsync(string savePath)
    {
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        try
        {
            if (string.IsNullOrEmpty(LocalFilePath) || !File.Exists(LocalFilePath))
            {
                // If local file doesn't exist, re-download
                await LoadAndDownloadStickerAsync();
            }

            if (!string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath))
            {
                var fileName = $"{Name}_{Id}.json";
                var destinationPath = Path.Combine(savePath, fileName);
                    
                // Copy file to destination
                File.Copy(LocalFilePath, destinationPath, true);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save sticker {Name}: {ex.Message}", ex.StackTrace);
            throw;
        }
    }
}