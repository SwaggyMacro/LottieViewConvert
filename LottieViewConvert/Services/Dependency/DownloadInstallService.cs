using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Models;

namespace LottieViewConvert.Services.Dependency
{
    /// <summary>
    /// Service for downloading and installing tools
    /// </summary>
    public class DownloadInstallService
    {
        private readonly HttpClient _httpClient;

        public DownloadInstallService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Download and install tool
        /// </summary>
        public async Task<(bool Success, string Message)> DownloadAndInstallAsync(
            ToolDownloadInfo downloadInfo,
            string installDirectory,
            string toolName,
            Func<string, Task> extractMethod,
            Func<string, Task<bool>> validationMethod,
            IProgress<double>? progress = null)
        {
            try
            {
                Logger.Info($"Starting {toolName} download from: {downloadInfo.Url}");
                progress?.Report(0);

                // Download
                var response = await _httpClient.GetAsync(downloadInfo.Url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                var tempFilePath = Path.Combine(installDirectory, downloadInfo.FileName);

                await using (var contentStream = await response.Content.ReadAsStreamAsync())
                await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
                                 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (double)downloadedBytes / totalBytes * 70; // 70% for download
                            progress?.Report(progressPercentage);
                        }
                    }
                }

                Logger.Info($"{toolName} download completed, extracting...");
                progress?.Report(75);

                // Extract
                await extractMethod(tempFilePath);

                progress?.Report(85);

                // Cleanup
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);

                progress?.Report(90);

                Logger.Info($"{toolName} installation completed successfully");
                return (true, "Installation completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download and install {toolName}: {ex.Message}");
                return (false, $"Installation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Set executable permissions on Unix-like systems
        /// </summary>
        public async Task SetExecutablePermissionsAsync(string executablePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{executablePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    await process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to set executable permissions: {ex.Message}");
                }
            }
        }
    }
}