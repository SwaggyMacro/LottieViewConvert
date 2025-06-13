using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Models;

namespace LottieViewConvert.Services.Dependency
{
    /// <summary>
    /// Service for detecting and validating executable files
    /// </summary>
    public class ExecutableDetectionService
    {
        /// <summary>
        /// Get executable path from system PATH using 'which' or 'where' command
        /// </summary>
        public async Task<string?> GetExecutableFromSystemPathAsync(string executableName)
        {
            try
            {
                var findCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = findCommand,
                        Arguments = executableName,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var executablePath = output.Trim().Split('\n')[0].Trim();
                    if (File.Exists(executablePath))
                    {
                        return executablePath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to find {executableName} in system PATH: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get executable path from common locations
        /// </summary>
        public string? GetExecutableFromCommonLocations(ExecutableToolConfig config)
        {
            var commonPaths = GetCommonPaths(config);

            foreach (var path in commonPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        Logger.Debug($"Checking {config.ToolName} at: {path}");
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to check path {path}: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Validate if the executable is working
        /// </summary>
        public async Task<bool> IsExecutableValidAsync(string executablePath, string versionArgument)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                    return false;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = versionArgument,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to validate executable at {executablePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get executable version
        /// </summary>
        public async Task<string> GetExecutableVersionAsync(string executablePath, string versionArgument, string toolName)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                    return "Not Found";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = versionArgument,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return ParseVersionFromOutput(output, toolName);
                }

                return "Unknown";
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get {toolName} version: {ex.Message}");
                return "Error";
            }
        }

        private string ParseVersionFromOutput(string output, string toolName)
        {
            var lines = output.Split('\n');
            var versionLine = lines[0];

            if (toolName.ToLower() == "ffmpeg")
            {
                if (versionLine.Contains("ffmpeg version"))
                {
                    var versionStart = versionLine.IndexOf("ffmpeg version ", StringComparison.Ordinal) + "ffmpeg version ".Length;
                    var versionEnd = versionLine.IndexOf(' ', versionStart);
                    if (versionEnd == -1) versionEnd = versionLine.Length;
                    return versionLine.Substring(versionStart, versionEnd - versionStart);
                }
            }
            else if (toolName.ToLower() == "gifski")
            {
                var parts = output.Trim().Split(' ');
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }

            return output.Trim();
        }

        private List<string> GetCommonPaths(ExecutableToolConfig config)
        {
            var paths = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                paths.AddRange(config.WindowsCommonPaths);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                paths.AddRange(config.MacOSCommonPaths);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                paths.AddRange(config.LinuxCommonPaths);
            }

            return paths;
        }
    }
}