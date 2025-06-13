using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;

namespace LottieViewConvert.Services.Dependency
{
    public class FFmpegService : BaseExecutableToolService
    {
        public FFmpegService() : base(CreateFFmpegConfig())
        {
        }

        private static ExecutableToolConfig CreateFFmpegConfig()
        {
            return new ExecutableToolConfig
            {
                ToolName = "FFmpeg",
                ExecutableName = "ffmpeg",
                LocalFolderName = "ffmpeg",
                VersionArgument = "-version",
                WindowsCommonPaths = new List<string>
                {
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe")
                },
                MacOSCommonPaths = new List<string>
                {
                    "/usr/local/bin/ffmpeg",
                    "/opt/homebrew/bin/ffmpeg",
                    "/usr/bin/ffmpeg",
                    "/Applications/ffmpeg",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "ffmpeg")
                },
                LinuxCommonPaths = new List<string>
                {
                    "/usr/bin/ffmpeg",
                    "/usr/local/bin/ffmpeg",
                    "/snap/bin/ffmpeg",
                    "/opt/ffmpeg/bin/ffmpeg",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "ffmpeg"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "bin", "ffmpeg")
                },
                PathComment = "FFmpeg"
            };
        }

        /// <summary>
        /// 1st via system PATH, 2nd via common locations, 3rd via local installation
        /// </summary>
        public async Task<string?> DetectFFmpegPathAsync()
        {
            return await DetectToolPathAsync();
        }

        /// <summary>
        /// Get FFmpeg path from system PATH using 'which' or 'where' command
        /// </summary>
        public async Task<string?> GetFFmpegFromSystemPathAsync()
        {
            return await GetToolFromSystemPathAsync();
        }

        /// <summary>
        /// Get the local FFmpeg executable path
        /// </summary>
        public string GetLocalFFmpegPath()
        {
            return GetLocalToolPath();
        }

        /// <summary>
        /// Get the local FFmpeg directory path
        /// </summary>
        public string GetLocalFFmpegDirectory()
        {
            return GetLocalToolDirectory();
        }

        /// <summary>
        /// Is the specified FFmpeg path valid
        /// </summary>
        public async Task<bool> IsFFmpegValidAsync(string? ffmpegPath = null)
        {
            return await IsToolValidAsync(ffmpegPath);
        }

        /// <summary>
        /// Get the FFmpeg version from the specified path
        /// </summary>
        public async Task<string> GetFFmpegVersionAsync(string? ffmpegPath = null)
        {
            return await GetToolVersionAsync(ffmpegPath);
        }

        /// <summary>
        /// Get the source of the specified FFmpeg path
        /// </summary>
        public async Task<string> GetFFmpegSourceAsync(string ffmpegPath)
        {
            return await GetToolSourceAsync(ffmpegPath);
        }

        /// <summary>
        /// check if the local FFmpeg directory is in the PATH environment variable
        /// </summary>
        public bool IsLocalFFmpegInPath()
        {
            return IsLocalToolInPath();
        }

        public override async Task<(bool Success, string Message)> DownloadAndInstallToolAsync(
            IProgress<double>? progress = null, bool addToPath = true)
        {
            return await DownloadAndInstallFFmpegAsync(progress, addToPath);
        }

        public async Task<(bool Success, string Message)> DownloadAndInstallFFmpegAsync(
            IProgress<double>? progress = null, bool addToPath = true)
        {
            try
            {
                var downloadInfo = GetDownloadInfo();
                if (downloadInfo == null)
                    return (false, Resources.UnsupportedPlatform);

                var result = await _downloadService.DownloadAndInstallAsync(
                    downloadInfo,
                    _toolFolderPath,
                    _config.ToolName,
                    async (tempFilePath) => await ExtractFFmpegAsync(tempFilePath, downloadInfo.IsZip),
                    async (localPath) => await IsFFmpegValidAsync(localPath),
                    progress);

                if (!result.Success)
                    return result;

                // Verify installation
                var localPath = GetLocalFFmpegPath();
                var isValid = await IsFFmpegValidAsync(localPath);
                if (!isValid)
                    return (false, $"{Resources.InstallationFailed} - {Resources.FFmpegNotWorking}");

                progress?.Report(90);

                string pathMessage = "";
                if (addToPath)
                {
                    Logger.Info("Adding FFmpeg to PATH environment variable...");
                    var pathResult = await AddToPathAsync();
                    if (pathResult.Success)
                    {
                        pathMessage = $" {Resources.FFmpegHasBeenAddedToYourPathEnvironmentVariable}.";
                    }
                    else
                    {
                        pathMessage = $" {Resources.Warning}: {Resources.CouldNotAddToPATH} - {pathResult.Message}";
                        Logger.Warn($"Failed to add FFmpeg to PATH: {pathResult.Message}");
                    }
                }

                progress?.Report(100);
                Logger.Info("FFmpeg installation completed successfully");
                return (true, $"FFmpeg {Resources.InstalledSuccessfully}!{pathMessage}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download and install FFmpeg: {ex.Message}");
                return (false, $"{Resources.InstallationFailed}: {ex.Message}");
            }
        }

        private async Task ExtractFFmpegAsync(string archivePath, bool isZip)
        {
            try
            {
                if (isZip)
                {
                    using var archive = ZipFile.OpenRead(archivePath);
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.Contains("ffmpeg") && (entry.Name.EndsWith(".exe") || !entry.Name.Contains(".")))
                        {
                            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? "ffmpeg.exe"
                                : "ffmpeg";
                            var destinationPath = Path.Combine(_toolFolderPath, executableName);

                            entry.ExtractToFile(destinationPath, true);
                            await _downloadService.SetExecutablePermissionsAsync(destinationPath);
                            break;
                        }
                    }
                }
                else
                {
                    // Handle tar.gz files for Linux/macOS
                    var extractProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "tar",
                            Arguments = $"-xzf \"{archivePath}\" -C \"{_toolFolderPath}\" --strip-components=1 --wildcards \"*/ffmpeg\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    extractProcess.Start();
                    await extractProcess.WaitForExitAsync();

                    if (extractProcess.ExitCode != 0)
                        throw new Exception("Failed to extract FFmpeg archive");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to extract FFmpeg: {ex.Message}");
                throw;
            }
        }

        private ToolDownloadInfo? GetDownloadInfo()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ToolDownloadInfo
                {
                    Url = "https://github.com/SwaggyMacro/LottieViewConvert/releases/download/v1.1.0/ffmpeg-master-latest-win64-gpl.zip",
                    FileName = "ffmpeg-windows.zip",
                    IsZip = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new ToolDownloadInfo
                {
                    Url = "https://evermeet.cx/ffmpeg/getrelease/zip",
                    FileName = RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 
                        ? "ffmpeg-macos-arm64.zip" 
                        : "ffmpeg-macos-x64.zip",
                    IsZip = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var architecture = RuntimeInformation.ProcessArchitecture;
                return new ToolDownloadInfo
                {
                    Url = architecture == System.Runtime.InteropServices.Architecture.Arm64
                        ? "https://github.com/SwaggyMacro/LottieViewConvert/releases/download/v1.1.0/ffmpeg-master-latest-linuxarm64-gpl.tar.xz"
                        : "https://github.com/SwaggyMacro/LottieViewConvert/releases/download/v1.1.0/ffmpeg-master-latest-linux64-gpl.tar.xz",
                    FileName = architecture == System.Runtime.InteropServices.Architecture.Arm64
                        ? "ffmpeg-linux-arm64.tar.xz"
                        : "ffmpeg-linux-x64.tar.xz",
                    IsZip = false
                };
            }

            return null;
        }
    }

    public class FFmpegDownloadInfo
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsZip { get; set; }
    }
}