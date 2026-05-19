using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;
using Microsoft.Win32;

namespace Helix_Studio.AppHost;

/// <summary>
/// 外觀 Runtime。
///
/// 主旨：
/// - 依據 INI 的外觀設定，套用到 WPF 全域資源字典（<see cref="Application.Resources"/>）。
///
/// 注意：
/// - 這裡讀取的 <c>[Settings]</c> 是「INI section 名稱」，用來存放外觀欄位（主題/語系/字型縮放/資料密度）。
/// - <c>[Settings]</c> 不代表「設定頁」或「Settings 功能模組」；此類別不依賴任何 Settings UI/Domain/ViewModel。
///
/// 原則：
/// - 此類別只負責「外觀套用」；不負責寫入 INI，也不提供跨模組通知線路。
/// - 外觀生效依賴 WPF 資源系統的自然行為；必要時以全域 invalidation 促使版面系統重算。
/// </summary>
public sealed class AppearanceRuntime : IDisposable
{
    private const string SettingsSection = "Settings";

    private static readonly Uri[] ThemeResources =
    [
        new("pack://application:,,,/Themes/ShellColors.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellColors.Dark.xaml", UriKind.Absolute),
    ];

    private static readonly Uri[] LocaleResources =
    [
        new("pack://application:,,,/Themes/Locale/Strings.zh-Hant.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/Locale/Strings.en.xaml", UriKind.Absolute),
    ];

    private static readonly Uri DefaultMetricsResource =
        new("pack://application:,,,/Themes/Ui/ShellMetrics.Default.xaml", UriKind.Absolute);

    private static readonly Uri[] FontScaleResources =
    [
        new("pack://application:,,,/Themes/Ui/ShellMetrics.Scale90.xaml", UriKind.Absolute),
        DefaultMetricsResource,
        new("pack://application:,,,/Themes/Ui/ShellMetrics.Scale110.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/Ui/ShellMetrics.Scale125.xaml", UriKind.Absolute),
    ];

    private static readonly Uri[] DataDensityResources =
    [
        new("pack://application:,,,/Themes/Ui/ShellMetrics.DensityComfortable.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/Ui/ShellMetrics.DensityCompact.xaml", UriKind.Absolute),
    ];

    private static readonly Uri[] ShellResources =
    [
        new("pack://application:,,,/Themes/ShellChromeStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellTypography.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellScrollStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellSettingsStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellMarkdownStyles.xaml", UriKind.Absolute),
    ];

    private static readonly HashSet<Uri> ManagedResources =
        ShellResources
            .Concat(ThemeResources)
            .Concat(LocaleResources)
            .Concat(FontScaleResources)
            .Concat(DataDensityResources)
            .Append(DefaultMetricsResource)
            .ToHashSet();

    private readonly ApplicationRuntime _applicationRuntime;
    private readonly IniRepository _iniRepository;

    private int _appliedThemeIndex = -1;
    private int _appliedLanguageIndex = -1;
    private int _appliedFontScaleIndex = -1;
    private int _appliedDataDensityIndex = -1;

    /// <summary>
    /// 建立外觀 Runtime，並訂閱 INI 變動。
    /// </summary>
    public AppearanceRuntime(ApplicationRuntime applicationRuntime, IniRepository iniRepository)
    {
        _applicationRuntime = applicationRuntime;
        _iniRepository = iniRepository;
        _iniRepository.Changed += OnIniChanged;
        RequestApplyAppearance();
    }

    /// <summary>
    /// 依 INI 目前快照套用外觀。
    /// </summary>
    public void ApplyAppearance() => RequestApplyAppearance();

    private void OnIniChanged(object? sender, EventArgs e) => RequestApplyAppearance();

    private void RequestApplyAppearance()
        => _applicationRuntime.InvokeOnUiThread(() =>
            ApplyAppearance(_iniRepository.QuerySnapshot<SettingsModel>(SettingsSection)));

    private void ApplyAppearance(SettingsModel settings)
    {
        var themeIndex = settings.ThemeIndex;
        var languageIndex = Math.Clamp(settings.LanguageIndex, 0, LocaleResources.Length - 1);
        var fontScaleIndex = Math.Clamp(settings.FontScaleIndex, 0, FontScaleResources.Length - 1);
        var dataDensityIndex = Math.Clamp(settings.DataDensityIndex, 0, DataDensityResources.Length - 1);

        if (themeIndex == _appliedThemeIndex
            && languageIndex == _appliedLanguageIndex
            && fontScaleIndex == _appliedFontScaleIndex
            && dataDensityIndex == _appliedDataDensityIndex)
        {
            return;
        }

        _appliedThemeIndex = themeIndex;
        _appliedLanguageIndex = languageIndex;
        _appliedFontScaleIndex = fontScaleIndex;
        _appliedDataDensityIndex = dataDensityIndex;

        var merged = Application.Current.Resources.MergedDictionaries;
        merged
            .Where(dictionary => dictionary.Source is not null && ManagedResources.Contains(dictionary.Source))
            .ToList()
            .ForEach(dictionary => merged.Remove(dictionary));

        merged.Add(new ResourceDictionary { Source = DefaultMetricsResource });
        merged.Add(new ResourceDictionary { Source = FontScaleResources[fontScaleIndex] });
        merged.Add(new ResourceDictionary { Source = DataDensityResources[dataDensityIndex] });
        merged.Add(new ResourceDictionary { Source = ResolveThemeResource(themeIndex) });

        foreach (var uri in ShellResources)
        {
            merged.Add(new ResourceDictionary { Source = uri });
        }

        merged.Add(new ResourceDictionary { Source = LocaleResources[languageIndex] });

        InvalidateApplication();
    }

    public void Dispose() => _iniRepository.Changed -= OnIniChanged;

    private static Uri ResolveThemeResource(int themeIndex)
    {
        var normalizedIndex = themeIndex switch
        {
            0 => 0,
            1 => 1,
            _ => QuerySystemThemeIndex(),
        };

        return ThemeResources[Math.Clamp(normalizedIndex, 0, ThemeResources.Length - 1)];
    }

    private static int QuerySystemThemeIndex()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme") as int?;
            return value == 0 ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void InvalidateApplication()
    {
        foreach (Window window in Application.Current.Windows)
        {
            InvalidateVisualTree(window);
        }
    }

    private static void InvalidateVisualTree(DependencyObject node)
    {
        if (node is UIElement element)
        {
            element.InvalidateMeasure();
            element.InvalidateArrange();
            element.InvalidateVisual();
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            InvalidateVisualTree(VisualTreeHelper.GetChild(node, i));
        }
    }
}

