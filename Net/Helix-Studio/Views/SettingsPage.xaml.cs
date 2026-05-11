using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Helix_Studio.Views;

/// <summary>
/// 設定頁面的檢視層。
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly Dictionary<string, FrameworkElement> _sectionMap;

    /// <summary>
    /// 建立設定頁面並接上 ViewModel。
    /// </summary>
    public SettingsPage()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<SettingsPageViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
        _sectionMap = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["General"] = SectionGeneral,
            ["Display"] = SectionDisplay,
            ["Workspaces"] = SectionWorkspaces,
            ["Modules"] = SectionModules,
            ["Log"] = SectionLog,
            ["About"] = SectionAbout,
        };
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Unloaded -= OnUnloaded;
    }

    private void SettingsSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        viewModel.ApplySelectedSearchSuggestion();
        ScrollToSection(viewModel.QuerySelectedSectionTag());
        e.Handled = true;
    }

    private void SettingsSearchSuggestList_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        viewModel.ApplySelectedSearchSuggestion();
        ScrollToSection(viewModel.QuerySelectedSectionTag());
    }

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string sectionTag })
        {
            ScrollToSection(sectionTag);
        }
    }

    private void BrowseLogFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            InitialDirectory = viewModel.LogExportFolder,
            Title = "選取日誌匯出資料夾",
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.ApplyLogExportFolder(dialog.FolderName);
        }
    }

    private void DataDensity_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string tag } || DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        viewModel.DataDensityIndex = int.Parse(tag, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ScrollToSection(string sectionTag)
    {
        if (!_sectionMap.TryGetValue(sectionTag, out var section))
        {
            return;
        }

        var targetPoint = section.TranslatePoint(new Point(0, 0), ContentScroller);
        ContentScroller.ScrollToVerticalOffset(ContentScroller.VerticalOffset + targetPoint.Y);
    }
}
