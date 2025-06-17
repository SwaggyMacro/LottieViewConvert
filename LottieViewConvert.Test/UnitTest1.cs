using System.Net;
using LottieViewConvert.Helper;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace LottieViewConvert.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test1()
    {
        const string token = "BotToken";
        var downloader = new TelegramStickerEmojiDownloader(token, "http://127.0.0.1:10801");
        downloader.DownloadProgressChanged += (file, downloaded, total) =>
        {
            if (total > 0)
            {
                double pct = downloaded * 100.0 / total;
                Console.WriteLine($@"[{Path.GetFileName(file)}] {pct:F1}% ({downloaded}/{total} bytes)");
            }
            else
            {
                Console.WriteLine($@"[{Path.GetFileName(file)}] {downloaded} bytes downloaded");
            }
        };
        downloader.OverallProgressChanged += (downloadedAll, grandTotal) =>
        {
            if (grandTotal > 0)
                Console.WriteLine($@"Overall: {downloadedAll*100.0/grandTotal:F1}% ({downloadedAll}/{grandTotal} bytes)");
        };
        var setName = "CarBrandsSticker";
        var outputDir = "./stickers";
        // CarBrandsSticker
        Console.WriteLine($@"Start downloading ：{setName}");
        await downloader.DownloadAsync(setName, outputDir, 30);
        Console.WriteLine($@"Download succeeded, saved in {outputDir}");
    }
}

