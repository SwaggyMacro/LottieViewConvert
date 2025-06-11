using System;
using System.Diagnostics.CodeAnalysis;
using Lottie;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// Conversion progress event arguments.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class ConversionProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Current conversion stage
        /// </summary>
        public ConversionStage Stage { get; }

        /// <summary>
        /// Current stage description
        /// </summary>
        public string StageDescription { get; }

        /// <summary>
        /// overall progress percentage (0-100)
        /// </summary>
        public double OverallProgress { get; }

        /// <summary>
        /// current stage progress percentage (0-100)
        /// </summary>
        public double StageProgress { get; }

        /// <summary>
        /// png export progress (only valid during PngExport stage)
        /// </summary>
        public ExportProgressEventArgs? PngProgress { get; }

        /// <summary>
        /// FFmpeg conversion progress (only valid during Converting stage)
        /// </summary>
        public TimeSpan? FfmpegProgress { get; }

        /// <summary>
        /// Gifski conversion progress (only valid during Converting stage)
        /// </summary>
        public double? GifskiProgress { get; }

        /// <summary>
        /// elapsed time since conversion started
        /// </summary>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// Estimated time remaining for the conversion to complete.
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; }

        public ConversionProgressEventArgs(
            ConversionStage stage,
            string stageDescription,
            double overallProgress,
            double stageProgress,
            TimeSpan elapsed,
            ExportProgressEventArgs? pngProgress = null,
            TimeSpan? ffmpegProgress = null,
            double? gifskiProgress = null)
        {
            Stage = stage;
            StageDescription = stageDescription;
            OverallProgress = Math.Max(0, Math.Min(100, overallProgress));
            StageProgress = Math.Max(0, Math.Min(100, stageProgress));
            Elapsed = elapsed;
            PngProgress = pngProgress;
            FfmpegProgress = ffmpegProgress;
            GifskiProgress = gifskiProgress;

            // a simple estimation of remaining time
            if (overallProgress > 0 && overallProgress < 100)
            {
                var totalEstimatedTime = elapsed.TotalSeconds / (overallProgress / 100.0);
                var remainingSeconds = totalEstimatedTime - elapsed.TotalSeconds;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
            }
        }
    }
}