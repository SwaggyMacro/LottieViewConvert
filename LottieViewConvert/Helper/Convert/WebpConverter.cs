using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// WebP format converter using ImageMagick.
    /// </summary>
    public class WebpConverter : IFormatConverter
    {
        public string FormatName => "WebP";
        public string FileExtension => "webp";

        public async Task<bool> ConvertAsync(
            string inputDirectory,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var pngFiles = Directory.GetFiles(inputDirectory, "*.png")
                        .OrderBy(f => f)
                        .ToArray();

                    if (pngFiles.Length == 0)
                    {
                        Logger.Error("png files not found in the input directory.");
                        return false;
                    }

                    CreateAnimatedWebP(pngFiles, outputPath, options, progress, cancellationToken);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    Logger.Warn("WebP conversion was cancelled.");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.Error($"WebP conversion failed: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        private void CreateAnimatedWebP(
            string[] pngFiles,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress,
            CancellationToken cancellationToken)
        {
            using var collection = new MagickImageCollection();
            var totalFrames = pngFiles.Length;

            for (int i = 0; i < pngFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var image = new MagickImage(pngFiles[i]);
                image.AnimationDelay = (uint)(100.0 / options.Fps);
                image.Quality = (uint)(options.Quality >= 100 ? 99 : options.Quality);
                collection.Add(image.Clone());

                if (progress != null && totalFrames > 0)
                {
                    var currentTime = TimeSpan.FromSeconds((i + 1) / (double)options.Fps);
                    progress.Report(currentTime);
                }

                if (i % 10 == 0)
                {
                    Thread.Sleep(1);
                }
            }

            collection.Write(outputPath);
            Logger.Debug($"WebP animation created successfully at {outputPath}");
        }
    }
}