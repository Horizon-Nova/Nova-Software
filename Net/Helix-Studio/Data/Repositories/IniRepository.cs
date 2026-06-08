using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Helix_Studio.Infrastructure.Paths;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// INI 設定檔 Repository。
/// </summary>
public sealed class IniRepository : IDisposable
{
    private readonly string _iniFilePath;
    private readonly IConfigurationRoot _configuration;
    private readonly IDisposable _reloadSubscription;
    private readonly object _writeSync = new();
    private bool _suppressChanged;

    private static readonly UTF8Encoding Utf8BomEncoding = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding StrictUtf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly TimeSpan ReplaceRetryDelay = TimeSpan.FromMilliseconds(40);
    private const int ReplaceRetryCount = 5;

    private static readonly IReadOnlyDictionary<Type, Func<string, object>> Parsers =
        new Dictionary<Type, Func<string, object>>
        {
            [typeof(int)] = static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0,
            [typeof(string)] = static value => value,
            [typeof(bool)] = static value => bool.TryParse(value, out var flag) && flag,
        };

    /// <summary>
    /// 設定變更事件。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立設定 Repository。
    /// </summary>
    /// <param name="iniFilePath">設定檔路徑，未提供時使用預設 App Home Root 設定檔。</param>
    public IniRepository(string? iniFilePath = null)
    {
        _iniFilePath = iniFilePath ?? AppPathProvider.QueryConfigFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(_iniFilePath)!);

