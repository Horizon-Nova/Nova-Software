namespace Helix_Studio.Model.Configuration;

/// <summary>應用程式層級組態（啟動載入／持久化）。</summary>
public sealed class AppConfig
{
    /// <summary>使用者介面相關組態。</summary>
    public UiConfig Ui { get; init; } = new();

    /// <summary>建立預設組態實例。</summary>
    public static AppConfig CreateDefault() => new();
}

/// <summary>介面文化、主題等 UI 組態。</summary>
public sealed class UiConfig
{
    /// <summary>文化名稱（例如 zh-Hant）。</summary>
    public string CultureName { get; set; } = AppCultureNames.ZhHant;

    /// <summary>主題模式。</summary>
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;
}

/// <summary>支援之應用程式文化名稱常數與正規化。</summary>
public static class AppCultureNames
{
    /// <summary>繁體中文文化名稱。</summary>
    public const string ZhHant = "zh-Hant";

    /// <summary>英文文化名稱。</summary>
    public const string En = "en";

    /// <summary>將輸入文化名稱正規化為支援值之一。</summary>
    public static string Normalize(string? cultureName) =>
        cultureName switch
        {
            En => En,
            _ => ZhHant
        };
}

/// <summary>應用程式主題模式。</summary>
public enum AppThemeMode
{
    /// <summary>深色主題。</summary>
    Dark,

    /// <summary>淺色主題。</summary>
    Light,

    /// <summary>遵循作業系統。</summary>
    System
}
