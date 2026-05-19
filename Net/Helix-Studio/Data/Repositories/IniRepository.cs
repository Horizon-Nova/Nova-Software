using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Helix_Studio.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// INI 存取入口（Repository）。
///
/// 職責：
/// - 以 <c>Microsoft.Extensions.Configuration</c> 讀取 INI（啟用 <c>reloadOnChange</c>）。
/// - 提供快照查詢 <see cref="QuerySnapshot{TSnapshot}"/>。
/// - 提供鍵值寫入 <see cref="SaveValues"/>（寫入後主動 Reload，並觸發 <see cref="Changed"/>）。
///
/// 重要限制：
/// - 本類別只負責 I/O 與變更通知；不包含 UI 套用或渲染刷新邏輯。
/// - UI 套用（主題/語系/縮放/密度等）由 AppHost Runtime（例如 <c>AppearanceRuntime</c>）訂閱 <see cref="Changed"/> 後自行處理。
/// </summary>
public sealed class IniRepository : IDisposable
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WritePrivateProfileString(
        string section,
        string key,
        string value,
        string filePath);

    private readonly string _iniFilePath;
    private readonly IConfigurationRoot _configuration;
    private readonly IDisposable _reloadSubscription;
    private readonly object _writeSync = new();
    private bool _suppressChanged;

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
    /// INI 內容變動通知。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立 INI Repository。
    /// </summary>
    /// <param name="iniFilePath">
    /// INI 檔案路徑；若為 <see langword="null"/>，則使用 <see cref="AppPathHelper.QueryConfigFilePath"/>。
    /// </param>
    public IniRepository(string? iniFilePath = null)
    {
        _iniFilePath = iniFilePath ?? AppPathHelper.QueryConfigFilePath();
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
            using var stream = new FileStream(iniFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (stream.Length == 0)
            {
                WriteUtf16LeBom(stream);
                return;
            }

            var bom = new byte[3];
            var read = stream.Read(bom, 0, bom.Length);
            stream.Position = 0;

            var isUtf8Bom = read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF;
            if (!isUtf8Bom)
            {
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = reader.ReadToEnd();
            stream.SetLength(0);
            WriteUtf16LeBom(stream);

            using var writer = new StreamWriter(stream, new UnicodeEncoding(bigEndian: false, byteOrderMark: false));
            writer.Write(text);
        }
        catch (IOException)
        {
            // INI 可能正被其他程序使用；此處不強制改寫，避免造成額外 I/O 競態。
        }
    }

    private static void WriteUtf16LeBom(Stream stream)
    {
        stream.WriteByte(0xFF);
        stream.WriteByte(0xFE);
        stream.Flush();
    }

    private static void NormalizeIniFile(string iniFilePath)
    {
        using var inputStream = new FileStream(iniFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(inputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();

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

                if (!valuesBySection.ContainsKey(currentSection))
                {
                    valuesBySection[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    keyOrderBySection[currentSection] = new List<string>();
                    sectionOrder.Add(currentSection);
                }

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
            var values = valuesBySection[currentSection];
            if (!values.ContainsKey(key))
            {
                keyOrderBySection[currentSection].Add(key);
            }

            values[key] = value;
        }

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

        var directoryPath = Path.GetDirectoryName(iniFilePath) ?? string.Empty;
        var tempFilePath = Path.Combine(directoryPath, Path.GetFileName(iniFilePath) + ".tmp");

        using (var outputStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        using (var writer = new StreamWriter(outputStream, new UnicodeEncoding(bigEndian: false, byteOrderMark: true)))
        {
            writer.Write(builder.ToString());
        }

        try
        {
            File.Replace(tempFilePath, iniFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            File.Move(tempFilePath, iniFilePath, overwrite: true);
        }
    }

    /// <summary>
    /// 查詢指定 INI section 的設定快照。
    /// </summary>
    /// <typeparam name="TSnapshot">快照型別（僅支援可寫入的 public 屬性）。</typeparam>
    /// <param name="section">INI section 名稱。</param>
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
    /// 寫入指定 section 的鍵值。
    ///
    /// 行為：
    /// - 寫入完成後會呼叫 <c>Reload()</c> 更新 Configuration 快取。
    /// - 最後一定會觸發 <see cref="Changed"/>，作為「INI 已變更」的唯一通知來源。
    /// </summary>
    /// <param name="section">INI section 名稱。</param>
    /// <param name="values">要寫入的 key/value。</param>
    /// <exception cref="Win32Exception">寫入失敗時拋出（由 Win32 last error 轉換）。</exception>
    public void SaveValues(string section, IReadOnlyDictionary<string, string?> values)
    {
        lock (_writeSync)
        {
            _suppressChanged = true;
            try
            {
                EnsureIniFileEncoding(_iniFilePath);

                foreach (var entry in values)
                {
                    if (!WritePrivateProfileString(section, entry.Key, entry.Value ?? string.Empty, _iniFilePath))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }

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
}

