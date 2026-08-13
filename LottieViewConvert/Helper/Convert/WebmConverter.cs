using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Telegram;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// WebM format converter using FFmpeg.
    /// </summary>
    public class WebmConverter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;

        public WebmConverter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "WebM";
        public string FileExtension => "webm";

        public async Task<bool> ConvertAsync(
            string inputDirectory,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (options.TelegramVideo)
                return await ConvertTelegramAsync(inputDirectory, outputPath, options, progress, cancellationToken);

            var args = new List<string>
            {
                "-hide_banner",
                "-y",
                "-r", options.Fps.ToString(),
                "-i", "%05d.png",
                "-c:v", "libvpx-vp9",
                "-crf", GetCrfValue(options.Quality).ToString(),
                "-b:v", "0", // Use CRF mode
                "-cpu-used", GetCpuUsed(options.Quality).ToString(),
                "-row-mt", "1", // Enable row-based multithreading
                "-tile-columns", "2",
                "-tile-rows", "1",
                "-frame-parallel", "1",
                "-auto-alt-ref", "1",
                "-lag-in-frames", "25",
                "-pix_fmt", "yuv420p",
                "-progress", "pipe:1",
                "-nostats",
                outputPath
            };

            return await _commandExecutor.ExecuteAsync("ffmpeg", args, inputDirectory, progress, cancellationToken);
        }

        private async Task<bool> ConvertTelegramAsync(
            string inputDirectory,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress,
            CancellationToken cancellationToken)
        {
            var crfAttempts = new[] { 24, 32, 40, 48, 56 };
            var maxBytes = options.MaximumOutputBytes ?? TelegramRules.MaxWebmBytes;
            string? smallestAttempt = null;
            long smallestSize = long.MaxValue;

            for (var attempt = 0; attempt < Math.Min(crfAttempts.Length, TelegramRules.MaxEncodeAttempts); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attemptPath = $"{outputPath}.attempt-{attempt + 1}.webm";
                var args = BuildTelegramArgs(options.Fps, crfAttempts[attempt], attemptPath);
                Logger.Info($"Telegram VP9 encode attempt {attempt + 1}/{TelegramRules.MaxEncodeAttempts}, CRF {crfAttempts[attempt]}");

                var success = await _commandExecutor.ExecuteAsync(
                    "ffmpeg", args, inputDirectory, progress, cancellationToken);
                if (!success || !File.Exists(attemptPath))
                {
                    TryDelete(attemptPath);
                    continue;
                }

                var size = new FileInfo(attemptPath).Length;
                if (size < smallestSize)
                {
                    if (smallestAttempt != null && smallestAttempt != attemptPath) TryDelete(smallestAttempt);
                    smallestAttempt = attemptPath;
                    smallestSize = size;
                }

                if (size <= maxBytes)
                {
                    File.Move(attemptPath, outputPath, true);
                    CleanupAttempts(outputPath, attempt + 1);
                    Logger.Info($"Telegram VP9 output fits the limit: {size} bytes at CRF {crfAttempts[attempt]}");
                    return true;
                }
            }

            // Preserve the smallest decodable attempt for explicit INVALID reporting.
            if (smallestAttempt != null && File.Exists(smallestAttempt))
                File.Move(smallestAttempt, outputPath, true);
            CleanupAttempts(outputPath, TelegramRules.MaxEncodeAttempts);
            Logger.Warn($"Telegram VP9 output exceeds {maxBytes} bytes after bounded optimization.");
            return File.Exists(outputPath);
        }

        private static List<string> BuildTelegramArgs(int fps, int crf, string outputPath) =>
        [
            "-hide_banner",
            "-y",
            "-framerate", fps.ToString(),
            "-i", "%05d.png",
            "-c:v", "libvpx-vp9",
            "-pix_fmt", "yuva420p",
            "-auto-alt-ref", "0",
            "-b:v", "0",
            "-crf", crf.ToString(),
            "-deadline", "good",
            "-cpu-used", "2",
            "-row-mt", "1",
            "-an",
            "-metadata:s:v:0", "alpha_mode=1",
            "-progress", "pipe:1",
            "-nostats",
            outputPath
        ];

        private static void CleanupAttempts(string outputPath, int count)
        {
            for (var index = 1; index <= count; index++)
                TryDelete($"{outputPath}.attempt-{index}.webm");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        /// <summary>
        /// Gets the CRF value based on quality for VP9.
        /// Lower CRF = better quality, higher file size
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>CRF value (0-63)</returns>
        private static int GetCrfValue(int quality)
        {
            return quality switch
            {
                >= 95 => 15,  // Excellent quality
                >= 90 => 20,  // Very high quality
                >= 80 => 25,  // High quality
                >= 70 => 30,  // Good quality
                >= 60 => 35,  // Medium quality
                >= 50 => 40,  // Fair quality
                >= 40 => 45,  // Low quality
                >= 30 => 50,  // Poor quality
                _ => 55       // Very poor quality
            };
        }

        /// <summary>
        /// Gets the CPU usage setting based on quality.
        /// Lower values = slower encoding but better compression
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>CPU usage value (0-8)</returns>
        private static int GetCpuUsed(int quality)
        {
            return quality switch
            {
                >= 90 => 0,  // Best quality, slowest
                >= 80 => 1,
                >= 70 => 2,
                >= 60 => 3,
                >= 50 => 4,  // Balanced
                >= 40 => 5,
                >= 30 => 6,
                _ => 8       // Fastest encoding
            };
        }
    }
}
