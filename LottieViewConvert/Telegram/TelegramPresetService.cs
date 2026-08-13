using System;
using System.Collections.Generic;
using System.Globalization;

namespace LottieViewConvert.Telegram;

public sealed record TelegramConversionPlan(
    int Width,
    int Height,
    int Fps,
    double PlaySpeed,
    double? MaxDurationSeconds,
    string Description);

public sealed class TelegramPresetService
{
    public static IReadOnlyList<string> PresetNames { get; } =
    [
        TelegramPresetNames.Default,
        TelegramPresetNames.AnimatedSticker,
        TelegramPresetNames.VideoSticker,
        TelegramPresetNames.AnimatedEmoji,
        TelegramPresetNames.VideoEmoji
    ];

    public static IReadOnlyList<string> DurationHandlingNames { get; } =
    ["Keep", "Trim to 3 s", "Speed up to 3 s"];

    public static DurationHandling ParseDurationHandling(string? value) => value switch
    {
        "Keep" => DurationHandling.Keep,
        "Speed up to 3 s" => DurationHandling.SpeedUpToThreeSeconds,
        _ => DurationHandling.TrimToThreeSeconds
    };

    public TelegramConversionPlan CreateVideoPlan(
        TelegramMediaFacts source,
        TelegramPreset preset,
        DurationHandling durationHandling)
    {
        if (preset is not (TelegramPreset.VideoSticker or TelegramPreset.VideoEmoji))
            throw new ArgumentException("A video Telegram preset is required.", nameof(preset));

        var sourceWidth = Math.Max(1, source.Width ?? 512);
        var sourceHeight = Math.Max(1, source.Height ?? 512);
        int width;
        int height;
        if (preset == TelegramPreset.VideoEmoji)
        {
            width = height = TelegramRules.EmojiVideoSize;
        }
        else if (sourceWidth >= sourceHeight)
        {
            width = TelegramRules.StickerMaxSide;
            height = MakeEven(Math.Max(2, (int)Math.Round(sourceHeight * TelegramRules.StickerMaxSide / (double)sourceWidth)));
        }
        else
        {
            height = TelegramRules.StickerMaxSide;
            width = MakeEven(Math.Max(2, (int)Math.Round(sourceWidth * TelegramRules.StickerMaxSide / (double)sourceHeight)));
        }

        var sourceFps = source.Fps is > 0 ? source.Fps.Value : TelegramRules.MaxVideoFps;
        var fps = Math.Max(1, (int)Math.Floor(Math.Min(sourceFps, TelegramRules.MaxVideoFps)));
        var duration = source.Duration ?? 0;
        var playSpeed = durationHandling == DurationHandling.SpeedUpToThreeSeconds && duration > TelegramRules.MaxDurationSeconds
            ? duration / TelegramRules.MaxDurationSeconds
            : 1d;
        double? maxDuration = durationHandling == DurationHandling.TrimToThreeSeconds
            ? TelegramRules.MaxDurationSeconds
            : null;

        var action = durationHandling switch
        {
            DurationHandling.Keep => "keep duration",
            DurationHandling.SpeedUpToThreeSeconds when duration > TelegramRules.MaxDurationSeconds =>
                $"speed up {playSpeed.ToString("0.###", CultureInfo.InvariantCulture)}× to 3.0 s",
            DurationHandling.SpeedUpToThreeSeconds => "keep duration (already ≤ 3.0 s)",
            _ when duration > TelegramRules.MaxDurationSeconds => "trim to 3.0 s",
            _ => "keep duration (already ≤ 3.0 s)"
        };

        var description = string.Join(Environment.NewLine,
            "Source",
            $"{sourceWidth}×{sourceHeight}",
            $"{duration.ToString("0.###", CultureInfo.InvariantCulture)} s",
            $"{sourceFps.ToString("0.###", CultureInfo.InvariantCulture)} FPS",
            string.Empty,
            $"{preset.DisplayName()} preset will:",
            $"- fit into {width}×{height} with transparent padding and no aspect distortion",
            $"- {action}",
            $"- convert to {fps} FPS",
            "- remove audio",
            "- encode VP9 with real alpha",
            $"- retry at most {TelegramRules.MaxEncodeAttempts} times to fit 256 KiB");

        return new(width, height, fps, playSpeed, maxDuration, description);
    }

    private static int MakeEven(int value) => value % 2 == 0 ? value : Math.Max(2, value - 1);
}
