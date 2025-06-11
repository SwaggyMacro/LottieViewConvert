using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace Lottie
{
    /// <summary>
    /// Progress information for PNG export operation.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class ExportProgressEventArgs : EventArgs
    {
        public int CurrentFrame { get; }
        public int TotalFrames { get; }
        public double ProgressPercentage { get; }
        public string CurrentFileName { get; }
        public TimeSpan Elapsed { get; }
        public TimeSpan? EstimatedTimeRemaining { get; }

        public ExportProgressEventArgs(int currentFrame, int totalFrames, string currentFileName, TimeSpan elapsed)
        {
            CurrentFrame = currentFrame;
            TotalFrames = totalFrames;
            ProgressPercentage = totalFrames > 0 ? (double)currentFrame / totalFrames * 100 : 0;
            CurrentFileName = currentFileName;
            Elapsed = elapsed;
            
            if (currentFrame > 0 && elapsed.TotalSeconds > 0)
            {
                var averageTimePerFrame = elapsed.TotalSeconds / currentFrame;
                var remainingFrames = totalFrames - currentFrame;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingFrames * averageTimePerFrame);
            }
        }
    }

    /// <summary>
    /// Utility class to extract every frame of a Lottie animation into PNG files.
    /// </summary>
    public static class PngExporter
    {
        /// <summary>
        /// Exports all frames of a Lottie animation to individual PNG files.
        /// </summary>
        /// <param name="lottiePath">Path or URI to the Lottie JSON (or .json.gz) file.</param>
        /// <param name="outputDirectory">Directory where PNG frames will be written. Will be created if it does not exist.</param>
        /// <param name="fps">Frames per second to sample. Defaults to 30.</param>
        /// <param name="playbackSpeed">Speed multiplier for playback (e.g. 2.0 = twice as fast). Defaults to 1.0.</param>
        /// <param name="outputWidth">
        /// Desired output width in pixels. If null, uses the animation's intrinsic width.
        /// </param>
        /// <param name="outputHeight">
        /// Desired output height in pixels. If null, uses the animation's intrinsic height.
        /// </param>
        /// <param name="progressCallback">Optional callback to receive progress updates during export.</param>
        public static void ExportPngSequence(
            string lottiePath,
            string outputDirectory,
            int fps = 30,
            double playbackSpeed = 1.0,
            int? outputWidth = null,
            int? outputHeight = null,
            Action<ExportProgressEventArgs> progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(lottiePath))
                throw new ArgumentException("Lottie path must not be null or empty", nameof(lottiePath));
            if (fps <= 0)
                throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be positive.");
            if (playbackSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(playbackSpeed), "Playback speed must be positive.");
            
            Directory.CreateDirectory(outputDirectory);

            using var stream = OpenStream(lottiePath);
            using var skStream = new SKManagedStream(stream);

            if (!Animation.TryCreate(skStream, out var animation))
                throw new InvalidOperationException("Failed to load Lottie animation from " + lottiePath);

            var startTime = DateTime.UtcNow;

            try
            {
                var durationSec = animation.Duration;
                
                // 计算实际的输出持续时间（考虑播放速度）
                var outputDurationSec = durationSec.Seconds / playbackSpeed;
                
                // 根据输出持续时间计算帧数
                var frameCount = (int)Math.Ceiling(outputDurationSec * fps);
                
                var baseWidth = (int)animation.Size.Width;
                var baseHeight = (int)animation.Size.Height;
                var width = outputWidth ?? baseWidth;
                var height = outputHeight ?? baseHeight;
                var info = new SKImageInfo(width, height);

                // Report initial progress
                progressCallback.Invoke(new ExportProgressEventArgs(0, frameCount, "", TimeSpan.Zero));

                for (var i = 0; i < frameCount; i++)
                {
                    // calculate the output time for this frame
                    var outputTime = i / (double)fps;
                    
                    // time for this frame adjusted by playback speed
                    var animationTime = outputTime * playbackSpeed;
                    
                    // ensure dont exceed the animation duration
                    animationTime = Math.Min(animationTime, durationSec.Seconds);

                    animation.SeekFrameTime(animationTime);

                    using var surface = SKSurface.Create(info);
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.Transparent);

                    // Scale if output size differs
                    if (width != baseWidth || height != baseHeight)
                    {
                        var scaleX = width / (float)baseWidth;
                        var scaleY = height / (float)baseHeight;
                        canvas.Scale(scaleX, scaleY);
                    }

                    animation.Render(canvas, new SKRect(0, 0, baseWidth, baseHeight));

                    using var image = surface.Snapshot();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                    var filename = Path.Combine(outputDirectory, $"{i:D5}.png");
                    using var fs = File.OpenWrite(filename);
                    data.SaveTo(fs);

                    // Report progress after each frame
                    var elapsed = DateTime.UtcNow - startTime;
                    var progress = new ExportProgressEventArgs(i + 1, frameCount, Path.GetFileName(filename), elapsed);
                    progressCallback?.Invoke(progress);
                }
            }
            finally
            {
                animation.Dispose();
            }
        }

        /// <summary>
        /// Exports all frames of a Lottie animation to individual PNG files with IProgress support.
        /// </summary>
        /// <param name="lottiePath">Path or URI to the Lottie JSON (or .json.gz) file.</param>
        /// <param name="outputDirectory">Directory where PNG frames will be written. Will be created if it does not exist.</param>
        /// <param name="fps">Frames per second to sample. Defaults to 30.</param>
        /// <param name="playbackSpeed">Speed multiplier for playback (e.g. 2.0 = twice as fast). Defaults to 1.0.</param>
        /// <param name="outputWidth">
        /// Desired output width in pixels. If null, uses the animation's intrinsic width.
        /// </param>
        /// <param name="outputHeight">
        /// Desired output height in pixels. If null, uses the animation's intrinsic height.
        /// </param>
        /// <param name="progress">Optional IProgress implementation to receive progress updates.</param>
        public static void ExportPngSequence(
            string lottiePath,
            string outputDirectory,
            int fps = 30,
            double playbackSpeed = 1.0,
            int? outputWidth = null,
            int? outputHeight = null,
            IProgress<ExportProgressEventArgs> progress = null)
        {
            ExportPngSequence(
                lottiePath,
                outputDirectory,
                fps,
                playbackSpeed,
                outputWidth,
                outputHeight,
                progress.Report);
        }

        private static Stream OpenStream(string path)
        {
            Stream rawStream;
            if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri)
                && uri.IsAbsoluteUri && uri.IsFile)
            {
                rawStream = File.OpenRead(uri.LocalPath);
            }
            else
            {
                rawStream = File.OpenRead(path);
            }

            if (!rawStream.CanSeek)
                rawStream = new BufferedStream(rawStream);

            Span<byte> header = stackalloc byte[2];
            var read = rawStream.Read(header);
            rawStream.Seek(-read, SeekOrigin.Current);

            if (read == 2 && header[0] == 0x1F && header[1] == 0x8B)
            {
                using var gzip = new GZipStream(rawStream, CompressionMode.Decompress, leaveOpen: false);
                var ms = new MemoryStream();
                gzip.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }

            return rawStream;
        }
    }
}