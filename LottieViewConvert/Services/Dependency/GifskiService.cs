using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using LottieViewConvert.Models;

namespace LottieViewConvert.Services.Dependency
{
    public class GifskiService : BaseExecutableToolService
    {
        public GifskiService() : base(CreateGifskiConfig())
        {
        }

        private static ExecutableToolConfig CreateGifskiConfig()
        {
            return new ExecutableToolConfig
            {
                ToolName = "Gifski",
                ExecutableName = "gifski",
                LocalFolderName = "gifski",
                VersionArgument = "--version",
                WindowsCommonPaths = new List<string>
                {
                    @"C:\gifski\gifski.exe",
                    @"C:\Program Files\gifski\gifski.exe",
                    @"C:\Program Files (x86)\gifski\gifski.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gifski", "gifski.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "gifski", "gifski.exe")
                },
                MacOSCommonPaths = new List<string>
                {
                    "/usr/local/bin/gifski",
                    "/opt/homebrew/bin/gifski",
                    "/usr/bin/gifski",
                    "/Applications/gifski",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "gifski")
                },
                LinuxCommonPaths = new List<string>
                {
                    "/usr/bin/gifski",
                    "/usr/local/bin/gifski",
                    "/snap/bin/gifski",
                    "/opt/gifski/bin/gifski",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "gifski"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "bin", "gifski")
                },
                PathComment = "Gifski"
            };
        }

        /// <summary>
        /// Detect gifski path: 1st via system PATH, 2nd via common locations, 3rd via local installation
        /// </summary>
        public async Task<string?> DetectGifskiPathAsync()
        {
            return await DetectToolPathAsync();
        }

        /// <summary>
        /// Get gifski path from system PATH using 'which' or 'where' command
        /// </summary>
        public async Task<string?> GetGifskiFromSystemPathAsync()
        {
            return await GetToolFromSystemPathAsync();
        }

        /// <summary>
        /// Get the local gifski executable path
        /// </summary>
        public string GetLocalGifskiPath()
        {
            return GetLocalToolPath();
        }

        /// <summary>
        /// Get the local gifski directory path
        /// </summary>
        public string GetLocalGifskiDirectory()
        {
            return GetLocalToolDirectory();
        }

        /// <summary>
        /// Check if the specified gifski path is valid
        /// </summary>
        public async Task<bool> IsGifskiValidAsync(string? gifskiPath = null)
        {
            return await IsToolValidAsync(gifskiPath);
        }

        /// <summary>
        /// Get the gifski version from the specified path
        /// </summary>
        public async Task<string> GetGifskiVersionAsync(string? gifskiPath = null)
        {
            return await GetToolVersionAsync(gifskiPath);
        }

        /// <summary>
        /// Get the source of the specified gifski path
        /// </summary>
        public async Task<string> GetGifskiSourceAsync(string gifskiPath)
        {
            return await GetToolSourceAsync(gifskiPath);
        }

        /// <summary>
        /// Check if the local gifski directory is in the PATH environment variable
        /// </summary>
        public bool IsLocalGifskiInPath()
        {
            return IsLocalToolInPath();
        }

        public override async Task<(bool Success, string Message)> DownloadAndInstallToolAsync(
            IProgress<double>? progress = null, bool addToPath = true)
        {
            return await DownloadAndInstallGifskiAsync(progress, addToPath);
        }

        /// <summary>
        /// Download and install gifski automatically (x64 only, ARM users need manual installation)
        /// </summary>
        public async Task<(bool Success, string Message)> DownloadAndInstallGifskiAsync(
            IProgress<double>? progress = null, bool addToPath = true)
        {
            try
            {
                // Check if current architecture is supported
                var architecture = RuntimeInformation.ProcessArchitecture;
                if (architecture != System.Runtime.InteropServices.Architecture.X64)
                {
                    return (false, Resources.GifskiAutoInstallOnlySupportsx64PlatformPleaseInstallManually);
                }

                var downloadInfo = GetGifskiDownloadInfo();
                if (downloadInfo == null)
                    return (false, Resources.UnsupportedPlatform);

                var result = await _downloadService.DownloadAndInstallAsync(
                    downloadInfo,
                    _toolFolderPath,
                    _config.ToolName,
                    async (tempFilePath) => await ExtractGifskiAsync(tempFilePath),
                    async (localPath) => await IsGifskiValidAsync(localPath),
                    progress);

                if (!result.Success)
                    return result;

                // Verify installation
                var localPath = GetLocalGifskiPath();
                var isValid = await IsGifskiValidAsync(localPath);
                if (!isValid)
                    return (false, $"{Resources.InstallationFailed} - {Resources.GifskiNotWorking}");

                progress?.Report(90);

                string pathMessage = "";
                if (addToPath)
                {
                    Logger.Info("Adding gifski to PATH environment variable...");
                    var pathResult = await AddToPathAsync();
                    if (pathResult.Success)
                    {
                        pathMessage = $" {Resources.GifskiHasBeenAddedToYourPathEnvironmentVariable}.";
                    }
                    else
                    {
                        pathMessage = $" {Resources.Warning}: {Resources.CouldNotAddToPATH} - {pathResult.Message}";
                        Logger.Warn($"Failed to add gifski to PATH: {pathResult.Message}");
                    }
                }

                progress?.Report(100);
                Logger.Info("Gifski installation completed successfully");
                return (true, $"Gifski {Resources.InstalledSuccessfully}!{pathMessage}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download and install gifski: {ex.Message}");
                return (false, $"{Resources.InstallationFailed}: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract gifski from the downloaded zip file
        /// </summary>
        private async Task ExtractGifskiAsync(string archivePath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(archivePath);
                
                // Determine platform folder name
                string platformFolder;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    platformFolder = "win";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    platformFolder = "mac";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    platformFolder = "linux";
                else
                    throw new PlatformNotSupportedException("Unsupported platform");

                var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gifski.exe" : "gifski";
                var targetPath = $"{platformFolder}/{executableName}";

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var destinationPath = Path.Combine(_toolFolderPath, executableName);
                        entry.ExtractToFile(destinationPath, true);
                        
                        await _downloadService.SetExecutablePermissionsAsync(destinationPath);

                        Logger.Info($"Extracted gifski to: {destinationPath}");
                        return;
                    }
                }

                throw new FileNotFoundException($"Could not find gifski executable for platform '{platformFolder}' in the archive");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to extract gifski: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get download information for gifski based on current platform
        /// </summary>
        private ToolDownloadInfo? GetGifskiDownloadInfo()
        {
            // Only support x64 architecture for auto-installation
            if (RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
                return null;

            return new ToolDownloadInfo
            {
                Url = "https://github.com/SwaggyMacro/LottieViewConvert/releases/download/v1.1.0/gifski-1.32.0.zip",
                FileName = "gifski-1.32.0.zip",
                IsZip = true
            };
        }
    }

    public class GifskiDownloadInfo
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}