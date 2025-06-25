using System.Text.Json.Serialization;

namespace LottieViewConvert.Models.Discord;

public class DiscordStickerPackData
{
    public DiscordStickerPackData(string id, string name, string description, DiscordStickerData[]? stickers)
    {
        Id = id;
        Name = name;
        Description = description;
        Stickers = stickers;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("stickers")]
    public DiscordStickerData[]? Stickers { get; set; }
}