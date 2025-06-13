using Newtonsoft.Json;

namespace LottieViewConvert.Models
{
    public class AppConfig
    {
        [JsonProperty("proxyAddress")]
        public string ProxyAddress { get; set; } = string.Empty;

        [JsonProperty("telegramBotToken")]
        public string TelegramBotToken { get; set; } = string.Empty;
        [JsonProperty("language")]
        public string? Language { get; set; } = "auto";
        [JsonProperty("ffmpegPath")]
        public string FFmpegPath { get; set; } = string.Empty;
        [JsonProperty("gifskiPath")]
        public string GifskiPath { get; set; } = string.Empty;
    }
}