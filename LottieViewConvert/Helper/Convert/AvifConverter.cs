using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// AVIF format converter using FFmpeg.
    /// </summary>
    public class AvifConverter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;

        public AvifConverter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "AVIF";
        public string FileExtension => "avif";

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
                "-c:v", "libaom-av1",
                "-crf", GetCrfValue(options.Quality).ToString(),
                "-b:v", "0", // Use CRF mode
                "-cpu-used", GetCpuUsed(options.Quality).ToString(),
                "-row-mt", "1", // Enable row-based multithreading
                "-tiles", GetTileConfiguration(options.Quality),
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                "-progress", "pipe:1",
                "-nostats"
            };

            // Add quality-specific optimizations
            AddQualityOptimizations(args, options.Quality);

            args.Add(outputPath);

            return await _commandExecutor.ExecuteAsync("ffmpeg", args, inputDirectory, progress, cancellationToken);
        }

        /// <summary>
        /// Gets the CRF value based on quality for AV1.
        /// AV1 typically uses lower CRF values than other codecs for similar quality.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>CRF value (0-63)</returns>
        private static int GetCrfValue(int quality)
        {
            return quality switch
            {
                >= 95 => 15,  // Excellent quality
                >= 90 => 18,  // Very high quality
                >= 80 => 22,  // High quality
                >= 70 => 26,  // Good quality
                >= 60 => 30,  // Medium quality
                >= 50 => 34,  // Fair quality
                >= 40 => 38,  // Low quality
                >= 30 => 42,  // Poor quality
                _ => 46       // Very poor quality
            };
        }

        /// <summary>
        /// Gets the CPU usage setting based on quality.
        /// Lower values = slower encoding but better compression efficiency.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>CPU usage value (0-8)</returns>
        private static int GetCpuUsed(int quality)
        {
            return quality switch
            {
                >= 90 => 2,  // Best quality, slower
                >= 80 => 3,
                >= 70 => 4,
                >= 60 => 5,
                >= 50 => 6,  // Balanced
                >= 40 => 7,
                _ => 8       // Fastest encoding
            };
        }

        /// <summary>
        /// Gets the tile configuration for parallel encoding.
        /// More tiles = faster encoding but slightly reduced efficiency.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>Tile configuration string</returns>
        private static string GetTileConfiguration(int quality)
        {
            return quality switch
            {
                >= 80 => "2x1",   // Fewer tiles for better compression
                >= 60 => "2x2",   // Balanced
                >= 40 => "3x2",   // More tiles for faster encoding
                _ => "4x2"        // Maximum tiles for speed
            };
        }

        /// <summary>
        /// Adds quality-specific optimization parameters.
        /// </summary>
        /// <param name="args">The argument list to modify</param>
        /// <param name="quality">Quality percentage (0-100)</param>
        private static void AddQualityOptimizations(List<string> args, int quality)
        {
            if (quality >= 80)
            {
                // High quality optimizations
                args.AddRange(new[] 
                { 
                    "-aom-params", "enable-chroma-deltaq=1:enable-qm=1:qm-min=0:qm-max=15"
                });
            }
            else if (quality >= 60)
            {
                // Medium quality optimizations
                args.AddRange(new[] 
                { 
                    "-aom-params", "enable-chroma-deltaq=1"
                });
            }
            else if (quality < 40)
            {
                // Low quality - prioritize speed
                args.AddRange(new[] 
                { 
                    "-aom-params", "enable-cdef=0:enable-restoration=0"
                });
            }

            // Animation-specific tuning
            if (quality >= 70)
            {
                args.AddRange(new[] { "-tune", "psnr" });
            }
        }
    }
}