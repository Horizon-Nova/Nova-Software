using System.Diagnostics;
using System.IO;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecFileLaunchUtility
{
    public static void OpenFile(PreviewResultItem resultItem, bool enableAutoRun)
    {
        if (!File.Exists(resultItem.FilePath))
        {
            throw new FileNotFoundException("找不到指定的 InSpec 檔案。", resultItem.FilePath);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resultItem.FilePath,
                UseShellExecute = true
            });

            InSpecRunButtonUtility.StartAutoRunAfterOpen(enableAutoRun);
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{resultItem.FilePath}\"",
                UseShellExecute = true
            });
        }
    }
}
