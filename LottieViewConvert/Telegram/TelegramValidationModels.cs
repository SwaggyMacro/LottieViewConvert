using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LottieViewConvert.Telegram;

public enum TelegramValidationSeverity
{
    Error,
    Warning
}

public sealed record TelegramValidationIssue(
    TelegramValidationSeverity Severity,
    string Code,
    string Message);

public sealed class TelegramMediaFacts
{
    public string? DetectedFormat { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? Duration { get; set; }
    public double? Fps { get; set; }
    public long? FileSize { get; set; }
    public string? Codec { get; set; }
    public string? PixelFormat { get; set; }
    public bool? HasAlpha { get; set; }
    public bool? HasAudio { get; set; }
    public double? InPoint { get; set; }
    public double? OutPoint { get; set; }
    public bool? LoopCapability { get; set; }
}

public sealed class TelegramValidationResult
{
    public TelegramPreset Preset { get; init; }
    public TelegramMediaFacts Facts { get; init; } = new();
    public List<TelegramValidationIssue> Issues { get; } = [];
    public bool IsValid => Issues.All(issue => issue.Severity != TelegramValidationSeverity.Error);
    public IEnumerable<TelegramValidationIssue> Errors => Issues.Where(issue => issue.Severity == TelegramValidationSeverity.Error);
    public IEnumerable<TelegramValidationIssue> Warnings => Issues.Where(issue => issue.Severity == TelegramValidationSeverity.Warning);

    public string ToDisplayText(string heading)
    {
        var facts = Facts;
        var lines = new List<string> { heading, string.Empty };
        if (facts.Width.HasValue && facts.Height.HasValue)
            lines.Add($"{facts.Width}×{facts.Height}");
        if (facts.Duration.HasValue)
            lines.Add($"{facts.Duration.Value.ToString("0.###", CultureInfo.InvariantCulture)} s");
        if (facts.Fps.HasValue)
            lines.Add($"{facts.Fps.Value.ToString("0.###", CultureInfo.InvariantCulture)} FPS");
        if (!string.IsNullOrWhiteSpace(facts.Codec)) lines.Add($"Codec: {facts.Codec}");
        if (!string.IsNullOrWhiteSpace(facts.PixelFormat)) lines.Add($"Pixel format: {facts.PixelFormat}");
        if (facts.HasAlpha.HasValue) lines.Add($"Alpha: {(facts.HasAlpha.Value ? "yes" : "missing")}");
        if (facts.HasAudio.HasValue) lines.Add($"Audio: {(facts.HasAudio.Value ? "present" : "none")}");
        if (facts.FileSize.HasValue) lines.Add($"File size: {facts.FileSize.Value / 1024d:0.##} KiB");

        foreach (var issue in Issues)
        {
            lines.Add($"{(issue.Severity == TelegramValidationSeverity.Error ? "✕" : "⚠")} {issue.Message}");
        }

        lines.Add(string.Empty);
        lines.Add(IsValid ? "VALID" : "INVALID");
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class TelegramExportResult
{
    public required string OutputPath { get; init; }
    public required TelegramValidationResult Validation { get; init; }
    public required string SourcePlan { get; init; }
    public bool IsValid => Validation.IsValid;
}
