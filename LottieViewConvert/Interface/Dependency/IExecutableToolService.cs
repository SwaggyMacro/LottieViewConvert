using System;
using System.Threading.Tasks;

namespace LottieViewConvert.Interface.Dependency
{
    /// <summary>
    /// Interface for executable tool services
    /// </summary>
    public interface IExecutableToolService : IDisposable
    {
        Task<string?> DetectToolPathAsync();
        Task<string?> GetToolFromSystemPathAsync();
        string GetLocalToolPath();
        string GetLocalToolDirectory();
        Task<bool> IsToolValidAsync(string? toolPath = null);
        Task<string> GetToolVersionAsync(string? toolPath = null);
        Task<string> GetToolSourceAsync(string toolPath);
        bool IsLocalToolInPath();
        Task<(bool Success, string Message)> AddToPathAsync();
        Task<(bool Success, string Message)> RemoveFromPathAsync();
        Task<(bool Success, string Message)> DownloadAndInstallToolAsync(IProgress<double>? progress = null, bool addToPath = true);
    }
}