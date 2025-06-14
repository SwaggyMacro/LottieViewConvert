using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lottie;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert;

[SuppressMessage("ReSharper", "UnusedParameter.Local")]
public class Converter
{
    private readonly string _source;
    private readonly string _outputDirectory;
    private readonly ConversionOptions _options;
    private readonly IFrameExporter _frameExporter;
    private readonly IProgressTracker _progressTracker;
    private readonly ConversionProgressCalculator _progressCalculator;
    private readonly Dictionary<string, IFormatConverter> _formatConverters;

    private CancellationTokenSource? _cts;
    private int _totalPngFrames;
    private TimeSpan _estimatedDuration;

    public event Action<ConversionProgressEventArgs>? DetailedProgressUpdated
    {
        add => _progressTracker.DetailedProgressUpdated += value;
        remove => _progressTracker.DetailedProgressUpdated -= value;
    }

    public event Action<TimeSpan>? ProgressUpdated
    {
        add => _progressTracker.ProgressUpdated += value;
        remove => _progressTracker.ProgressUpdated -= value;
    }

    public Converter(
        string source,
        string outputDirectory,
        ConversionOptions? options = null,
        IFrameExporter? frameExporter = null,
        IProgressTracker? progressTracker = null,
        Dictionary<string, IFormatConverter>? formatConverters = null)
    {
        _source = source;
        _outputDirectory = outputDirectory;
        _options = options ?? new ConversionOptions();
        _frameExporter = frameExporter ?? new FrameExporter();
        _progressTracker = progressTracker ?? new ProgressTracker();
        _progressCalculator = new ConversionProgressCalculator();
        
        var commandExecutor = new CommandExecutor();
        _formatConverters = formatConverters ?? new Dictionary<string, IFormatConverter>
        {
            ["gif"] = new GifConverter(commandExecutor),
            ["mp4"] = new Mp4Converter(commandExecutor),
            ["webp"] = new WebpConverter(),
            ["apng"] = new ApngConverter(commandExecutor),
            ["webm"] = new WebmConverter(commandExecutor),
            ["mkv"] = new MkvConverter(commandExecutor),
            ["avif"] = new AvifConverter(commandExecutor)
        };
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public async Task<bool> ToGif(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("gif", progress, externalToken);
    }

    public async Task<bool> ToMp4(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("mp4", progress, externalToken);
    }

    public async Task<bool> ToWebp(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("webp", progress, externalToken);
    }

    public async Task<bool> ToApng(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("apng", progress, externalToken);
    }
        
    public async Task<bool> ToWebm(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("webm", progress, externalToken);
    }
        
    public async Task<bool> ToMkv(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("mkv", progress, externalToken);
    }
        
    public async Task<bool> ToAvif(IProgress<TimeSpan>? progress = null, CancellationToken? externalToken = null)
    {
        return await ConvertToFormat("avif", progress, externalToken);
    }
        
        

    private async Task<bool> ConvertToFormat(
        string format,
        IProgress<TimeSpan>? progress = null,
        CancellationToken? externalToken = null)
    {
        if (!_formatConverters.TryGetValue(format, out var converter))
        {
            Logger.Error($"Unsupported format: {format}");
            return false;
        }

        _cts = externalToken != null
            ? CancellationTokenSource.CreateLinkedTokenSource(externalToken.Value)
            : new CancellationTokenSource();

        var token = _cts.Token;

        try
        {
            ReportProgress(ConversionStage.Initializing, "Ready to start...", 0);

            var tmpDir = Path.Combine(_outputDirectory, $"tmp_{format}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tmpDir);

            var outputFileName = Path.GetFileNameWithoutExtension(_source);
            var outputPath = Path.Combine(_outputDirectory, $"{outputFileName}.{converter.FileExtension}");
            
            ReportProgress(ConversionStage.PngExport, "Export PNG frame...", 0);

            var pngExportProgress = new Progress<ExportProgressEventArgs>(pngProgress =>
            {
                _totalPngFrames = pngProgress.TotalFrames;
                _estimatedDuration = TimeSpan.FromSeconds(_totalPngFrames / (double)_options.Fps);

                var progressArgs = _progressCalculator.CalculateProgress(
                    ConversionStage.PngExport,
                    $"Export PNG frame ({pngProgress.CurrentFrame}/{pngProgress.TotalFrames})",
                    pngProgress.ProgressPercentage,
                    pngProgress);

                _progressTracker.ReportProgress(progressArgs);
            });

            _frameExporter.ExportPngSequence(
                _source,
                tmpDir,
                _options.Fps,
                _options.PlaySpeed,
                _options.Width,
                _options.Height,
                pngExportProgress,
                token);
            
            ReportProgress(ConversionStage.Converting, $"convert to {converter.FormatName}...", 0);

            var conversionProgress = new Progress<TimeSpan>(timespan =>
            {
                var conversionProgressPercentage = _estimatedDuration.TotalSeconds > 0
                    ? Math.Min(100, timespan.TotalSeconds / _estimatedDuration.TotalSeconds * 100)
                    : 0;

                var progressArgs = _progressCalculator.CalculateProgress(
                    ConversionStage.Converting,
                    $"convert to {converter.FormatName} ({timespan:mm\\:ss}/{_estimatedDuration:mm\\:ss})",
                    conversionProgressPercentage,
                    null,
                    timespan);

                _progressTracker.ReportProgress(progressArgs);
            });

            var success = await converter.ConvertAsync(tmpDir, outputPath, _options, conversionProgress, token);
            if (!success) return false;
            
            ReportProgress(ConversionStage.Cleanup, "cleanup the tmp files...", 0);
            CleanupTempDirectory(tmpDir);
            
            ReportProgress(ConversionStage.Completed, "Conversion completed.", 100);

            return true;
        }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Conversion {format} was canceled: {_source}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Convert to {format} failed: {_source}, {ex.Message}");
            return false;
        }
    }

    private void ReportProgress(ConversionStage stage, string description, double stageProgress)
    {
        var progressArgs = _progressCalculator.CalculateProgress(stage, description, stageProgress);
        _progressTracker.ReportProgress(progressArgs);
    }

    private static void CleanupTempDirectory(string tmpDir)
    {
        try
        {
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to cleanup the tmp files: {ex.Message}");
        }
    }
}