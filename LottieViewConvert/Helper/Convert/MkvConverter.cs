using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// MKV format converter using FFmpeg.
    /// </summary>
    public class MkvConverter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;

        public MkvConverter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "MKV";
        public string FileExtension => "mkv";

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
                "-c:v", GetVideoCodec(options.Quality),
                "-crf", GetCrfValue(options.Quality).ToString(),
                "-preset", GetPreset(options.Quality),
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                "-progress", "pipe:1",
                "-nostats"
            };

            // Add codec-specific options
            AddCodecSpecificOptions(args, options.Quality);

            args.Add(outputPath);

            return await _commandExecutor.ExecuteAsync("ffmpeg", args, inputDirectory, progress, cancellationToken);
        }

        /// <summary>
        /// Gets the video codec based on quality requirements.
        /// Higher quality uses more advanced codecs.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>Video codec name</returns>
        private static string GetVideoCodec(int quality)
        {
            return quality switch
            {
                >= 80 => "libx265",  // HEVC - better compression for high quality
                >= 50 => "libx264",  // H.264 - good balance
                _ => "libx264"       // H.264 - widely compatible
            };
        }

        /// <summary>
        /// Gets the CRF value based on quality and codec.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>CRF value</returns>
        private static int GetCrfValue(int quality)
        {
            return quality switch
            {
                >= 95 => 18,  // Excellent quality
                >= 90 => 20,  // Very high quality
                >= 80 => 23,  // High quality
                >= 70 => 26,  // Good quality
                >= 60 => 28,  // Medium quality
                >= 50 => 30,  // Fair quality
                >= 40 => 32,  // Low quality
                >= 30 => 35,  // Poor quality
                _ => 40       // Very poor quality
            };
        }

        /// <summary>
        /// Gets the encoding preset based on quality.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>Encoding preset</returns>
        private static string GetPreset(int quality)
        {
            return quality switch
            {
                >= 90 => "veryslow",  // Best compression
                >= 80 => "slow",
                >= 60 => "medium",
                >= 40 => "fast",
                >= 20 => "faster",
                _ => "veryfast"       // Fastest encoding
            };
        }

        /// <summary>
        /// Adds codec-specific optimization options.
        /// </summary>
        /// <param name="args">The argument list to modify</param>
        /// <param name="quality">Quality percentage (0-100)</param>
        private static void AddCodecSpecificOptions(List<string> args, int quality)
        {
            if (quality >= 80)
            {
                // Use HEVC (x265) specific optimizations
                args.AddRange(new[] { "-x265-params", "log-level=error" });
            }
            
            // Add general optimization for MKV container
            if (quality >= 70)
            {
                args.AddRange(new[] { "-tune", "animation" });
            }
        }
    }
}