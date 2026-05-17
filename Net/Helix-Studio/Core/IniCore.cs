using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Helix_Studio.Core;

/// <summary>
/// INI 設定檔讀寫核心（使用 Microsoft.Extensions.Configuration.Ini）。
/// </summary>
public sealed class IniCore : IDisposable
{
    private readonly string _iniFilePath;
    private readonly ConfigurationRoot _configuration;
    private readonly IDisposable _reloadSubscription;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private static readonly IReadOnlyDictionary<Type, Func<string, object>> Parsers =
        new Dictionary<Type, Func<string, object>>
        {
            [typeof(int)] = static value => int.Parse(value, CultureInfo.InvariantCulture),
            [typeof(string)] = static value => value,
        };

    /// <summary>
    /// INI 檔案變更事件（由底層設定提供者觸發）。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立 INI 核心並掛載檔案變更監控。
    /// </summary>
    /// <param name="iniFilePath">INI 檔案路徑；未指定時使用預設路徑。</param>
    public IniCore(string? iniFilePath = null)
    {
        _iniFilePath = ResolveIniFilePath(iniFilePath);
        _configuration = BuildConfiguration(_iniFilePath);
        _reloadSubscription = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            () => Changed?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// 查詢指定區段的快照物件。
    /// </summary>
    /// <typeparam name="TSnapshot">快照型別。</typeparam>
    /// <param name="section">INI 區段名稱。</param>
    public TSnapshot QuerySnapshot<TSnapshot>(string section)
        where TSnapshot : class, new()
    {
        var snapshot = new TSnapshot();
        var properties = typeof(TSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties.Where(property => property.CanWrite))
        {
            var value = _configuration.GetSection(section)[property.Name] ?? string.Empty;
            property.SetValue(snapshot, Parsers[property.PropertyType](value));
        }

        return snapshot;
    }

    /// <summary>
    /// 儲存指定區段的快照物件。
    /// </summary>
    /// <typeparam name="TSnapshot">快照型別。</typeparam>
    /// <param name="section">INI 區段名稱。</param>
    /// <param name="snapshot">快照物件。</param>
    public void SaveSnapshot<TSnapshot>(string section, TSnapshot snapshot)
        where TSnapshot : class
    {
        var properties = typeof(TSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = properties
            .Where(property => property.CanRead)
            .Select(property => new IniEntry(
                section,
                property.Name,
                Convert.ToString(property.GetValue(snapshot), CultureInfo.InvariantCulture) ?? string.Empty))
            .ToArray();

        Save(entries);
    }

    /// <summary>
    /// 釋放檔案變更訂閱。
    /// </summary>
    public void Dispose() => _reloadSubscription.Dispose();

    private void Save(params IniEntry[] entries)
    {
        _ioLock.Wait();
        try
        {
            var sections = QuerySectionsFromFile();

            foreach (var entry in entries)
            {
                var section = sections.TryGetValue(entry.Section, out var existing)
                    ? existing
                    : sections[entry.Section] = new Dictionary<string, string>(StringComparer.Ordinal);

                section[entry.Key] = entry.Value;
            }

            var output = new StringBuilder();
            foreach (var section in sections.OrderBy(section => section.Key, StringComparer.Ordinal))
            {
                output.Append('[').Append(section.Key).AppendLine("]");
                foreach (var value in section.Value.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    output.Append(value.Key).Append('=').Append(value.Value).AppendLine();
                }

                output.AppendLine();
            }

            SaveToFile(output.ToString());
            _configuration.Reload();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private Dictionary<string, Dictionary<string, string>> QuerySectionsFromFile()
    {
        var text = File.ReadAllText(_iniFilePath, Encoding.UTF8);
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var currentSection = string.Empty;

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length >= 2)
            {
                currentSection = line[1..^1].Trim();
                _ = sections.TryAdd(currentSection, new Dictionary<string, string>(StringComparer.Ordinal));
                continue;
            }

            var equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim();

            var section = sections.TryGetValue(currentSection, out var existing)
                ? existing
                : sections[currentSection] = new Dictionary<string, string>(StringComparer.Ordinal);

            section[key] = value;
        }

        return sections;
    }

    private void SaveToFile(string content)
    {
        var tempPath = Path.Combine(
            Path.GetDirectoryName(_iniFilePath)!,
            $"{Path.GetFileName(_iniFilePath)}.{Guid.NewGuid():N}.tmp");

        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(tempPath, _iniFilePath, overwrite: true);
    }

    private static string ResolveIniFilePath(string? iniFilePath)
    {
        var filePath = iniFilePath ?? AppPathCore.QueryConfigFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var _ = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        return filePath;
    }

    private static ConfigurationRoot BuildConfiguration(string iniFilePath)
        => (ConfigurationRoot)new ConfigurationBuilder()
            .AddIniFile(iniFilePath, optional: false, reloadOnChange: true)
            .Build();

    private sealed record IniEntry(string Section, string Key, string Value);
}
