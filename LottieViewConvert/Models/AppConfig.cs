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
    }
}