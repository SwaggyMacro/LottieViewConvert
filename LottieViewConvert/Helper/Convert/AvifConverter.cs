using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// AVIF format converter using FFmpeg with proper transparency support.
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
            try
            {
                // First pass
                var firstPassArgs = GetEncodingArgs(options, outputPath, 1);
                bool firstPassSuccess = await _commandExecutor.ExecuteAsync(
                    "ffmpeg", 
                    firstPassArgs, 
                    inputDirectory, 
                    progress, 
                    cancellationToken);

                if (!firstPassSuccess)
                    throw new Exception("First pass AVIF encoding failed");

                // Second pass
                var secondPassArgs = GetEncodingArgs(options, outputPath, 2);
                bool secondPassSuccess = await _commandExecutor.ExecuteAsync(
                    "ffmpeg", 
                    secondPassArgs, 
                    inputDirectory, 
                    progress, 
                    cancellationToken);
                
                if (!secondPassSuccess)
                    throw new Exception("Second pass AVIF encoding failed");
                
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while converting AVIF", ex);
            }
        }

        private List<string> GetEncodingArgs(
            ConversionOptions options, 
            string outputPath, 
            int passNumber)
        {
            // Get quality parameters
            int crf = GetCrfValue(options.Quality);
            int crfAlpha = Math.Min(crf + 20, 63); // Higher CRF for alpha (less quality needed for transparency)
            int cpuUsed = GetCpuUsed(options.Quality);

            var args = new List<string>
            {
                "-hide_banner",
                "-y",
                "-r", options.Fps.ToString(),
                "-i", "%05d.png",
                "-color_range", "tv",
                "-pix_fmt:0", "yuv420p",
                "-pix_fmt:1", "gray8",
                
                // Key part: proper alpha channel handling
                "-filter_complex", 
                "[0:v]format=pix_fmts=yuva444p[main]; [main]split[main][alpha]; [alpha]alphaextract[alpha]",
                
                // Map the main video and alpha channels separately
                "-map", "[main]:v",
                "-map", "[alpha]:v",
                
                // No audio
                "-an",
                
                // AV1 codec settings
                "-c:v", "libaom-av1",
                "-cpu-used", cpuUsed.ToString(),
                "-crf", crf.ToString(),
                "-crf:1", crfAlpha.ToString(),
                
                // Two-pass encoding
                "-pass", passNumber.ToString(),
                
                // Add progress reporting for the progress bar
                "-progress", "pipe:1",
                "-nostats"
            };
            
            // Add quality-specific optimizations
            AddQualityOptimizations(args, options.Quality);
            
            // For first pass, output to null
            if (passNumber == 1)
            {
                args.Add("-f");
                args.Add("null");
                args.Add(Environment.OSVersion.Platform == PlatformID.Win32NT ? "NUL" : "/dev/null");
            }
            else
            {
                args.Add(outputPath);
            }
            
            return args;
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
        /// Adds quality-specific optimization parameters.
        /// </summary>
        /// <param name="args">The argument list to modify</param>
        /// <param name="quality">Quality percentage (0-100)</param>
        private static void AddQualityOptimizations(List<string> args, int quality)
        {
            if (quality >= 80)
            {
                // High quality optimizations
                args.AddRange([
                    "-aom-params", "enable-chroma-deltaq=1:enable-qm=1:qm-min=0:qm-max=15"
                ]);
            }
            else if (quality >= 60)
            {
                // Medium quality optimizations
                args.AddRange([
                    "-aom-params", "enable-chroma-deltaq=1"
                ]);
            }
            else if (quality < 40)
            {
                // Low quality - prioritize speed
                args.AddRange([
                    "-aom-params", "enable-cdef=0:enable-restoration=0"
                ]);
            }

            // Animation-specific tuning
            if (quality >= 70)
            {
                args.AddRange(["-tune", "psnr"]);
            }
        }
    }
}