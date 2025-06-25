using System.Text.Json.Serialization;

namespace LottieViewConvert.Models.Discord;

public class DiscordStickerPacksResponse
{
    [JsonPropertyName("sticker_packs")]
    public DiscordStickerPackData[]? StickerPacks { get; set; }
}