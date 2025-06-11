using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Interface.Convert;

namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    ///  CommandExecutor class for executing external commands.
    /// </summary>
    public class CommandExecutor : ICommandExecutor
    {
        private readonly Regex _timeRegex = new(@"^(?<hh>\d+):(?<mm>\d+):(?<ss>\d+)\.(?<frac>\d+)$", RegexOptions.Compiled);
        private readonly Regex _gifskiProgressRegex = new(@"Frame\s+(?<current>\d+)\s*/\s*(?<total>\d+)", RegexOptions.Compiled);

        public async Task<bool> ExecuteAsync(
            string command,
            IEnumerable<string> arguments,
            string workingDirectory,
            IProgress<TimeSpan>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var progressData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var cmd = Cli.Wrap(command)
                    .WithArguments(arguments)
                    .WithWorkingDirectory(workingDirectory)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                    {
                        ProcessProgressLine(line, progressData, progress, command);
                    }))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                    {
                        ProcessProgressLine(line, progressData, progress, command);
                        Logger.Debug($"{command} stderr: {line}");
                    }));

                var result = await cmd.ExecuteAsync(cancellationToken: cancellationToken);

                if (result.ExitCode != 0)
                {
                    Logger.Error($"{command} exited with code {result.ExitCode}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"{command} execution failed: {ex.Message}");
                return false;
            }
        }

        private void ProcessProgressLine(
            string line,
            Dictionary<string, string> progressData,
            IProgress<TimeSpan>? progress,
            string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    progressData.Clear();
                    return;
                }

                // handle different command formats
                if (command.Contains("gifski", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessGifskiProgress(line, progress);
                }
                else
                {
                    ProcessFFmpegProgress(line, progressData, progress);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to parse progress line: '{line}', {ex.Message}");
            }
        }

        private void ProcessGifskiProgress(string line, IProgress<TimeSpan>? progress)
        {
            // gifski format "5.9MB GIF; Frame 170 / 270 #######################################_....... 1s"
            var match = _gifskiProgressRegex.Match(line);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["current"].Value, out var current) &&
                    int.TryParse(match.Groups["total"].Value, out var total) &&
                    total > 0)
                {
                    var percentage = (double)current / total * 100;
                    Logger.Debug($"Gifski progress: {current}/{total} frames ({percentage:F1}%)");
                    
                    var progressTime = TimeSpan.FromMilliseconds(percentage * 1000);
                    progress?.Report(progressTime);
                }
            }
        }

        private void ProcessFFmpegProgress(
            string line,
            Dictionary<string, string> progressData,
            IProgress<TimeSpan>? progress)
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();
            progressData[key] = value;

            if (key.Equals("out_time", StringComparison.OrdinalIgnoreCase))
            {
                var timeSpan = ParseTimeSpan(value);
                if (timeSpan.HasValue)
                {
                    progress?.Report(timeSpan.Value);
                }
            }
        }

        private TimeSpan? ParseTimeSpan(string timeString)
        {
            var match = _timeRegex.Match(timeString);
            if (!match.Success) return null;

            if (int.TryParse(match.Groups["hh"].Value, out var hh) &&
                int.TryParse(match.Groups["mm"].Value, out var mm) &&
                int.TryParse(match.Groups["ss"].Value, out var ss) &&
                int.TryParse(match.Groups["frac"].Value, out _))
            {
                var fracStr = match.Groups["frac"].Value;
                int ms = 0;
                if (fracStr.Length >= 3)
                    ms = int.Parse(fracStr.Substring(0, 3));
                else if (fracStr.Length > 0)
                    ms = int.Parse(fracStr.PadRight(3, '0'));

                return new TimeSpan(0, hh, mm, ss, ms);
            }

            return null;
        }
    }
}