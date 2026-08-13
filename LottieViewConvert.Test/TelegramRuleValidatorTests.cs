using LottieViewConvert.Telegram;

namespace LottieViewConvert.Test;

public class TelegramRuleValidatorTests
{
    [TestCase(512, 512, true)]
    [TestCase(512, 384, true)]
    [TestCase(513, 512, false)]
    public void VideoStickerSizeRules(int width, int height, bool expected)
    {
        var facts = ValidVideo(width, height);
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(100, 100, true)]
    [TestCase(99, 100, false)]
    public void VideoEmojiSizeRules(int width, int height, bool expected)
    {
        var facts = ValidVideo(width, height);
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoEmoji).IsValid, Is.EqualTo(expected));
    }

    [TestCase(2.99, true)]
    [TestCase(3.00, true)]
    [TestCase(3.01, false)]
    public void DurationBoundary(double duration, bool expected)
    {
        var facts = ValidVideo(512, 384);
        facts.Duration = duration;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(30, true)]
    [TestCase(31, false)]
    public void VideoFpsBoundary(double fps, bool expected)
    {
        var facts = ValidVideo(512, 384);
        facts.Fps = fps;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(60, true)]
    [TestCase(61, false)]
    public void TgsFpsBoundary(double fps, bool expected)
    {
        var facts = ValidTgs();
        facts.Fps = fps;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.AnimatedSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(64 * 1024, true)]
    [TestCase(64 * 1024 + 1, false)]
    public void TgsUsesBinary64KiBLimit(long bytes, bool expected)
    {
        var facts = ValidTgs();
        facts.FileSize = bytes;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.AnimatedSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(256 * 1024, true)]
    [TestCase(256 * 1024 + 1, false)]
    public void WebmUsesBinary256KiBLimit(long bytes, bool expected)
    {
        var facts = ValidVideo(512, 384);
        facts.FileSize = bytes;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void AudioMustBeAbsent(bool hasAudio, bool expected)
    {
        var facts = ValidVideo(512, 384);
        facts.HasAudio = hasAudio;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void ProductionPresetRequiresRealAlpha(bool hasAlpha, bool expected)
    {
        var facts = ValidVideo(512, 384);
        facts.HasAlpha = hasAlpha;
        Assert.That(TelegramRuleValidator.Validate(facts, TelegramPreset.VideoSticker).IsValid, Is.EqualTo(expected));
    }

    private static TelegramMediaFacts ValidTgs() => new()
    {
        DetectedFormat = "TGS",
        Width = 512,
        Height = 512,
        Duration = 3,
        Fps = 60,
        FileSize = TelegramRules.MaxTgsBytes,
        LoopCapability = true
    };

    private static TelegramMediaFacts ValidVideo(int width, int height) => new()
    {
        DetectedFormat = "WebM",
        Width = width,
        Height = height,
        Duration = 3,
        Fps = 30,
        FileSize = TelegramRules.MaxWebmBytes,
        Codec = "vp9",
        PixelFormat = "yuv420p",
        HasAlpha = true,
        HasAudio = false
    };
}
