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
                var durationSec = animation.Duration.TotalSeconds;

                // calculate the actual output duration based on playback speed
                var outputDurationSec = durationSec / playbackSpeed;

                // calculate the fps via the output duration
                var frameCount = (int)Math.Ceiling(outputDurationSec * fps);

                var baseWidth = (int)animation.Size.Width;
                var baseHeight = (int)animation.Size.Height;
                var width = outputWidth ?? baseWidth;
                var height = outputHeight ?? baseHeight;
                var info = new SKImageInfo(width, height);

                // Report initial progress
                progressCallback?.Invoke(new ExportProgressEventArgs(0, frameCount, "", TimeSpan.Zero));

                for (var i = 0; i < frameCount; i++)
                {
                    // calculate the output time for this frame
                    var outputTime = i / (double)fps;

                    // time for this frame adjusted by playback speed
                    var animationTime = outputTime * playbackSpeed;

                    // ensure dont exceed the animation duration
                    animationTime = Math.Min(animationTime, durationSec);

                    animation.SeekFrameTime(animationTime);

                    using var surface = SKSurface.Create(info);
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.Transparent);

                    // Scale if the output size differs
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
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to export PNG sequence from {lottiePath}", e);
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

        /// <summary>
        /// Exports specific frames of a Lottie animation to individual PNG files.
        /// </summary>
        /// <param name="lottiePath">Path or URI to the Lottie JSON (or .json.gz) file.</param>
        /// <param name="outputDirectory">Directory where PNG frames will be written. Will be created if it does not exist.</param>
        /// <param name="frameIndices">Array of frame indices to export (0-based).</param>
        /// <param name="fps">Frames per second to sample. Defaults to 30.</param>
        /// <param name="playbackSpeed">Speed multiplier for playback (e.g. 2.0 = twice as fast). Defaults to 1.0.</param>
        /// <param name="outputWidth">
        /// Desired output width in pixels. If null, uses the animation's intrinsic width.
        /// </param>
        /// <param name="outputHeight">
        /// Desired output height in pixels. If null, uses the animation's intrinsic height.
        /// </param>
        /// <param name="progressCallback">Optional callback to receive progress updates during export.</param>
        public static void ExportSpecificFrames(
            string lottiePath,
            string outputDirectory,
            int[] frameIndices,
            int fps = 30,
            double playbackSpeed = 1.0,
            int? outputWidth = null,
            int? outputHeight = null,
            Action<ExportProgressEventArgs> progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(lottiePath))
                throw new ArgumentException("Lottie path must not be null or empty", nameof(lottiePath));
            if (frameIndices == null || frameIndices.Length == 0)
                throw new ArgumentException("Frame indices must not be null or empty", nameof(frameIndices));
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
                var durationSec = animation.Duration.TotalSeconds;
                var outputDurationSec = durationSec / playbackSpeed;
                var totalFrameCount = (int)Math.Ceiling(outputDurationSec * fps);

                // Validate frame indices
                var invalidIndices = frameIndices.Where(i => i < 0 || i >= totalFrameCount).ToArray();
                if (invalidIndices.Any())
                {
                    throw new ArgumentException(
                        $"Invalid frame indices: {string.Join(", ", invalidIndices)}. " +
                        $"Valid range is 0-{totalFrameCount - 1}.", 
                        nameof(frameIndices));
                }

                var baseWidth = (int)animation.Size.Width;
                var baseHeight = (int)animation.Size.Height;
                var width = outputWidth ?? baseWidth;
                var height = outputHeight ?? baseHeight;
                var info = new SKImageInfo(width, height);

                // Report initial progress
                progressCallback?.Invoke(new ExportProgressEventArgs(0, frameIndices.Length, "", TimeSpan.Zero));

                for (var i = 0; i < frameIndices.Length; i++)
                {
                    var frameIndex = frameIndices[i];
                    
                    // calculate the output time for this frame
                    var outputTime = frameIndex / (double)fps;
                    
                    // time for this frame adjusted by playback speed
                    var animationTime = outputTime * playbackSpeed;
                    
                    // ensure don't exceed the animation duration
                    animationTime = Math.Min(animationTime, durationSec);

                    animation.SeekFrameTime(animationTime);

                    using var surface = SKSurface.Create(info);
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.Transparent);

                    // Scale if the output size differs
                    if (width != baseWidth || height != baseHeight)
                    {
                        var scaleX = width / (float)baseWidth;
                        var scaleY = height / (float)baseHeight;
                        canvas.Scale(scaleX, scaleY);
                    }

                    animation.Render(canvas, new SKRect(0, 0, baseWidth, baseHeight));

                    using var image = surface.Snapshot();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                    var filename = Path.Combine(outputDirectory, $"frame_{frameIndex:D5}.png");
                    using var fs = File.OpenWrite(filename);
                    data.SaveTo(fs);

                    // Report progress after each frame
                    var elapsed = DateTime.UtcNow - startTime;
                    var progress = new ExportProgressEventArgs(i + 1, frameIndices.Length, Path.GetFileName(filename), elapsed);
                    progressCallback?.Invoke(progress);
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to export specific frames from {lottiePath}", e);
            }
            finally
            {
                animation.Dispose();
            }
        }

        /// <summary>
        /// Exports specific frames of a Lottie animation to individual PNG files with IProgress support.
        /// </summary>
        /// <param name="lottiePath">Path or URI to the Lottie JSON (or .json.gz) file.</param>
        /// <param name="outputDirectory">Directory where PNG frames will be written. Will be created if it does not exist.</param>
        /// <param name="frameIndices">Array of frame indices to export (0-based).</param>
        /// <param name="fps">Frames per second to sample. Defaults to 30.</param>
        /// <param name="playbackSpeed">Speed multiplier for playback (e.g. 2.0 = twice as fast). Defaults to 1.0.</param>
        /// <param name="outputWidth">
        /// Desired output width in pixels. If null, uses the animation's intrinsic width.
        /// </param>
        /// <param name="outputHeight">
        /// Desired output height in pixels. If null, uses the animation's intrinsic height.
        /// </param>
        /// <param name="progress">Optional IProgress implementation to receive progress updates.</param>
        public static void ExportSpecificFrames(
            string lottiePath,
            string outputDirectory,
            int[] frameIndices,
            int fps = 30,
            double playbackSpeed = 1.0,
            int? outputWidth = null,
            int? outputHeight = null,
            IProgress<ExportProgressEventArgs> progress = null)
        {
            ExportSpecificFrames(
                lottiePath,
                outputDirectory,
                frameIndices,
                fps,
                playbackSpeed,
                outputWidth,
                outputHeight,
                progress.Report);
        }

        /// <summary>
        /// Exports a single frame of a Lottie animation to a PNG file.
        /// </summary>
        /// <param name="lottiePath">Path or URI to the Lottie JSON (or .json.gz) file.</param>
        /// <param name="outputPath">Full path for the output PNG file.</param>
        /// <param name="frameIndex">Frame index to export (0-based).</param>
        /// <param name="fps">Frames per second to sample. Defaults to 30.</param>
        /// <param name="playbackSpeed">Speed multiplier for playback (e.g. 2.0 = twice as fast). Defaults to 1.0.</param>
        /// <param name="outputWidth">
        /// Desired output width in pixels. If null, uses the animation's intrinsic width.
        /// </param>
        /// <param name="outputHeight">
        /// Desired output height in pixels. If null, uses the animation's intrinsic height.
        /// </param>
        public static void ExportSingleFrame(
            string lottiePath,
            string outputPath,
            int frameIndex,
            int fps = 30,
            double playbackSpeed = 1.0,
            int? outputWidth = null,
            int? outputHeight = null)
        {
            if (string.IsNullOrWhiteSpace(lottiePath))
                throw new ArgumentException("Lottie path must not be null or empty", nameof(lottiePath));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path must not be null or empty", nameof(outputPath));
            if (frameIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index must be non-negative.");
            if (fps <= 0)
                throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be positive.");
            if (playbackSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(playbackSpeed), "Playback speed must be positive.");

            // Create output directory if it doesn't exist
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using var stream = OpenStream(lottiePath);
            using var skStream = new SKManagedStream(stream);

            if (!Animation.TryCreate(skStream, out var animation))
                throw new InvalidOperationException("Failed to load Lottie animation from " + lottiePath);

            try
            {
                var durationSec = animation.Duration.TotalSeconds;
                var outputDurationSec = durationSec / playbackSpeed;
                var totalFrameCount = (int)Math.Ceiling(outputDurationSec * fps);

                // Validate frame index
                if (frameIndex >= totalFrameCount)
                {
                    throw new ArgumentException(
                        $"Frame index {frameIndex} is out of range. Valid range is 0-{totalFrameCount - 1}.", 
                        nameof(frameIndex));
                }

                var baseWidth = (int)animation.Size.Width;
                var baseHeight = (int)animation.Size.Height;
                var width = outputWidth ?? baseWidth;
                var height = outputHeight ?? baseHeight;
                var info = new SKImageInfo(width, height);

                // calculate the output time for this frame
                var outputTime = frameIndex / (double)fps;
                
                // time for this frame adjusted by playback speed
                var animationTime = outputTime * playbackSpeed;
                
                // ensure don't exceed the animation duration
                animationTime = Math.Min(animationTime, durationSec);

                animation.SeekFrameTime(animationTime);

                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                // Scale if the output size differs
                if (width != baseWidth || height != baseHeight)
                {
                    var scaleX = width / (float)baseWidth;
                    var scaleY = height / (float)baseHeight;
                    canvas.Scale(scaleX, scaleY);
                }

                animation.Render(canvas, new SKRect(0, 0, baseWidth, baseHeight));

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                using var fs = File.OpenWrite(outputPath);
                data.SaveTo(fs);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to export frame {frameIndex} from {lottiePath}", e);
            }
            finally
            {
                animation.Dispose();
            }
        }

        private static Stream OpenStream(string path)
        {
            Stream rawStream;
            if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri)
                && uri is { IsAbsoluteUri: true, IsFile: true })
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

            if (read != 2 || header[0] != 0x1F || header[1] != 0x8B) return rawStream;
            
            using var gzip = new GZipStream(rawStream, CompressionMode.Decompress, leaveOpen: false);
            var ms = new MemoryStream();
            gzip.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}