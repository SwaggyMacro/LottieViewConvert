using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// GIF format converter using Gifski.
    /// </summary>
    public class GifConverter : IFormatConverter
    {
        private readonly ICommandExecutor _commandExecutor;
        private readonly Regex _progressRegex = new(@"Frame (\d+)/(\d+)", RegexOptions.Compiled);

        public GifConverter(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        public string FormatName => "GIF";
        public string FileExtension => "gif";

        public async Task<bool> ConvertAsync(
            string inputDirectory,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "--fps", options.Fps.ToString(),
                "--width", options.Width.ToString(),
                "--height", options.Height.ToString(),
                "--quality", GetGifskiQuality(options.Quality).ToString(),
                "--output", outputPath,
                Path.Combine(inputDirectory, "*.png")
            };

            if (options.Quality < 70)
            {
                args.Insert(0, "--fast");
            }

            return await _commandExecutor.ExecuteAsync("gifski", args, inputDirectory, progress, cancellationToken);
        }

        private static int GetGifskiQuality(int quality)
        {
            return Math.Max(1, Math.Min(100, quality));
        }
    }
}