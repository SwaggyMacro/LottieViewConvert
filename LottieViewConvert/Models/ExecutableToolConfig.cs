using System.Collections.Generic;

namespace LottieViewConvert.Models
{
    /// <summary>
    /// Configuration for executable tools
    /// </summary>
    public class ExecutableToolConfig
    {
        public string ToolName { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty;
        public string LocalFolderName { get; set; } = string.Empty;
        public string VersionArgument { get; set; } = string.Empty;
        public List<string> WindowsCommonPaths { get; set; } = new();
        public List<string> MacOSCommonPaths { get; set; } = new();
        public List<string> LinuxCommonPaths { get; set; } = new();
        public string PathComment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Download information for tools
    /// </summary>
    public class ToolDownloadInfo
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsZip { get; set; } = true;
    }
}