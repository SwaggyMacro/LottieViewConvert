using System;
using System.IO;
using LottieViewConvert.Helper.LogHelper;

namespace LottieViewConvert.Utils;

public class FolderUtil
{
    public static void OpenSavedFolder(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Open saved folder failed: {ex}");
        }
    }
}