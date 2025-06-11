using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// MP4 format converter using FFmpeg.
    /// </summary>
    public class Mp4Converter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;

        public Mp4Converter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "MP4";
        public string FileExtension => "mp4";

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
                "-c:v", "libx264",
                "-crf", GetCrfValue(options.Quality).ToString(),
                "-preset", GetPreset(options.Quality),
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                "-progress", "pipe:1",
                "-nostats",
                outputPath
            };

            return await _commandExecutor.ExecuteAsync("ffmpeg", args, inputDirectory, progress, cancellationToken);
        }

        private static int GetCrfValue(int quality)
        {
            return (int)Math.Round(51 - (quality / 100.0 * 51));
        }

        private static string GetPreset(int quality)
        {
            return quality switch
            {
                >= 90 => "veryslow",
                >= 80 => "slow",
                >= 60 => "medium",
                >= 40 => "fast",
                _ => "veryfast"
            };
        }
    }
}