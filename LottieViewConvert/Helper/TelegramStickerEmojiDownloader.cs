using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LottieViewConvert.Helper
{
    /// <summary>
    /// Downloads Telegram stickers with support for concurrency, single file progress, and overall download progress feedback.
    /// Supports both regular stickers and emoji stickers.
    /// </summary>
    public class TelegramStickerEmojiDownloader
    {
        private readonly ITelegramBotClient _botClient;
        private readonly HttpClient _httpClient;
        private readonly string _botToken;

        /// <summary>
        /// when downloader reads a chunk of bytes, it provides the current file's downloaded bytes and total bytes.
        /// </summary>
        public event Action<string /*filePath*/, long /*downloadedBytes*/, long /*totalBytes*/>?
            DownloadProgressChanged;

        /// <summary>
        /// when a single sticker file is downloaded, it provides the local file path.
        /// </summary>
        public event Action<string /*filePath*/>? DownloadFileCompleted;

        /// <summary>
        /// when the overall download progress is updated, it provides the total downloaded bytes and grand total bytes.
        /// </summary>
        public event Action<long /*totalDownloadedBytes*/, long /*grandTotalBytes*/>? OverallProgressChanged;

        /// <summary>
        /// when metadata fetching progress is updated, it provides the fetched count and total count.
        /// </summary>
        public event Action<int /*fetchedCount*/, int /*totalCount*/>? MetadataProgressChanged;

        public TelegramStickerEmojiDownloader(string botToken, string? proxyUrl = "")
        {
            if (string.IsNullOrWhiteSpace(botToken))
                throw new ArgumentException(@"Bot token cannot be null or empty", nameof(botToken));

            _botToken = botToken;
            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                var handler = new SocketsHttpHandler
                {
                    Proxy = new WebProxy(proxyUrl, BypassOnLocal: false),
                    UseProxy = true
                };
                _httpClient = new HttpClient(handler);
                _botClient = new TelegramBotClient(botToken, _httpClient);
            }
            else
            {
                _httpClient = new HttpClient();
                _botClient = new TelegramBotClient(botToken, _httpClient);
            }
        }

        /// <summary>
        /// Concurrently download a sticker set by its name. Supports both regular stickers and emoji stickers.
        /// </summary>
        /// <param name="stickerSetName">Sticker set name, e.g. "CarBrandsSticker" or "DeadpoolWolverineEmoji"</param>
        /// <param name="outputDirectory">Output directory to save downloaded stickers</param>
        /// <param name="maxConcurrency">Maximum number of concurrent downloads. Default is 4.</param>
        /// <param name="cancellationToken"></param>
        public async Task DownloadAsync(
            string stickerSetName,
            string outputDirectory,
            int maxConcurrency = 4,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(stickerSetName))
                throw new ArgumentException(@"Sticker set name cannot be null or empty", nameof(stickerSetName));
            Directory.CreateDirectory(outputDirectory);

            // 1. Get sticker set basic info
            StickerSet set = await _botClient.GetStickerSet(stickerSetName, cancellationToken).ConfigureAwait(false);

            // 2. Concurrently fetch file metadata
            var files = await GetFileMetadataAsync(set.Stickers, set.StickerType, maxConcurrency, cancellationToken)
                .ConfigureAwait(false);

            // 3. Calculate total bytes
            long grandTotalBytes = 0;
            foreach (var (_, _, size, _, _) in files)
            {
                grandTotalBytes += size;
            }

            // 4. Concurrently download all files
            await DownloadFilesAsync(files, outputDirectory, grandTotalBytes, maxConcurrency, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Concurrently fetch metadata for all stickers in the set, including file ID, file path, size, unique ID, and emoji.
        /// </summary>
        private async Task<(string FileId, string FilePath, long Size, string UniqueId, string? Emoji)[]>
            GetFileMetadataAsync(
                Sticker[] stickers,
                StickerType stickerType,
                int maxConcurrency,
                CancellationToken cancellationToken)
        {
            var files =
                new (string FileId, string FilePath, long Size, string UniqueId, string? Emoji)[stickers.Length];
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();
            int fetchedCount = 0;

            for (int i = 0; i < stickers.Length; i++)
            {
                int index = i; // capture loop variable
                var sticker = stickers[i];

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        TGFile info = await _botClient.GetFile(sticker.FileId, cancellationToken).ConfigureAwait(false);
                        long size = info.FileSize ?? 0;

                        // For emoji stickers, include the emoji in the metadata
                        string? emoji = (stickerType == StickerType.CustomEmoji &&
                                         !string.IsNullOrWhiteSpace(sticker.Emoji))
                            ? sticker.Emoji
                            : null;

                        files[index] = (sticker.FileId, info.FilePath!, size, sticker.FileUniqueId, emoji);

                        // Update metadata progress
                        int completed = Interlocked.Increment(ref fetchedCount);
                        MetadataProgressChanged?.Invoke(completed, stickers.Length);
                    } catch (Exception ex)
                    {
                        // Log or handle the error as needed
                        Logger.Error($"Error fetching metadata for sticker {sticker.FileId}: {ex.Message}");
                        files[index] = (sticker.FileId, string.Empty, 0, sticker.FileUniqueId, null);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return files;
        }

        /// <summary>
        /// Concurrently download all sticker files to the specified output directory.
        /// </summary>
        private async Task DownloadFilesAsync(
            (string FileId, string FilePath, long Size, string UniqueId, string? Emoji)[] files,
            string outputDirectory,
            long grandTotalBytes,
            int maxConcurrency,
            CancellationToken cancellationToken)
        {
            long overallDownloaded = 0;
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var (fileId, filePath, size, uniqueId, emoji) in files)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Generate filename with emoji support
                        string fileName = GenerateFileName(uniqueId, filePath, emoji);
                        string localPath = Path.Combine(outputDirectory, fileName);

                        // Download and report progress
                        await DownloadInternalAsync(
                            fileId, filePath, localPath, size,
                            onChunk: downloadedBytes =>
                            {
                                // Single file progress, thread-safe
                                DownloadProgressChanged?.Invoke(localPath, downloadedBytes, size);
                                // Overall progress, thread-safe
                                long currentOverall = Interlocked.Read(ref overallDownloaded);
                                OverallProgressChanged?.Invoke(currentOverall + downloadedBytes, grandTotalBytes);
                            },
                            cancellationToken: cancellationToken
                        ).ConfigureAwait(false);

                        // File download completed, update overall progress, thread-safe
                        Interlocked.Add(ref overallDownloaded, size);
                        DownloadFileCompleted?.Invoke(localPath);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate filename for sticker with emoji support
        /// </summary>
        private string GenerateFileName(string uniqueId, string filePath, string? emoji)
        {
            var extension = Path.GetExtension(filePath);
            
            if (string.IsNullOrWhiteSpace(emoji)) return $"{uniqueId}{extension}";
            
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder();
            foreach (var c in emoji.Where(c => c != '\uFE0F' && Array.IndexOf(invalidChars, c) < 0))
            {
                builder.Append(c);
            }
            var cleanEmoji = builder.ToString();
            return !string.IsNullOrWhiteSpace(cleanEmoji) ? $"{cleanEmoji}_{uniqueId}{extension}" : $"{uniqueId}{extension}";
        }

        /// <summary>
        /// Download sticker / emoji file from telegram server
        /// </summary>
        private async Task DownloadInternalAsync(
            string fileId,
            string filePath,
            string destinationPath,
            long totalSize,
            Action<long /*downloadedBytes*/> onChunk,
            CancellationToken cancellationToken)
        {
            string fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";
            using var response = await _httpClient.GetAsync(
                fileUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            ).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                downloaded += read;
                onChunk(downloaded);
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}