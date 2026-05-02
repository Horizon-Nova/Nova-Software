using Microsoft.Win32;
using System.Windows;
using Helix_Studio.Model.Configuration;
using Helix_Studio.Services;

namespace Helix_Studio.Infrastructure.Localization;

/// <summary>殼層主題色彩字典之載入與系統主題偵測。</summary>
public static class UiTheme
{
    /// <summary>目前實際套用之主題模式（系統模式會解析為淺／深）。</summary>
    public static AppThemeMode CurrentMode { get; private set; } = AppThemeMode.Light;

    /// <summary>主題變更時引發。</summary>
    public static event EventHandler? Changed;

    /// <summary>套用主題模式並可選擇是否寫入組態；支援跟隨系統。</summary>
    public static void Apply(AppThemeMode themeMode, bool persist)
    {
        var applied = themeMode == AppThemeMode.System ? QuerySystemThemeMode() : themeMode;

        var pack = applied == AppThemeMode.Dark
            ? "pack://application:,,,/Themes/ShellColors.Dark.xaml"
            : "pack://application:,,,/Themes/ShellColors.xaml";

        ReplaceThemeColorsDictionary(pack);

        CurrentMode = applied;

        if (persist)
        {
            AppServices.Config.SaveTheme(themeMode);
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void ReplaceThemeColorsDictionary(string packUri)
    {
        var app = Application.Current;
        if (app is null) return;

        var merged = app.Resources.MergedDictionaries;
        RemoveBySourceMatch(merged, "/Themes/ShellColors");
        merged.Insert(0, new ResourceDictionary { Source = new Uri(packUri, UriKind.Absolute) });
    }

    private static void RemoveBySourceMatch(
        System.Collections.ObjectModel.Collection<ResourceDictionary> dictionaries,
        string pathFragment)
    {
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.ToString();
            if (source?.Contains(pathFragment, StringComparison.OrdinalIgnoreCase) == true)
            {
                dictionaries.RemoveAt(i);
            }
        }
    }

    private static AppThemeMode QuerySystemThemeMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var appsUseLightTheme = key?.GetValue("AppsUseLightTheme") as int?;
            if (appsUseLightTheme.HasValue)
                return appsUseLightTheme.Value == 0 ? AppThemeMode.Dark : AppThemeMode.Light;
        }
        catch
        {
        }

        return AppThemeMode.Light;
    }
}
