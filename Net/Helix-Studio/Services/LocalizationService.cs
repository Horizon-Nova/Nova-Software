using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace Helix_Studio.Services;

/// <summary>
/// 介面語言服務：以抽換 Application 層 MergedDictionaries 達成執行期即時切換語言。
/// 純文字由 XAML 端以 DynamicResource 綁定，抽換字典即自動更新；
/// 轉換器與程式碼端文字以 GetString(key) 在顯示當下取值，並透過 LanguageChanged 通知刷新。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private static readonly HashSet<string> Supported = new() { "zh-TW", "en-US", "zh-CN" };
    private const string Fallback = "zh-TW";

    private ResourceDictionary? _current;

    private LocalizationService() { }

    /// <summary>目前生效的語言標籤（zh-TW / en-US / zh-CN）。</summary>
    public string CurrentLanguage { get; private set; } = Fallback;

    /// <summary>語言版本序號，語言每次變更時遞增，供需要時觸發重算。</summary>
    public int Version { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>語言變更後觸發，供 ViewModel 重整使用轉換器產生文字的視圖。</summary>
    public event Action? LanguageChanged;

    /// <summary>套用語言：抽換語言字典、設定執行緒文化，並通知變更。不支援的標籤回退預設值。</summary>
    public void SetLanguage(string? tag)
    {
        var lang = tag is not null && Supported.Contains(tag) ? tag : Fallback;

        var dict = new ResourceDictionary
        {
            Source = new Uri($"Resources/Lang/Strings.{lang}.xaml", UriKind.Relative),
        };

        var app = Application.Current;
        if (app is not null)
        {
            if (_current is not null)
                app.Resources.MergedDictionaries.Remove(_current);
            app.Resources.MergedDictionaries.Add(dict);
            _current = dict;
        }

        CurrentLanguage = lang;

        var culture = CultureInfo.GetCultureInfo(lang);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Version++;
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(CurrentLanguage));
        LanguageChanged?.Invoke();
    }

    /// <summary>取得目前語言下指定 key 的字串；找不到時回傳 key 本身（便於發現漏填）。</summary>
    public string GetString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>取得字串並套用格式參數。</summary>
    public string Format(string key, params object?[] args)
        => string.Format(GetString(key), args);

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
