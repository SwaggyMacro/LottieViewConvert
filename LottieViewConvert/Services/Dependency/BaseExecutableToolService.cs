using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Interface.Dependency;
using LottieViewConvert.Models;
using LottieViewConvert.Services.Core;

namespace LottieViewConvert.Services.Dependency
{
    /// <summary>
    /// Base class for executable tool services
    /// </summary>
    public abstract class BaseExecutableToolService : IExecutableToolService
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _toolFolderPath;
        protected readonly ExecutableToolConfig _config;
        protected readonly ExecutableDetectionService _detectionService;
        protected readonly PathEnvironmentService _pathService;
        protected readonly DownloadInstallService _downloadService;

        protected BaseExecutableToolService(ExecutableToolConfig config)
        {
            _config = config;
            
            var configService = new ConfigService();
            configService.LoadConfig();
            
            if (!string.IsNullOrEmpty(configService.GetConfig().ProxyAddress))
            {
                _httpClient = new HttpClient(new HttpClientHandler
                {
                    Proxy = new WebProxy(configService.GetConfig().ProxyAddress),
                    UseProxy = true
                });
            }
            else
            {
                _httpClient = new HttpClient();
            }

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LottieViewConvert");
            _toolFolderPath = Path.Combine(appDataPath, _config.LocalFolderName);

            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(_toolFolderPath);

            _detectionService = new ExecutableDetectionService();
            _pathService = new PathEnvironmentService();
            _downloadService = new DownloadInstallService(_httpClient);
        }

        public virtual async Task<string?> DetectToolPathAsync()
        {
            // 1. Check system PATH
            var systemPath = await GetToolFromSystemPathAsync();
            if (!string.IsNullOrEmpty(systemPath))
            {
                Logger.Info($"Found {_config.ToolName} in system PATH: {systemPath}");
                return systemPath;
            }

            // 2. Check common locations
            var commonPath = _detectionService.GetExecutableFromCommonLocations(_config);
            if (!string.IsNullOrEmpty(commonPath))
            {
                Logger.Info($"Found {_config.ToolName} in common location: {commonPath}");
                return commonPath;
            }

            // 3. Check local installation
            var localPath = GetLocalToolPath();
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                Logger.Info($"Found {_config.ToolName} in local installation: {localPath}");
                return localPath;
            }

            Logger.Info($"{_config.ToolName} not found in any location");
            return null;
        }

        public virtual async Task<string?> GetToolFromSystemPathAsync()
        {
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                $"{_config.ExecutableName}.exe" : _config.ExecutableName;
            
            var path = await _detectionService.GetExecutableFromSystemPathAsync(executableName);
            
            if (!string.IsNullOrEmpty(path) && await IsToolValidAsync(path))
            {
                return path;
            }
            
            return null;
        }

        public virtual string GetLocalToolPath()
        {
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                $"{_config.ExecutableName}.exe" : _config.ExecutableName;
            return Path.Combine(_toolFolderPath, executableName);
        }

        public virtual string GetLocalToolDirectory()
        {
            return _toolFolderPath;
        }

        public virtual async Task<bool> IsToolValidAsync(string? toolPath = null)
        {
            if (string.IsNullOrEmpty(toolPath))
                return false;

            return await _detectionService.IsExecutableValidAsync(toolPath, _config.VersionArgument);
        }

        public virtual async Task<string> GetToolVersionAsync(string? toolPath = null)
        {
            if (string.IsNullOrEmpty(toolPath))
                return "Not Found";

            return await _detectionService.GetExecutableVersionAsync(toolPath, _config.VersionArgument, _config.ToolName);
        }

        public virtual async Task<string> GetToolSourceAsync(string toolPath)
        {
            try
            {
                // Check system path
                var systemPath = await GetToolFromSystemPathAsync();
                if (!string.IsNullOrEmpty(systemPath) && Path.GetFullPath(systemPath) == Path.GetFullPath(toolPath))
                {
                    return "System (PATH)";
                }

                // Check common paths
                var commonPath = _detectionService.GetExecutableFromCommonLocations(_config);
                if (!string.IsNullOrEmpty(commonPath) && Path.GetFullPath(commonPath) == Path.GetFullPath(toolPath))
                {
                    return "System Installation";
                }

                // Check local path
                var localPath = GetLocalToolPath();
                if (!string.IsNullOrEmpty(localPath) && Path.GetFullPath(localPath) == Path.GetFullPath(toolPath))
                {
                    return "Local Installation";
                }

                return "Custom Path";
            }
            catch
            {
                return "Unknown";
            }
        }

        public virtual bool IsLocalToolInPath()
        {
            return _pathService.IsDirectoryInPath(GetLocalToolDirectory());
        }

        public virtual async Task<(bool Success, string Message)> AddToPathAsync()
        {
            var localDirectory = GetLocalToolDirectory();
            var localToolPath = GetLocalToolPath();

            if (!Directory.Exists(localDirectory))
            {
                return (false, $"Local {_config.ToolName} directory does not exist");
            }

            if (string.IsNullOrEmpty(localToolPath) || !File.Exists(localToolPath))
            {
                return (false, $"{_config.ToolName} executable not found in local directory");
            }

            if (IsLocalToolInPath())
            {
                return (true, $"{_config.ToolName} directory is already in PATH");
            }

            return await _pathService.AddToPathAsync(localDirectory, _config.ToolName);
        }

        public virtual async Task<(bool Success, string Message)> RemoveFromPathAsync()
        {
            var localDirectory = GetLocalToolDirectory();
            return await _pathService.RemoveFromPathAsync(localDirectory, _config.ToolName);
        }

        public abstract Task<(bool Success, string Message)> DownloadAndInstallToolAsync(
            IProgress<double>? progress = null, bool addToPath = true);

        public virtual void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}