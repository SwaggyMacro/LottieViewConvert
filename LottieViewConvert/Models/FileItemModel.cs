using ReactiveUI;

namespace LottieViewConvert.Models;

public class FileItemModel : ReactiveObject
{
    private string _fileName = string.Empty;
    public string FileName
    {
        get => _fileName;
        set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

    private string _fullPath = string.Empty;
    public string FullPath
    {
        get => _fullPath;
        set => this.RaiseAndSetIfChanged(ref _fullPath, value);
    }

    private ConversionStatus _status = ConversionStatus.Pending;
    public ConversionStatus Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }
}

public enum ConversionStatus
{
    Pending,
    Converting,
    Success,
    Failed
}