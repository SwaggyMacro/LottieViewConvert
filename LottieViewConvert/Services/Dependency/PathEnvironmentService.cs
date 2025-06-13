using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LottieViewConvert.Helper.LogHelper;
using LottieViewConvert.Lang;
using Microsoft.Win32;

namespace LottieViewConvert.Services.Core
{
    /// <summary>
    /// Service for managing PATH environment variables
    /// </summary>
    public class PathEnvironmentService
    {
        /// <summary>
        /// Check if a directory is in the PATH environment variable
        /// </summary>
        public bool IsDirectoryInPath(string directory)
        {
            try
            {
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                var processPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";

                var allPaths = new[] { userPath, systemPath, processPath };

                foreach (var pathVariable in allPaths)
                {
                    if (string.IsNullOrEmpty(pathVariable)) continue;

                    var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var path in paths)
                    {
                        try
                        {
                            var normalizedPath = Path.GetFullPath(path.Trim().Trim('"'));
                            var normalizedLocal = Path.GetFullPath(directory);

                            if (string.Equals(normalizedPath, normalizedLocal, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // Ignore invalid paths
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to check if directory is in PATH: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add directory to PATH environment variable
        /// </summary>
        public async Task<(bool Success, string Message)> AddToPathAsync(string directory, string toolName)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return (false, $"Local {toolName} directory does not exist");
                }

                if (IsDirectoryInPath(directory))
                {
                    return (true, $"{toolName} directory is already in PATH");
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await AddToWindowsPathAsync(directory, toolName);
                }
                else
                {
                    return await AddToUnixPathAsync(directory, toolName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add {toolName} to PATH: {ex.Message}");
                return (false, $"{Resources.FailedToAddToPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove directory from PATH environment variable
        /// </summary>
        public async Task<(bool Success, string Message)> RemoveFromPathAsync(string directory, string toolName)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await RemoveFromWindowsPathAsync(directory, toolName);
                }

                return await RemoveFromUnixPathAsync(directory, toolName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove {toolName} from PATH: {ex.Message}");
                return (false, $"{Resources.FailedToRemoveFromPath}: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> AddToWindowsPathAsync(string directory, string toolName)
        {
            try
            {
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                var cleanedPath = RemoveDuplicatePathEntries(userPath, directory);
                var newUserPath = string.IsNullOrEmpty(cleanedPath)
                    ? directory
                    : $"{cleanedPath}{Path.PathSeparator}{directory}";

                Environment.SetEnvironmentVariable("PATH", newUserPath, EnvironmentVariableTarget.User);
                await Task.Delay(1000);

                if (IsDirectoryInPath(directory))
                {
                    Logger.Info($"Successfully added {directory} to user PATH");
                    return (true, $"{toolName} directory added to PATH successfully. You may need to restart applications to see the change.");
                }

                return await AddToWindowsPathViaRegistryAsync(directory, toolName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add to Windows PATH: {ex.Message}");
                return (false, $"{Resources.FailedToAddToPath}: {ex.Message}");
            }
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
        private async Task<(bool Success, string Message)> AddToWindowsPathViaRegistryAsync(string directory, string toolName)
        {
            try
            {
                const string userEnvironmentRegistryKey = @"Environment";
                using var key = Registry.CurrentUser.CreateSubKey(userEnvironmentRegistryKey);

                if (key != null)
                {
                    var currentPath = key.GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                    if (currentPath != null)
                    {
                        var cleanedPath = RemoveDuplicatePathEntries(currentPath, directory);
                        var newPath = string.IsNullOrEmpty(cleanedPath)
                            ? directory
                            : $"{cleanedPath}{Path.PathSeparator}{directory}";

                        key.SetValue("PATH", newPath, RegistryValueKind.ExpandString);
                    }

                    await NotifySystemEnvironmentChangeAsync();

                    Logger.Info($"Successfully added {directory} to user PATH via registry");
                    return (true, $"{toolName} directory added to PATH successfully. You may need to restart applications to see the change.");
                }

                return (false, Resources.UnableToAccessUserEnvironmentRegistryKey);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add to Windows PATH via registry: {ex.Message}");
                return (false, $"{Resources.FailedToAddToPathViaRegistry}: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> AddToUnixPathAsync(string directory, string toolName)
        {
            try
            {
                var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var shellProfiles = new[]
                {
                    Path.Combine(homeDirectory, ".bashrc"),
                    Path.Combine(homeDirectory, ".zshrc"),
                    Path.Combine(homeDirectory, ".profile")
                };

                var exportLine = $"export PATH=\"{directory}:$PATH\"";
                var commentLine = $"# Added by LottieViewConvert - {toolName}";
                var added = false;
                var alreadyExists = false;

                foreach (var profile in shellProfiles)
                {
                    try
                    {
                        if (File.Exists(profile))
                        {
                            var content = await File.ReadAllTextAsync(profile);

                            if (content.Contains(directory) && content.Contains("LottieViewConvert"))
                            {
                                alreadyExists = true;
                                Logger.Info($"{toolName} path already exists in {profile}");
                                continue;
                            }

                            if (!content.Contains(directory))
                            {
                                var linesToAdd = $"\n{commentLine}\n{exportLine}\n";
                                await File.AppendAllTextAsync(profile, linesToAdd);
                                Logger.Info($"Added {toolName} path to {profile}");
                                added = true;
                            }
                        }
                        else
                        {
                            var linesToAdd = $"{commentLine}\n{exportLine}\n";
                            await File.WriteAllTextAsync(profile, linesToAdd);
                            Logger.Info($"Created {profile} with {toolName} path");
                            added = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed to update {profile}: {ex.Message}");
                    }
                }

                if (alreadyExists)
                {
                    return (true, $"{toolName} directory is already configured in shell profiles");
                }

                if (added)
                {
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!currentPath.Contains(directory))
                    {
                        Environment.SetEnvironmentVariable("PATH", $"{directory}:{currentPath}");
                    }

                    return (true, $"{toolName} directory added to shell profiles. You should restart the application to apply changes.");
                }

                return (false, Resources.FailedToUpdateAnyShellProfileFiles);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add to Unix PATH: {ex.Message}");
                return (false, $"{Resources.FailedToAddToPath}: {ex.Message}");
            }
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
        private async Task<(bool Success, string Message)> RemoveFromWindowsPathAsync(string directory, string toolName)
        {
            try
            {
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                var cleanedUserPath = RemoveDuplicatePathEntries(userPath, directory);

                Environment.SetEnvironmentVariable("PATH", cleanedUserPath, EnvironmentVariableTarget.User);

                try
                {
                    const string userEnvironmentRegistryKey = @"Environment";
                    using var key = Registry.CurrentUser.CreateSubKey(userEnvironmentRegistryKey);
                    if (key != null)
                    {
                        var registryPath = key.GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                        if (registryPath != null)
                        {
                            var cleanedRegistryPath = RemoveDuplicatePathEntries(registryPath, directory);
                            key.SetValue("PATH", cleanedRegistryPath, RegistryValueKind.ExpandString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to clean registry PATH: {ex.Message}");
                }

                await NotifySystemEnvironmentChangeAsync();

                Logger.Info($"Removed {directory} from Windows PATH");
                return (true, $"{toolName} directory removed from PATH successfully. You should restart the application to apply changes.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove from Windows PATH: {ex.Message}");
                return (false, $"{Resources.FailedToRemoveFromPath}: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> RemoveFromUnixPathAsync(string directory, string toolName)
        {
            try
            {
                var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var shellProfiles = new[]
                {
                    Path.Combine(homeDirectory, ".bashrc"),
                    Path.Combine(homeDirectory, ".zshrc"),
                    Path.Combine(homeDirectory, ".profile")
                };

                var removed = false;

                foreach (var profile in shellProfiles)
                {
                    try
                    {
                        if (File.Exists(profile))
                        {
                            var lines = await File.ReadAllLinesAsync(profile);
                            var newLines = new List<string>();
                            var skipNext = false;

                            foreach (var line in lines)
                            {
                                if (line.Contains("Added by LottieViewConvert") && line.Contains(toolName))
                                {
                                    skipNext = true;
                                    removed = true;
                                    continue;
                                }

                                if (skipNext && line.Contains(directory) && line.StartsWith("export PATH="))
                                {
                                    skipNext = false;
                                    continue;
                                }

                                if (line.Contains(directory) && line.StartsWith("export PATH=") &&
                                    line.Contains("LottieViewConvert"))
                                {
                                    removed = true;
                                    continue;
                                }

                                skipNext = false;
                                newLines.Add(line);
                            }

                            if (removed)
                            {
                                await File.WriteAllLinesAsync(profile, newLines);
                                Logger.Info($"Removed {toolName} path from {profile}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed to clean {profile}: {ex.Message}");
                    }
                }

                if (removed)
                {
                    return (true, $"{toolName} directory removed from PATH successfully. You should restart the application to apply changes.");
                }

                return (true, $"No {toolName} entries found in shell profiles to remove");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove from Unix PATH: {ex.Message}");
                return (false, $"{Resources.FailedToRemoveFromPath}: {ex.Message}");
            }
        }

        private string RemoveDuplicatePathEntries(string pathVariable, string directoryToRemove)
        {
            if (string.IsNullOrEmpty(pathVariable)) return pathVariable;

            var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var cleanedPaths = new List<string>();
            var normalizedTargetPath = Path.GetFullPath(directoryToRemove);

            foreach (var path in paths)
            {
                try
                {
                    var trimmedPath = path.Trim().Trim('"');
                    if (string.IsNullOrEmpty(trimmedPath)) continue;

                    var normalizedPath = Path.GetFullPath(trimmedPath);

                    if (!string.Equals(normalizedPath, normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        cleanedPaths.Add(path.Trim());
                    }
                }
                catch
                {
                    cleanedPaths.Add(path.Trim());
                }
            }

            return string.Join(Path.PathSeparator.ToString(), cleanedPaths);
        }

        private async Task NotifySystemEnvironmentChangeAsync()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"[Environment]::SetEnvironmentVariable('PATH', [Environment]::GetEnvironmentVariable('PATH', 'User'), 'User')\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to notify system of environment change: {ex.Message}");
            }
        }
    }
}