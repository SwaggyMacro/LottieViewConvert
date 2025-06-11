using System;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Helper.Convert;

namespace LottieViewConvert.Interface.Convert
{
    /// <summary>
    /// format converter interface
    /// </summary>
    public interface IFormatConverter
    {
        string FormatName { get; }
        string FileExtension { get; }
        
        Task<bool> ConvertAsync(
            string inputDirectory,
            string outputPath,
            ConversionOptions options,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default);
    }
}