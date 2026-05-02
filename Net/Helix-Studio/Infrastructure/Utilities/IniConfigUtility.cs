using System.IO;
using Helix_Studio.Model.Configuration;

namespace Helix_Studio.Infrastructure.Utilities;

/// <summary>以 INI 檔讀寫啟動用 UI 文化與主題組態。</summary>
public sealed class IniConfigUtility
{
    private const string UiSection = "Ui";
    private const string CultureKey = "Culture";
    private const string ThemeKey = "Theme";

    private readonly string _configFilePath;
    private AppConfig? _current;

    /// <summary>建立工具；未指定路徑時使用 <see cref="AppPathUtility.QueryConfigFilePath"/>。</summary>
    public IniConfigUtility(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? AppPathUtility.QueryConfigFilePath();
    }

    /// <summary>查詢目前組態（含快取）；檔案不存在時建立預設檔。</summary>
    public AppConfig QueryCurrent()
    {
        if (_current is not null)
        {
            return _current;
        }

        if (!File.Exists(_configFilePath))
        {
            _current = AppConfig.CreateDefault();
            Save(_current);
            return _current;
        }

        _current = QueryConfigFromDisk();
        return _current;
    }

    /// <summary>更新文化名稱並儲存。</summary>
    public AppConfig SaveUiCulture(string cultureName)
    {
        var config = QueryCurrent();
        config.Ui.CultureName = AppCultureNames.Normalize(cultureName);
        return Save(config);
    }

    /// <summary>更新主題模式並儲存。</summary>
    public AppConfig SaveTheme(AppThemeMode themeMode)
    {
        var config = QueryCurrent();
        config.Ui.ThemeMode = themeMode;
        return Save(config);
    }

    /// <summary>正規化並寫入完整組態至 INI 檔。</summary>
    public AppConfig Save(AppConfig config)
    {
        var normalizedConfig = new AppConfig
        {
            Ui = new UiConfig
            {
                CultureName = AppCultureNames.Normalize(config.Ui.CultureName),
                ThemeMode = config.Ui.ThemeMode
            }
        };

        _current = normalizedConfig;

        var sections = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [UiSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [CultureKey] = normalizedConfig.Ui.CultureName,
                [ThemeKey] = normalizedConfig.Ui.ThemeMode.ToString()
            }
        };

        File.WriteAllText(_configFilePath, IniDocumentUtility.Serialize(sections));
        return normalizedConfig;
    }

    private AppConfig QueryConfigFromDisk()
    {
        var config = AppConfig.CreateDefault();
        var sections = IniDocumentUtility.Parse(File.ReadAllText(_configFilePath));

        if (sections.TryGetValue(UiSection, out var uiSection))
        {
            if (uiSection.TryGetValue(CultureKey, out var cultureName))
            {
                config.Ui.CultureName = AppCultureNames.Normalize(cultureName);
            }

            if (uiSection.TryGetValue(ThemeKey, out var themeName)
                && Enum.TryParse(themeName, ignoreCase: true, out AppThemeMode themeMode))
            {
                config.Ui.ThemeMode = themeMode;
            }
        }

        return config;
    }
}
