using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LottieViewConvert.Helper;

public static class FilePickerHelper
{
    /// <summary>
    /// Choose a file with specified options.
    /// </summary>
    /// <param name="window">parent window</param>
    /// <param name="title">dialog title</param>
    /// <param name="fileTypeFilter">file type filters</param>
    /// <param name="allowMultiple">allow multiple file selection</param>
    /// <returns>the selected file path(s), or null if canceled</returns>
    public static async Task<string?> SelectFileAsync(
        Window window,
        string title,
        FilePickerFileType fileTypeFilter = null,
        bool allowMultiple = false)
    {
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title,
            FileTypeFilter = [fileTypeFilter]
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        if (result.Count <= 0) return null;
        
        var file = result[0];
        return file.Path.LocalPath;
    }

    /// <summary>
    /// Choose multiple files with specified options.
    /// </summary>
    /// <param name="window">parent window</param>
    /// <param name="title">dialog title</param>
    /// <param name="fileTypeFilter">file type filters</param>
    /// <returns>the selected file paths, or empty array if canceled</returns>
    public static async Task<string[]> SelectMultipleFilesAsync(
        Window window,
        string title,
        IReadOnlyList<FilePickerFileType>? fileTypeFilter = null)
    {
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = title,
            FileTypeFilter = fileTypeFilter
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        if (result.Count <= 0) return new string[0];
        
        var paths = new string[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            paths[i] = result[i].Path.LocalPath;
        }
        return paths;
    }

    /// <summary>
    /// Choose a folder with specified title.
    /// </summary>
    /// <param name="window">parent window</param>
    /// <param name="title">dialog title</param>
    /// <param name="allowMultiple">allow multiple folder selection</param>
    /// <returns>the selected folder path, or null if canceled</returns>
    public static async Task<string?> SelectFolderAsync(
        Window window,
        string title,
        bool allowMultiple = false)
    {
        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            Title = title
        };

        var result = await window.StorageProvider.OpenFolderPickerAsync(options);
        if (result.Count <= 0) return null;
        
        var folder = result[0];
        return folder.Path.LocalPath;
    }

    /// <summary>
    /// Choose a file to save with specified options.
    /// </summary>
    /// <param name="window">parent window</param>
    /// <param name="title">dialog title</param>
    /// <param name="defaultName">default file name</param>
    /// <param name="fileTypes">file type choices</param>
    /// <returns>the selected file path, or null if canceled</returns>
    public static async Task<string?> SelectSaveFileAsync(
        Window window,
        string title,
        string defaultName = "",
        IReadOnlyList<FilePickerFileType>? fileTypes = null)
    {
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices = fileTypes
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Create a file type filter for common use cases.
    /// </summary>
    /// <param name="name">display name for the file type</param>
    /// <param name="patterns">file patterns (e.g., "*.json", "*.txt")</param>
    /// <returns>a FilePickerFileType object</returns>
    public static FilePickerFileType CreateFileType(string name, params string[] patterns)
    {
        return new FilePickerFileType(name)
        {
            Patterns = patterns
        };
    }
}