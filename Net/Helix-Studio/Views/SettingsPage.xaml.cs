using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Helix_Studio.AppRuntime;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Helix_Studio.Views;

/// <summary>
/// 設定頁。
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly Dictionary<string, FrameworkElement> _sectionMap;
    private readonly LocalizationRuntime _localizationRuntime;

    /// <summary>
    /// 建立設定頁並綁定 ViewModel。
    /// </summary>
    public SettingsPage()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        _localizationRuntime = provider.GetRequiredService<LocalizationRuntime>();
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
    }

    /// <summary>
    /// Scrolls the settings view to the requested section tag.
    /// </summary>
    public void ApplySectionTag(string sectionTag)
    {
        if (string.IsNullOrWhiteSpace(sectionTag))
        {
            return;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => ScrollToSection(sectionTag)));
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
            Title = _localizationRuntime.QueryString("Loc_Settings_LogFolderDialogTitle"),
        };

        if (dialog.ShowDialog() == true)
        {
            var selected = dialog.FolderName ?? string.Empty;
            viewModel.ApplyLogExportFolder(selected);
        }
    }

    private async void CreateWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        var confirmed = await WorkspaceCreateDialog.ShowDialogAsync(
            _localizationRuntime.QueryString("Loc_Settings_WorkspaceCreateDialogTitle"),
            _localizationRuntime.QueryString("Loc_Settings_WorkspaceCreateDialogBody"),
            _localizationRuntime.QueryString("Loc_Settings_WorkspaceCreateDialogName"),
            _localizationRuntime.QueryString("Loc_Settings_WorkspaceCreateDialogDescription"),
            _localizationRuntime.QueryString("Loc_Common_Create"),
            _localizationRuntime.QueryString("Loc_Common_Cancel")).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        viewModel.ApplyCreateWorkspaceNow(WorkspaceCreateDialog.Input1Text, WorkspaceCreateDialog.Input2Text);
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
