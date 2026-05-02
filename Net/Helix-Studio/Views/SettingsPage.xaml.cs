using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Helix_Studio.Model.Workspace;
using Helix_Studio.ViewModels;
using WinForms = System.Windows.Forms;

namespace Helix_Studio.Views;

/// <summary>應用程式設定分頁：一般、顯示、工作區、模組、日誌與關於等區塊。</summary>
public partial class SettingsPage : UserControl
{
    private readonly SettingsPageViewModel _viewModel;

    /// <summary>建立分頁、繫結 <see cref="SettingsPageViewModel"/> 並註冊區段導向與匯出／法律聲明回呼。</summary>
    public SettingsPage()
    {
        InitializeComponent();

        _viewModel = new SettingsPageViewModel();
        DataContext = _viewModel;

        Loaded += SettingsPage_ViewReady;
        Unloaded += SettingsPage_ViewLeaving;

        _viewModel.SectionRequested += OnSectionRequested;
        _viewModel.LogExported += OnLogArchiveCreated;
        _viewModel.LegalNoticesOpened += OnLegalNoticesOpened;
        _viewModel.WorkspaceOperationFailed += OnWorkspaceOperationFailed;
    }

    private void SettingsPage_ViewReady(object sender, RoutedEventArgs e)
        => _viewModel.LoadedCommand.Execute(null);

    private void SettingsPage_ViewLeaving(object sender, RoutedEventArgs e)
    {
        _viewModel.SectionRequested -= OnSectionRequested;
        _viewModel.LogExported -= OnLogArchiveCreated;
        _viewModel.LegalNoticesOpened -= OnLegalNoticesOpened;
        _viewModel.WorkspaceOperationFailed -= OnWorkspaceOperationFailed;
        _viewModel.Dispose();
    }

    private void OnSectionRequested(string tag)
    {
        FrameworkElement? target = tag switch
        {
            "General"    => SectionGeneral,
            "Display"    => SectionDisplay,
            "Workspaces" => SectionWorkspaces,
            "Modules"    => SectionModules,
            "Log"        => SectionLog,
            "About"      => SectionAbout,
            _            => null,
        };
        target?.BringIntoView();
    }

    private void OnLogArchiveCreated(Helix_Studio.Model.Settings.LogExportResult result)
    {
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.ArchivePath))
        {
            MessageBox.Show(
                $"已輸出日誌壓縮檔：\n{result.ArchivePath}",
                "日誌匯出",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            $"日誌匯出失敗：\n{result.ErrorMessage}",
            "日誌匯出",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnLegalNoticesOpened(Helix_Studio.Model.Settings.LegalNoticeResult result)
    {
        if (result.IsFound) return;

        MessageBox.Show(
            result.Message ?? "找不到法律聲明檔案。",
            "Legal Notices",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void OnWorkspaceOperationFailed(string message)
    {
        MessageBox.Show(
            message,
            "工作區管理",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        var tag = rb.Tag as string;
        if (string.IsNullOrWhiteSpace(tag)) return;
        OnSectionRequested(tag);
    }

    private void BrowseLogFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "選擇日誌匯出資料夾",
            ShowNewFolderButton = true,
            SelectedPath = _viewModel.LogExportFolder,
        };

        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.SelectedPath)) return;

        _viewModel.LogExportFolder = dlg.SelectedPath;
    }

    private void NewWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WorkspaceDialog(Window.GetWindow(this));
        if (dlg.ShowDialog() != true) return;

        _viewModel.CreateWorkspaceCommand.Execute(
            new WorkspaceModel { Name = dlg.ResultName, Description = dlg.ResultDesc });
    }

    private void EditWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not WorkspaceModel ws) return;

        var dlg = new WorkspaceDialog(Window.GetWindow(this), ws.Name, ws.Description);
        if (dlg.ShowDialog() != true) return;

        _viewModel.UpdateWorkspaceCommand.Execute(
            new WorkspaceModel
            {
                Id = ws.Id,
                Name = dlg.ResultName,
                Description = dlg.ResultDesc,
            });
    }

    private void DeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not WorkspaceModel ws) return;

        var confirm = MessageBox.Show(
            $"刪除工作區「{ws.Name}」？\n此操作無法復原。",
            "確認刪除",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK) return;

        _viewModel.DeleteWorkspaceCommand.Execute(ws.Id);
    }

    private void SettingsSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsSearchPopupOpen) return;

        if (e.Key == Key.Down)
        {
            var next = SettingsSearchSuggestList.SelectedIndex + 1;
            if (next >= SettingsSearchSuggestList.Items.Count) next = SettingsSearchSuggestList.Items.Count - 1;
            SettingsSearchSuggestList.SelectedIndex = next;
            SettingsSearchSuggestList.ScrollIntoView(SettingsSearchSuggestList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            var prev = SettingsSearchSuggestList.SelectedIndex - 1;
            if (prev < 0) prev = 0;
            SettingsSearchSuggestList.SelectedIndex = prev;
            SettingsSearchSuggestList.ScrollIntoView(SettingsSearchSuggestList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && SettingsSearchSuggestList.SelectedItem is Helix_Studio.Model.Settings.SettingsSearchSuggestion sug)
        {
            _viewModel.AcceptSearchSuggestionCommand.Execute(sug);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _viewModel.IsSearchPopupOpen = false;
            e.Handled = true;
        }
    }

    private void SettingsSearchSuggestList_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (SettingsSearchSuggestList.SelectedItem is Helix_Studio.Model.Settings.SettingsSearchSuggestion sug)
            _viewModel.AcceptSearchSuggestionCommand.Execute(sug);
    }

    private void DataDensity_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is not int index) return;
        _viewModel.SetDataDensityCommand.Execute(index);
    }
}
