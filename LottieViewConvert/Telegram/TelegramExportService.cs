using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Helper.Convert;

namespace LottieViewConvert.Telegram;

public sealed class TelegramExportService
{
    private readonly TelegramMediaValidator _validator = new();
    private readonly TelegramPresetService _presetService = new();

    public async Task<TelegramExportResult> ExportAsync(
        string sourcePath,
        string outputDirectory,
        TelegramPreset preset,
        DurationHandling durationHandling,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (preset == TelegramPreset.Default)
            throw new ArgumentException("Default is not a Telegram export preset.", nameof(preset));
        Directory.CreateDirectory(outputDirectory);

        if (preset is TelegramPreset.AnimatedSticker or TelegramPreset.AnimatedEmoji)
        {
            var outputPath = GetUniqueOutputPath(sourcePath, outputDirectory, preset, ".tgs");
            File.Copy(sourcePath, outputPath, overwrite: false);
            var validation = await _validator.ValidateAsync(outputPath, preset, cancellationToken);
            return new TelegramExportResult
            {
                OutputPath = outputPath,
                Validation = validation,
                SourcePlan = "TGS is validated and copied byte-for-byte; no destructive vector optimization is applied."
            };
        }

        var facts = _validator.InspectLottieSource(sourcePath);
        var plan = _presetService.CreateVideoPlan(facts, preset, durationHandling);
        var outputPathForNaming = GetUniqueOutputPath(sourcePath, outputDirectory, preset, ".webm");
        var outputFileName = Path.GetFileNameWithoutExtension(outputPathForNaming);
        var converter = new Converter(sourcePath, outputDirectory, new ConversionOptions
        {
            Width = plan.Width,
            Height = plan.Height,
            Fps = plan.Fps,
            PlaySpeed = plan.PlaySpeed,
            MaxDurationSeconds = plan.MaxDurationSeconds,
            PreserveAspectRatio = true,
            TelegramVideo = true,
            MaximumOutputBytes = TelegramRules.MaxWebmBytes,
            OutputFileName = outputFileName,
            Quality = 90
        });
        converter.DetailedProgressUpdated += update => progress?.Report(update.OverallProgress);
        var success = await converter.ToWebm(null, cancellationToken);
        var actualOutput = converter.OutputPath ?? outputPathForNaming;
        TelegramValidationResult validationResult;
        if (!success || !File.Exists(actualOutput))
        {
            validationResult = new TelegramValidationResult { Preset = preset };
            validationResult.Issues.Add(new(TelegramValidationSeverity.Error, "encode", "VP9 WebM encoding failed."));
        }
        else
        {
            // VALID is based on the actual encoded file, never on requested settings.
            validationResult = await _validator.ValidateAsync(actualOutput, preset, cancellationToken);
        }

        return new TelegramExportResult
        {
            OutputPath = actualOutput,
            Validation = validationResult,
            SourcePlan = plan.Description
        };
    }

    public TelegramValidationResult ValidateTgsSource(string path, TelegramPreset preset) =>
        _validator.ValidateTgs(path, preset);

    public string DescribeVideoSource(string path, TelegramPreset preset, DurationHandling durationHandling)
    {
        var facts = _validator.InspectLottieSource(path);
        return _presetService.CreateVideoPlan(facts, preset, durationHandling).Description;
    }

    private static string GetUniqueOutputPath(
        string sourcePath, string outputDirectory, TelegramPreset preset, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var stem = $"{baseName}.{preset.FileSuffix()}";
        var candidate = Path.Combine(outputDirectory, stem + extension);
        var index = 2;
        while (File.Exists(candidate))
            candidate = Path.Combine(outputDirectory, $"{stem}-{index++}{extension}");
        return candidate;
    }
}
