using System;
using System.Threading;
using Lottie;

namespace LottieViewConvert.Interface.Convert
{
    /// <summary>
    /// frame exporter interface for exporting PNG sequences from Lottie animations.
    /// </summary>
    public interface IFrameExporter
    {
        void ExportPngSequence(
            string source,
            string outputDirectory,
            int fps,
            double playSpeed,
            int outputWidth,
            int outputHeight,
            IProgress<ExportProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default);
    }
}