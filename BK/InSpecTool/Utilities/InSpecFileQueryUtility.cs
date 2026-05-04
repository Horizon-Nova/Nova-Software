using System.IO;
using System.Text.RegularExpressions;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public sealed class InSpecFileQueryUtility
{
    private sealed class IndexedFileEntry
    {
        public string FilePath { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string FileNameUpper { get; init; } = string.Empty;

        public DateTime LastWriteTimeUtc { get; init; }
    }

    private readonly object _syncRoot = new();
    private string _indexedRootDirectory = string.Empty;
    private List<IndexedFileEntry> _indexedEntries = [];

    public void ClearIndex()
    {
        lock (_syncRoot)
        {
            _indexedRootDirectory = string.Empty;
            _indexedEntries = [];
        }
    }

    public IReadOnlyList<PreviewResultItem> QueryFiles(string searchDirectory, string rawPartNumber, string normalizedPartNumber, string setupKeyword)
    {
        var rootDirectory = searchDirectory.Trim();
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("尚未設定搜尋資料夾。");
        }

        EnsureIndex(rootDirectory);

        var queryTokens = new[] { rawPartNumber, normalizedPartNumber }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (queryTokens.Length == 0)
        {
            return [];
        }

        var requiredKeyword = setupKeyword.Trim().ToUpperInvariant();
        List<IndexedFileEntry> matches;

        lock (_syncRoot)
        {
            matches = _indexedEntries
                .Where(entry =>
                    (string.IsNullOrWhiteSpace(requiredKeyword) || entry.FileNameUpper.Contains(requiredKeyword, StringComparison.Ordinal)) &&
                    queryTokens.Any(token => entry.FileNameUpper.Contains(token, StringComparison.Ordinal)))
                .OrderByDescending(entry => entry.LastWriteTimeUtc)
                .ToList();
        }

        return matches
            .Select(entry => new PreviewResultItem
            {
                FileName = entry.FileName,
                FilePath = entry.FilePath,
                PartNumber = normalizedPartNumber,
                Revision = QueryRevision(entry.FileName)
            })
            .ToArray();
    }

    private void EnsureIndex(string rootDirectory)
    {
        var normalizedRoot = Path.GetFullPath(rootDirectory);
        lock (_syncRoot)
        {
            if (string.Equals(_indexedRootDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Directory.Exists(normalizedRoot))
            {
                throw new DirectoryNotFoundException($"找不到搜尋資料夾：{normalizedRoot}");
            }

            _indexedEntries = Directory
                .EnumerateFiles(normalizedRoot, "*.iwp", SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return new IndexedFileEntry
                    {
                        FilePath = fileInfo.FullName,
                        FileName = fileInfo.Name,
                        FileNameUpper = fileInfo.Name.ToUpperInvariant(),
                        LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                    };
                })
                .ToList();

            _indexedRootDirectory = normalizedRoot;
        }
    }

    private static string QueryRevision(string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var match = Regex.Match(fileNameWithoutExtension, @"(?i)\brev[.\-_ ]?([A-Z0-9]+)\b");
        return match.Success ? $"Rev.{match.Groups[1].Value.ToUpperInvariant()}" : string.Empty;
    }
}
