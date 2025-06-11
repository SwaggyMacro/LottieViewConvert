using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

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