using System;
using LottieViewConvert.Helper.Convert;

namespace LottieViewConvert.Interface.Convert
{
    /// <summary>
    /// Interface for tracking conversion progress.
    /// </summary>
    public interface IProgressTracker
    {
        void ReportProgress(ConversionProgressEventArgs progress);
        event Action<ConversionProgressEventArgs>? DetailedProgressUpdated;
        event Action<TimeSpan>? ProgressUpdated;
    }
}