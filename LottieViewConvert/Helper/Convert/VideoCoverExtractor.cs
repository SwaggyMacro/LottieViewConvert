using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// Extracts a single cover frame from a video file using FFmpeg.
    /// </summary>
    public class VideoCoverExtractor
    {
        private readonly ICommandExecutor _commandExecutor;

        public VideoCoverExtractor(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        /// <summary>
        /// Extracts the first frame of the video to the specified output file.
        /// </summary>
        /// <param name="inputFile">Full path to the video file.</param>
        /// <param name="outputFilePath">Full path where the PNG frame will be saved.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if extraction succeeded; otherwise false.</returns>
        public async Task<bool> ExtractCoverFrameAsync(
            string inputFile,
            string outputFilePath,
            CancellationToken cancellationToken = default)
        {
            var args = new List<string>
            {
                "-hide_banner",
                "-y",
                "-vcodec", "libvpx-vp9", // webm codec
                "-i", inputFile,
                "-frames:v", "1",
                "-c:v", "png",
                "-vf", "format=yuva420p",
                "-pix_fmt", "rgba",
                outputFilePath
            };
            var workingDir = Path.GetDirectoryName(inputFile) ?? string.Empty;
            return await _commandExecutor.ExecuteAsync(
                "ffmpeg",
                args,
                workingDir,
                null,
                cancellationToken);
        }
    }
}




