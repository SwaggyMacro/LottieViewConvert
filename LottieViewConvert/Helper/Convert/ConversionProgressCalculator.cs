using System;
using Lottie;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// A class to calculate and report the progress of the conversion process.
    /// </summary>
    public class ConversionProgressCalculator
    {
        private readonly DateTime _startTime;
        private readonly double _pngExportWeight = 0.65; // PNG export is 65% of the total progress
        private readonly double _conversionWeight = 0.25; // Conversion is 25% of the total progress
        private readonly double _initWeight = 0.05; // Initialization is 5% of the total progress
        private readonly double _cleanupWeight = 0.05; // Cleanup is 5% of the total progress

        public ConversionProgressCalculator()
        {
            _startTime = DateTime.UtcNow;
        }

        public ConversionProgressEventArgs CalculateProgress(
            ConversionStage stage,
            string description,
            double stageProgress,
            ExportProgressEventArgs? pngProgress = null,
            TimeSpan? ffmpegProgress = null,
            double? gifskiProgress = null)
        {
            var overallProgress = stage switch
            {
                ConversionStage.Initializing => _initWeight * stageProgress / 100.0,
                ConversionStage.PngExport => _initWeight * 100 + _pngExportWeight * stageProgress,
                ConversionStage.Converting => (_initWeight + _pngExportWeight) * 100 + _conversionWeight * stageProgress,
                ConversionStage.Cleanup => (_initWeight + _pngExportWeight + _conversionWeight) * 100 + _cleanupWeight * stageProgress,
                ConversionStage.Completed => 100,
                _ => 0
            };

            var elapsed = DateTime.UtcNow - _startTime;

            return new ConversionProgressEventArgs(
                stage,
                description,
                overallProgress,
                stageProgress,
                elapsed,
                pngProgress,
                ffmpegProgress,
                gifskiProgress);
        }
    }
}