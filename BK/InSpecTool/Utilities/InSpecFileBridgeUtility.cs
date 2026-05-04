using System.IO;
using System.IO.IsolatedStorage;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecFileBridgeUtility
{
    private static readonly string BaseDirectoryPath = @"C:\InSpecTool";
    private static readonly string InputFilePath = Path.Combine(BaseDirectoryPath, "Input.txt");
    private static readonly string CurrentTargetFilePath = Path.Combine(BaseDirectoryPath, ".current_target.txt");
    private const string IsolatedStorageDirectoryName = "InSpecTool";
    private const string IsolatedStorageCurrentTargetFileName = "current-target.txt";

    public static string BaseDirectory => BaseDirectoryPath;

    public static string InputFile => InputFilePath;

    public static void PrepareBridgeFiles()
    {
        Directory.CreateDirectory(BaseDirectoryPath);
        EnsureFileExists(InputFilePath);
        DeleteLegacyCurrentTargetFile();
    }

    public static void EnsureInputFileReady()
    {
        EnsureFileExists(InputFilePath);
    }

    public static void TruncateInputFile()
    {
        EnsureFileExists(InputFilePath, truncate: true);
    }

    public static byte[] StripUtf16LeBom(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return bytes[2..];
        }

        return bytes;
    }

    public static async Task AppendContentToTargetAsync(string targetPath, byte[] contentBytes)
    {
        var parentDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        await using var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(contentBytes).ConfigureAwait(false);
    }

    public static string LoadCurrentTargetPathFromState()
    {
        using var store = IsolatedStorageFile.GetUserStoreForAssembly();
        var statePath = QueryStateRelativePath();
        if (!store.FileExists(statePath))
        {
            return string.Empty;
        }

        using var stream = new IsolatedStorageFileStream(statePath, FileMode.Open, FileAccess.Read, store);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    public static void SaveCurrentTargetPathToState(string targetPath)
    {
        using var store = IsolatedStorageFile.GetUserStoreForAssembly();
        EnsureIsolatedStorageDirectoryExists(store);
        var statePath = QueryStateRelativePath();

        using var stream = new IsolatedStorageFileStream(statePath, FileMode.Create, FileAccess.Write, store);
        using var writer = new StreamWriter(stream);
        writer.Write(targetPath ?? string.Empty);
    }

    public static void DeleteLegacyCurrentTargetFile()
    {
        if (File.Exists(CurrentTargetFilePath))
        {
            File.Delete(CurrentTargetFilePath);
        }
    }

    private static void EnsureIsolatedStorageDirectoryExists(IsolatedStorageFile store)
    {
        if (!store.DirectoryExists(IsolatedStorageDirectoryName))
        {
            store.CreateDirectory(IsolatedStorageDirectoryName);
        }
    }

    private static string QueryStateRelativePath() =>
        $"{IsolatedStorageDirectoryName}/{IsolatedStorageCurrentTargetFileName}";

    private static void EnsureFileExists(string path, bool truncate = false)
    {
        if (truncate || !File.Exists(path))
        {
            File.WriteAllBytes(path, []);
        }
    }
}
