namespace LottieViewConvert.Telegram;

public enum TelegramPreset
{
    Default,
    AnimatedSticker,
    VideoSticker,
    AnimatedEmoji,
    VideoEmoji
}

public enum DurationHandling
{
    Keep,
    TrimToThreeSeconds,
    SpeedUpToThreeSeconds
}

public static class TelegramPresetNames
{
    public const string Default = "Default";
    public const string AnimatedSticker = "Telegram Animated Sticker";
    public const string VideoSticker = "Telegram Video Sticker";
    public const string AnimatedEmoji = "Telegram Animated Emoji";
    public const string VideoEmoji = "Telegram Video Emoji";

    public static TelegramPreset Parse(string? value) => value switch
    {
        AnimatedSticker => TelegramPreset.AnimatedSticker,
        VideoSticker => TelegramPreset.VideoSticker,
        AnimatedEmoji => TelegramPreset.AnimatedEmoji,
        VideoEmoji => TelegramPreset.VideoEmoji,
        _ => TelegramPreset.Default
    };

    public static string DisplayName(this TelegramPreset preset) => preset switch
    {
        TelegramPreset.AnimatedSticker => AnimatedSticker,
        TelegramPreset.VideoSticker => VideoSticker,
        TelegramPreset.AnimatedEmoji => AnimatedEmoji,
        TelegramPreset.VideoEmoji => VideoEmoji,
        _ => Default
    };

    public static string FileSuffix(this TelegramPreset preset) => preset switch
    {
        TelegramPreset.AnimatedSticker => "telegram-sticker",
        TelegramPreset.VideoSticker => "telegram-video-sticker",
        TelegramPreset.AnimatedEmoji => "telegram-emoji",
        TelegramPreset.VideoEmoji => "telegram-video-emoji",
        _ => "converted"
    };
}
