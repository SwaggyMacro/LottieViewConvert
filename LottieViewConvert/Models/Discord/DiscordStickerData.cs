using System.Text.Json.Serialization;

namespace LottieViewConvert.Models.Discord;

public class DiscordStickerData
{
    public DiscordStickerData(string id, string name, string tags, int formatType, string? description)
    {
        Id = id;
        Name = name;
        Tags = tags;
        FormatType = formatType;
        Description = description;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tags")]
    public string Tags { get; set; }

    [JsonPropertyName("format_type")]
    public int FormatType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}