        using var _ = new FileStream(_iniFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        EnsureIniFileEncoding(_iniFilePath);

        _configuration = BuildConfiguration(_iniFilePath);

        _reloadSubscription = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            () =>
            {
                if (_suppressChanged)
                {
                    return;
                }

                Changed?.Invoke(this, EventArgs.Empty);
            });
    }

    private static IConfigurationRoot BuildConfiguration(string iniFilePath)
    {
        try
        {
            return new ConfigurationBuilder()
                .AddIniFile(iniFilePath, optional: true, reloadOnChange: true)
                .Build();
        }
        catch (InvalidDataException)
        {
            NormalizeIniFile(iniFilePath);
            return new ConfigurationBuilder()
                .AddIniFile(iniFilePath, optional: true, reloadOnChange: true)
                .Build();
        }
        catch (FormatException)
        {
            NormalizeIniFile(iniFilePath);
            return new ConfigurationBuilder()
                .AddIniFile(iniFilePath, optional: true, reloadOnChange: true)
                .Build();
        }
    }

    private static void EnsureIniFileEncoding(string iniFilePath)
    {
        try
        {
            var text = ReadIniText(iniFilePath);
            File.WriteAllText(iniFilePath, text, Utf8BomEncoding);
        }
        catch (IOException)
        {
            // 若設定檔暫時被占用，交由後續讀取或寫入流程重試。
        }
    }

    private static string ReadIniText(string iniFilePath)
    {
        var bytes = File.ReadAllBytes(iniFilePath);
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (HasUtf8Bom(bytes))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (HasUtf16LeBom(bytes))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasUtf16BeBom(bytes))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return StrictUtf8Encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(bytes);
        }
    }

    private static void NormalizeIniFile(string iniFilePath)
    {
        var document = ParseIniDocument(ReadIniText(iniFilePath));
        WriteIniDocument(iniFilePath, document.SectionOrder, document.KeyOrderBySection, document.ValuesBySection);
    }

    private static IniDocument ParseIniDocument(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sectionOrder = new List<string>();
        var keyOrderBySection = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var valuesBySection = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                currentSection = line[1..^1].Trim();
                if (currentSection.Length == 0)
                {
                    continue;
                }

                EnsureSection(currentSection, sectionOrder, keyOrderBySection, valuesBySection);
                continue;
            }

            if (currentSection.Length == 0)
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var value = equalsIndex == line.Length - 1 ? string.Empty : line[(equalsIndex + 1)..].Trim();
            var sectionValues = valuesBySection[currentSection];
            if (!sectionValues.ContainsKey(key))
            {
                keyOrderBySection[currentSection].Add(key);
            }

            sectionValues[key] = value;
        }

        return new IniDocument(sectionOrder, keyOrderBySection, valuesBySection);
    }

    private static void SaveIniValues(string iniFilePath, string section, IReadOnlyDictionary<string, string?> values)
    {
        var document = ParseIniDocument(File.Exists(iniFilePath) ? ReadIniText(iniFilePath) : string.Empty);
        EnsureSection(section, document.SectionOrder, document.KeyOrderBySection, document.ValuesBySection);

        foreach (var entry in values)
        {
            if (!document.ValuesBySection[section].ContainsKey(entry.Key))
            {
                document.KeyOrderBySection[section].Add(entry.Key);
            }

            document.ValuesBySection[section][entry.Key] = entry.Value ?? string.Empty;
        }

        WriteIniDocument(iniFilePath, document.SectionOrder, document.KeyOrderBySection, document.ValuesBySection);
    }

    private static void WriteIniDocument(
        string iniFilePath,
        IReadOnlyList<string> sectionOrder,
        IReadOnlyDictionary<string, List<string>> keyOrderBySection,
        IReadOnlyDictionary<string, Dictionary<string, string>> valuesBySection)
    {
        var builder = new StringBuilder();
        for (var sectionIndex = 0; sectionIndex < sectionOrder.Count; sectionIndex++)
        {
            var section = sectionOrder[sectionIndex];
            builder.Append('[').Append(section).AppendLine("]");

            foreach (var key in keyOrderBySection[section])
            {
                builder.Append(key)
                    .Append('=')
                    .AppendLine(valuesBySection[section][key] ?? string.Empty);
            }

            if (sectionIndex != sectionOrder.Count - 1)
            {
                builder.AppendLine();
            }
        }

        var content = builder.ToString();
        var directoryPath = Path.GetDirectoryName(iniFilePath) ?? string.Empty;
        var tempFilePath = Path.Combine(
            directoryPath,
            $"{Path.GetFileName(iniFilePath)}.{Guid.NewGuid():N}.tmp");

        WriteFileWithSharedRead(tempFilePath, content);

        try
        {
            ReplaceIniFile(tempFilePath, iniFilePath, content);
        }
        finally
        {
            TryDeleteTemporaryFile(tempFilePath);
        }
    }

    private static void ReplaceIniFile(string tempFilePath, string iniFilePath, string content)
    {
        for (var attempt = 0; attempt < ReplaceRetryCount; attempt++)
        {
            try
            {
                File.Replace(tempFilePath, iniFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }
            catch (FileNotFoundException)
            {
                File.Move(tempFilePath, iniFilePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < ReplaceRetryCount - 1)
            {
                Thread.Sleep(ReplaceRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < ReplaceRetryCount - 1)
            {
                Thread.Sleep(ReplaceRetryDelay);
            }
        }

        // 高頻切換時 reload watcher 可能短暫鎖住目標檔，改用共享寫入避免整個宿主因設定寫檔失敗而退出。
        WriteFileWithSharedRead(iniFilePath, content);
    }

    private static void WriteFileWithSharedRead(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, Utf8BomEncoding);
        writer.Write(content);
    }

    private static void TryDeleteTemporaryFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void EnsureSection(
        string section,
        ICollection<string> sectionOrder,
        IDictionary<string, List<string>> keyOrderBySection,
        IDictionary<string, Dictionary<string, string>> valuesBySection)
    {
        if (valuesBySection.ContainsKey(section))
        {
            return;
        }

        valuesBySection[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        keyOrderBySection[section] = new List<string>();
        sectionOrder.Add(section);
    }

    private static bool HasUtf8Bom(byte[] bytes)
        => bytes.Length >= 3
           && bytes[0] == 0xEF
           && bytes[1] == 0xBB
           && bytes[2] == 0xBF;

    private static bool HasUtf16LeBom(byte[] bytes)
        => bytes.Length >= 2
           && bytes[0] == 0xFF
           && bytes[1] == 0xFE;

    private static bool HasUtf16BeBom(byte[] bytes)
        => bytes.Length >= 2
           && bytes[0] == 0xFE
           && bytes[1] == 0xFF;

    /// <summary>
    /// 讀取指定 section 的設定快照。
    /// </summary>
    public TSnapshot QuerySnapshot<TSnapshot>(string section)
        where TSnapshot : class, new()
    {
        var snapshot = new TSnapshot();
        var properties = typeof(TSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties.Where(property => property is { CanWrite: true }))
        {
            var rawValue = _configuration.GetSection(section)[property.Name] ?? string.Empty;
            property.SetValue(snapshot, Parsers[property.PropertyType](rawValue));
        }

        return snapshot;
    }

    /// <summary>
    /// 寫入指定 section 的設定值。
    /// </summary>
    public void SaveValues(string section, IReadOnlyDictionary<string, string?> values)
    {
        lock (_writeSync)
        {
            _suppressChanged = true;
            try
            {
                EnsureIniFileEncoding(_iniFilePath);
                SaveIniValues(_iniFilePath, section, values);

                try
                {
                    _configuration.Reload();
                }
                catch (InvalidDataException)
                {
                    NormalizeIniFile(_iniFilePath);
                    _configuration.Reload();
                }
                catch (FormatException)
                {
                    NormalizeIniFile(_iniFilePath);
                    _configuration.Reload();
                }
            }
            finally
            {
                _suppressChanged = false;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 釋放 Reload Token 訂閱。
    /// </summary>
    public void Dispose() => _reloadSubscription.Dispose();

    private sealed record IniDocument(
        List<string> SectionOrder,
        Dictionary<string, List<string>> KeyOrderBySection,
        Dictionary<string, Dictionary<string, string>> ValuesBySection);
}
