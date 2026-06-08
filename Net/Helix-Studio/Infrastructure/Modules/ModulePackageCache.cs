using System;
using System.IO;
using Helix_Studio.Infrastructure.Paths;

namespace Helix_Studio.Infrastructure.Modules;

internal static class ModulePackageCache
{
    public static string QueryStagedDllPath(string sourceDirectoryPath, string sourceDllPath, string cacheKey, string purpose)
    {
        var signature = QuerySignature(sourceDllPath);
        var relativeDllPath = Path.GetRelativePath(sourceDirectoryPath, sourceDllPath);
        var stagedDirectoryPath = Path.Combine(
            AppPathProvider.QueryAppDataDirectory(),
            "cache",
            "modules",
            SanitizePathSegment(purpose),
            SanitizePathSegment(cacheKey),
            signature);

        var stagedDllPath = Path.Combine(stagedDirectoryPath, relativeDllPath);
        if (File.Exists(stagedDllPath))
        {
            return stagedDllPath;
        }

        CopyDirectory(sourceDirectoryPath, stagedDirectoryPath);
        return stagedDllPath;
    }

    private static string QuerySignature(string dllPath)
    {
        var info = new FileInfo(dllPath);
        return $"{info.Length}-{info.LastWriteTimeUtc.Ticks}";
    }

    private static string SanitizePathSegment(string value)
    {
        var sanitized = (value ?? string.Empty).Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }

    private static void CopyDirectory(string sourceDirectoryPath, string targetDirectoryPath)
    {
        Directory.CreateDirectory(targetDirectoryPath);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectoryPath))
        {
            var targetFilePath = Path.Combine(targetDirectoryPath, Path.GetFileName(filePath));
            File.Copy(filePath, targetFilePath, overwrite: true);
        }

        foreach (var childDirectoryPath in Directory.EnumerateDirectories(sourceDirectoryPath))
        {
            var targetChildDirectoryPath = Path.Combine(targetDirectoryPath, Path.GetFileName(childDirectoryPath));
            CopyDirectory(childDirectoryPath, targetChildDirectoryPath);
        }
    }
}
