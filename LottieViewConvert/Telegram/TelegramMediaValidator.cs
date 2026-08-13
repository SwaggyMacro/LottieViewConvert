using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lottie;
using SkiaSharp;

namespace LottieViewConvert.Telegram;

public sealed class TelegramMediaValidator
{
    public async Task<TelegramValidationResult> ValidateAsync(
        string path,
        TelegramPreset preset,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            var missing = new TelegramValidationResult { Preset = preset };
            missing.Issues.Add(new(TelegramValidationSeverity.Error, "missing", "File does not exist."));
            return missing;
        }

        return Path.GetExtension(path).Equals(".tgs", StringComparison.OrdinalIgnoreCase)
            ? ValidateTgs(path, preset)
            : await ValidateVideoAsync(path, preset, cancellationToken);
    }

    public TelegramValidationResult ValidateTgs(string path, TelegramPreset preset)
    {
        var isGzip = false;
        try
        {
            using var raw = File.OpenRead(path);
            isGzip = raw.ReadByte() == 0x1F && raw.ReadByte() == 0x8B;
        }
        catch { }

        TelegramMediaFacts facts;
        JsonDocument document;
        try
        {
            using var stream = LottieFile.OpenFile(path);
            document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var fps = GetDouble(root, "fr");
            var inPoint = GetDouble(root, "ip");
            var outPoint = GetDouble(root, "op");
            facts = new TelegramMediaFacts
            {
                DetectedFormat = "TGS",
                Width = GetInt(root, "w"),
                Height = GetInt(root, "h"),
                Fps = fps,
                InPoint = inPoint,
                OutPoint = outPoint,
                Duration = fps is > 0 && inPoint.HasValue && outPoint.HasValue
                    ? (outPoint.Value - inPoint.Value) / fps.Value
                    : null,
                FileSize = new FileInfo(path).Length,
                LoopCapability = true
            };
        }
        catch (Exception ex)
        {
            var invalid = new TelegramValidationResult { Preset = preset };
            invalid.Issues.Add(new(TelegramValidationSeverity.Error, "tgs-json", $"Invalid gzip/Lottie JSON: {ex.Message}"));
            return invalid;
        }

        using (document)
        {
            var result = TelegramRuleValidator.Validate(facts, preset);
            if (!isGzip)
                result.Issues.Add(new(TelegramValidationSeverity.Error, "tgs-gzip", "TGS must be a gzip-compressed Lottie JSON file."));
            InspectUnsupportedFeatures(document.RootElement, result);
            result.Issues.Add(new(TelegramValidationSeverity.Warning, "loop-control",
                "TGS has no intrinsic loop flag; Telegram players loop the timeline externally."));
            return result;
        }
    }

    public TelegramMediaFacts InspectLottieSource(string path)
    {
        using var stream = LottieFile.OpenFile(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var fps = GetDouble(root, "fr");
        var inPoint = GetDouble(root, "ip");
        var outPoint = GetDouble(root, "op");
        return new TelegramMediaFacts
        {
            DetectedFormat = Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
            Width = GetInt(root, "w"),
            Height = GetInt(root, "h"),
            Fps = fps,
            InPoint = inPoint,
            OutPoint = outPoint,
            Duration = fps is > 0 && inPoint.HasValue && outPoint.HasValue
                ? (outPoint.Value - inPoint.Value) / fps.Value
                : null,
            FileSize = new FileInfo(path).Length,
            LoopCapability = true
        };
    }

    private async Task<TelegramValidationResult> ValidateVideoAsync(
        string path,
        TelegramPreset preset,
        CancellationToken cancellationToken)
    {
        var probe = await RunAsync("ffprobe",
            ["-v", "error", "-show_streams", "-show_format", "-of", "json", path],
            cancellationToken);
        if (probe.ExitCode != 0)
        {
            var failed = new TelegramValidationResult { Preset = preset };
            failed.Issues.Add(new(TelegramValidationSeverity.Error, "ffprobe", $"ffprobe failed: {probe.Error.Trim()}"));
            return failed;
        }

        var facts = new TelegramMediaFacts
        {
            DetectedFormat = "WebM",
            FileSize = new FileInfo(path).Length,
            HasAudio = false,
            HasAlpha = false
        };

        try
        {
            using var document = JsonDocument.Parse(probe.Output);
            var root = document.RootElement;
            foreach (var stream in root.GetProperty("streams").EnumerateArray())
            {
                var codecType = GetString(stream, "codec_type");
                if (codecType == "audio") facts.HasAudio = true;
                if (codecType != "video" || facts.Codec != null) continue;

                facts.Codec = GetString(stream, "codec_name");
                facts.PixelFormat = GetString(stream, "pix_fmt");
                facts.Width = GetInt(stream, "width");
                facts.Height = GetInt(stream, "height");
                facts.Fps = ParseFraction(GetString(stream, "avg_frame_rate"))
                            ?? ParseFraction(GetString(stream, "r_frame_rate"));
            }

            if (root.TryGetProperty("format", out var format))
                facts.Duration = ParseDouble(GetString(format, "duration"));
        }
        catch (Exception ex)
        {
            var failed = new TelegramValidationResult { Preset = preset, Facts = facts };
            failed.Issues.Add(new(TelegramValidationSeverity.Error, "probe-json", $"Cannot parse ffprobe output: {ex.Message}"));
            return failed;
        }

        facts.HasAlpha = await HasRealAlphaAsync(path, cancellationToken);
        return TelegramRuleValidator.Validate(facts, preset, requireTransparentPixels: true);
    }

    private static async Task<bool> HasRealAlphaAsync(string path, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"lvc-alpha-{Guid.NewGuid():N}.png");
        try
        {
            // FFmpeg's native VP9 decoder drops the auxiliary alpha stream. Force libvpx-vp9.
            var decoded = await RunAsync("ffmpeg",
                ["-v", "error", "-y", "-c:v", "libvpx-vp9", "-i", path,
                 "-frames:v", "1", "-pix_fmt", "rgba", tempPath], cancellationToken);
            if (decoded.ExitCode != 0 || !File.Exists(tempPath)) return false;

            using var bitmap = SKBitmap.Decode(tempPath);
            if (bitmap == null) return false;
            var hasTransparent = false;
            var hasVisible = false;
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var alpha = bitmap.GetPixel(x, y).Alpha;
                if (alpha < byte.MaxValue) hasTransparent = true;
                if (alpha > 0) hasVisible = true;
                if (hasTransparent && hasVisible) return true;
            }

            return false;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static void InspectUnsupportedFeatures(JsonElement element, TelegramValidationResult result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("assets") && property.Value.ValueKind == JsonValueKind.Array &&
                    property.Value.EnumerateArray().Any(asset =>
                        asset.ValueKind == JsonValueKind.Object &&
                        (asset.TryGetProperty("p", out _) || asset.TryGetProperty("u", out _))))
                    AddUnique(result, "images", "Raster image assets are not supported by Telegram TGS.");

                if (property.NameEquals("ty") && property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetInt32(out var layerType))
                {
                    if (layerType == 1) AddUnique(result, "solids", "Solid layers are not supported by Telegram TGS.");
                    if (layerType == 5) AddUnique(result, "text", "Text layers are not supported by Telegram TGS.");
                }
                if (property.NameEquals("ddd") && property.Value.ValueKind == JsonValueKind.Number && property.Value.GetInt32() != 0)
                    AddUnique(result, "3d", "3D layers are not supported by Telegram TGS.");
                if (property.NameEquals("masksProperties") && property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                    AddUnique(result, "masks", "Masks are not supported by Telegram TGS.");
                if (property.NameEquals("ef") && property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                    AddUnique(result, "effects", "Layer effects are not supported by Telegram TGS.");
                if (property.NameEquals("x") && property.Value.ValueKind == JsonValueKind.String)
                    AddUnique(result, "expressions", "Expressions are not supported by Telegram TGS.");
                if (property.NameEquals("tm"))
                    AddUnique(result, "time-remap", "Time remapping is not supported by Telegram TGS.");
                if (property.NameEquals("sr") && property.Value.ValueKind == JsonValueKind.Number &&
                    Math.Abs(property.Value.GetDouble() - 1d) > 0.0001)
                    AddUnique(result, "time-stretch", "Time stretching is not supported by Telegram TGS.");
                if (property.NameEquals("ty") && property.Value.ValueKind == JsonValueKind.String)
                {
                    var shapeType = property.Value.GetString();
                    if (shapeType is "mm" or "rp" or "sr" or "gs")
                        AddUnique(result, $"shape-{shapeType}", $"Potentially unsupported shape feature detected: {shapeType}.", warning: true);
                }

                InspectUnsupportedFeatures(property.Value, result);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) InspectUnsupportedFeatures(child, result);
        }
    }

    private static void AddUnique(TelegramValidationResult result, string code, string message, bool warning = false)
    {
        if (result.Issues.Any(issue => issue.Code == code)) return;
        result.Issues.Add(new(warning ? TelegramValidationSeverity.Warning : TelegramValidationSeverity.Error, code, message));
    }

    private static int? GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static double? GetDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDouble(out var parsed) ? parsed : null;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static double? ParseFraction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('/');
        if (parts.Length == 2 && ParseDouble(parts[0]) is { } numerator && ParseDouble(parts[1]) is { } denominator && denominator != 0)
            return numerator / denominator;
        return ParseDouble(value);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string command, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start {command}.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stdout, await stderr);
    }
}
