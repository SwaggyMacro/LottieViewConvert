using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// APNG format converter using FFmpeg.
    /// </summary>
    public class ApngConverter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;

        public ApngConverter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "APNG";
        public string FileExtension => "apng";

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
                "-f", "apng",
                "-plays", "0", // 0 means infinite loop
                "-progress", "pipe:1",
                "-nostats"
            };

            // Add quality-based options if supported
            AddQualityOptions(args, options.Quality);

            args.Add(outputPath);

            return await _commandExecutor.ExecuteAsync("ffmpeg", args, inputDirectory, progress, cancellationToken);
        }

        /// <summary>
        /// Adds quality-based options to the FFmpeg arguments.
        /// Uses only well-supported APNG encoder options.
        /// </summary>
        /// <param name="args">The argument list to modify</param>
        /// <param name="quality">Quality percentage (0-100)</param>
        private static void AddQualityOptions(List<string> args, int quality)
        {
            // For APNG, we can mainly control the output through filter options
            // Since direct compression_level and pred might not be supported,
            // we'll use alternative approaches
            
            if (quality < 80)
            {
                // For lower quality, we can add a scale filter to reduce resolution slightly
                // or use other filters to optimize file size
                var scale = GetScaleFactor(quality);
                if (scale < 1.0)
                {
                    args.AddRange(new[] { "-vf", $"scale=iw*{scale:F2}:ih*{scale:F2}:flags=lanczos" });
                }
            }
        }

        /// <summary>
        /// Gets the scale factor based on quality.
        /// Lower quality may use slight downscaling to reduce file size.
        /// </summary>
        /// <param name="quality">Quality percentage (0-100)</param>
        /// <returns>Scale factor (0.5-1.0)</returns>
        private static double GetScaleFactor(int quality)
        {
            return quality switch
            {
                >= 80 => 1.0,    // No scaling
                >= 60 => 0.95,   // Slight reduction
                >= 40 => 0.90,   // Moderate reduction
                >= 20 => 0.85,   // More reduction
                _ => 0.80        // Maximum reduction
            };
        }
    }
}