using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Helix_Studio.Core;

/// <summary>主題與語系資源套用核心。</summary>
public sealed class ThemeCore
{
    private static readonly Uri[] ThemeResources =
    [
        new("pack://application:,,,/Themes/ShellColors.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellColors.Dark.xaml", UriKind.Absolute),
    ];

    private static readonly Uri[] LocaleResources =
    [
        new("pack://application:,,,/Themes/Locale/Strings.en.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/Locale/Strings.zh-Hant.xaml", UriKind.Absolute),
    ];

    private static readonly Uri[] ShellResources =
    [
        new("pack://application:,,,/Themes/ShellChromeStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellTypography.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellScrollStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellSettingsStyles.xaml", UriKind.Absolute),
        new("pack://application:,,,/Themes/ShellMarkdownStyles.xaml", UriKind.Absolute),
    ];

    private static readonly HashSet<Uri> AllResources =
        ShellResources
            .Concat(ThemeResources)
            .Concat(LocaleResources)
            .ToHashSet();

    /// <summary>套用主題與語系資源。</summary>
    /// <param name="themeIndex">主題索引。</param>
    /// <param name="languageIndex">語系索引。</param>
    internal void ApplyAppearance(int themeIndex, int languageIndex)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var removeTargets = merged
            .Where(dictionary => dictionary.Source is not null && AllResources.Contains(dictionary.Source))
            .ToList();

        removeTargets.ForEach(dictionary => merged.Remove(dictionary));

        foreach (var uri in ShellResources)
        {
            merged.Add(new ResourceDictionary { Source = uri });
        }

        merged.Add(new ResourceDictionary { Source = ThemeResources[themeIndex] });
        merged.Add(new ResourceDictionary { Source = LocaleResources[languageIndex] });
    }
}

