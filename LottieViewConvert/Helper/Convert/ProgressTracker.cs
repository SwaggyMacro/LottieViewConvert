using System;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// progress tracker for conversion tasks.
    /// </summary>
    public class ProgressTracker : IProgressTracker
    {
        public event Action<ConversionProgressEventArgs>? DetailedProgressUpdated;
        public event Action<TimeSpan>? ProgressUpdated;

        public void ReportProgress(ConversionProgressEventArgs progress)
        {
            try
            {
                DetailedProgressUpdated?.Invoke(progress);

                // trigger ffmpeg progress 
                if (progress.FfmpegProgress.HasValue)
                {
                    ProgressUpdated?.Invoke(progress.FfmpegProgress.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Progress callback threw exception: {ex.Message}");
            }
        }
    }
}