using System.Globalization;
using System.Windows;
using Helix_Studio.Model.Configuration;
using Helix_Studio.Infrastructure.Extensions;
using Helix_Studio.Services;

namespace Helix_Studio.Infrastructure.Localization;

/// <summary>介面字串資源字典與文化切換。</summary>
public static class UiLocale
{
    /// <summary>繁體中文文化常數。</summary>
    public const string ZhHant = AppCultureNames.ZhHant;

    /// <summary>英文文化常數。</summary>
    public const string En = AppCultureNames.En;

    private static ResourceDictionary? _stringsDictionary;

    /// <summary>目前作用中之文化名稱。</summary>
    public static string CurrentName { get; private set; } = ZhHant;

    /// <summary>文化或字串字典變更時引發。</summary>
    public static event EventHandler? Changed;

    /// <summary>套用指定文化並可選擇是否持久化；會載入對應之字串資源字典。</summary>
    public static void Apply(string cultureName, bool persist)
    {
        cultureName = AppCultureNames.Normalize(cultureName);

        var merged = Application.Current!.Resources.MergedDictionaries;
        merged.RemoveBySourceMatch("/Themes/Locale/Strings.");
        _stringsDictionary = null;

        var pack = cultureName == En
            ? "pack://application:,,,/Themes/Locale/Strings.en.xaml"
            : "pack://application:,,,/Themes/Locale/Strings.zh-Hant.xaml";

        _stringsDictionary = new ResourceDictionary { Source = new Uri(pack, UriKind.Absolute) };
        merged.Add(_stringsDictionary);

        CurrentName = cultureName;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

        if (persist)
        {
            AppServices.Config.SaveUiCulture(cultureName);
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>自應用程式資源查詢字串資源鍵對應之文字。</summary>
    public static string QueryString(string key) =>
        (string)Application.Current!.FindResource(key);
}
