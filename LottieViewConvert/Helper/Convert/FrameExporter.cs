using System;
using System.Threading;
using Lottie;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// PNG sequence exporter for Lottie animations.
    /// </summary>
    public class FrameExporter : IFrameExporter
    {
        public void ExportPngSequence(
            string source,
            string outputDirectory,
            int fps,
            double playSpeed,
            int outputWidth,
            int outputHeight,
            IProgress<ExportProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            PngExporter.ExportPngSequence(
                source,
                outputDirectory,
                fps,
                playSpeed,
                outputWidth,
                outputHeight,
                progress);
        }
    }
}