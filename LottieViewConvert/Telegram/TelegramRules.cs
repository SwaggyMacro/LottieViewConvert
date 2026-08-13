namespace LottieViewConvert.Telegram;

/// <summary>
/// Centralized limits from https://core.telegram.org/stickers and
/// https://core.telegram.org/import-stickers. File-size limits use KiB (1024 bytes).
/// </summary>
public static class TelegramRules
{
    public const double MaxDurationSeconds = 3.0;
    public const double MaxVideoFps = 30.0;
    public const double RequiredTgsFps = 60.0;
    public const long MaxTgsBytes = 64L * 1024L;
    public const long MaxWebmBytes = 256L * 1024L;
    public const int StickerMaxSide = 512;
    public const int EmojiVideoSize = 100;
    public const int MaxEncodeAttempts = 5;
}
