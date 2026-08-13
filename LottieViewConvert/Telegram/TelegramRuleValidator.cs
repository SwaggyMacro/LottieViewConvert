using System;

namespace LottieViewConvert.Telegram;

public static class TelegramRuleValidator
{
    private const double Tolerance = 0.0001;

    public static TelegramValidationResult Validate(
        TelegramMediaFacts facts,
        TelegramPreset preset,
        bool requireTransparentPixels = true)
    {
        var result = new TelegramValidationResult { Preset = preset, Facts = facts };
        if (preset == TelegramPreset.Default) return result;

        var isTgs = preset is TelegramPreset.AnimatedSticker or TelegramPreset.AnimatedEmoji;
        if (isTgs)
        {
            Require(result, string.Equals(facts.DetectedFormat, "TGS", StringComparison.OrdinalIgnoreCase),
                "format", "Format must be TGS.");
            Require(result, facts.Width == 512 && facts.Height == 512,
                "canvas", "Canvas must be exactly 512×512.");
            Require(result, facts.Duration.HasValue && facts.Duration.Value <= TelegramRules.MaxDurationSeconds + Tolerance,
                "duration", "Duration must not exceed 3.00 s.");
            Require(result, facts.Fps.HasValue && Math.Abs(facts.Fps.Value - TelegramRules.RequiredTgsFps) <= Tolerance,
                "fps", "Telegram production TGS must run at 60 FPS.");
            Require(result, facts.FileSize.HasValue && facts.FileSize.Value <= TelegramRules.MaxTgsBytes,
                "file-size", "TGS file size must not exceed 64 KiB.");
            if (facts.LoopCapability == false)
                result.Issues.Add(new(TelegramValidationSeverity.Error, "loop", "Animation cannot be looped."));
            return result;
        }

        Require(result, string.Equals(facts.DetectedFormat, "WebM", StringComparison.OrdinalIgnoreCase),
            "format", "Format must be WebM.");
        Require(result, string.Equals(facts.Codec, "vp9", StringComparison.OrdinalIgnoreCase),
            "codec", "Video codec must be VP9.");
        Require(result, facts.Duration.HasValue && facts.Duration.Value <= TelegramRules.MaxDurationSeconds + Tolerance,
            "duration", "Duration must not exceed 3.00 s.");
        Require(result, facts.Fps.HasValue && facts.Fps.Value <= TelegramRules.MaxVideoFps + Tolerance,
            "fps", "Frame rate must not exceed 30 FPS.");
        Require(result, facts.FileSize.HasValue && facts.FileSize.Value <= TelegramRules.MaxWebmBytes,
            "file-size", "WebM file size must not exceed 256 KiB.");
        Require(result, facts.HasAudio == false, "audio", "Audio stream must be absent.");

        if (preset == TelegramPreset.VideoSticker)
        {
            Require(result,
                facts.Width is > 0 and <= 512 && facts.Height is > 0 and <= 512 &&
                (facts.Width == 512 || facts.Height == 512),
                "canvas", "One side must be exactly 512 px and the other no more than 512 px.");
        }
        else
        {
            Require(result, facts.Width == 100 && facts.Height == 100,
                "canvas", "Video emoji must be exactly 100×100.");
        }

        if (requireTransparentPixels)
            Require(result, facts.HasAlpha == true, "alpha", "Real transparent pixels are required by this production preset.");
        else if (facts.HasAlpha != true)
            result.Issues.Add(new(TelegramValidationSeverity.Warning, "alpha", "No transparent pixels detected."));

        return result;
    }

    private static void Require(TelegramValidationResult result, bool condition, string code, string message)
    {
        if (!condition) result.Issues.Add(new(TelegramValidationSeverity.Error, code, message));
    }
}
