using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExternalModules.InSpecTool.Core;
using ExternalModules.InSpecTool.Utilities;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Win32;

namespace ExternalModules.InSpecTool.Views;

public partial class InSpecToolSettingsView : UserControl
{
    private IShellContext? _context;
    private bool _isLoading;

    public ObservableCollection<InSpecSettingsFieldRow> FieldRows { get; } = [];

    public IReadOnlyList<string> TargetModeOptions { get; } = InSpecSettingsViewUtility.QueryTargetModes();

    public InSpecToolSettingsView()
    {
        InitializeComponent();
        DataContext = this;
        ScanIntervalCombo.ItemsSource = InSpecSettingsViewUtility.QueryScanIntervalOptions();
        MonitorIntervalCombo.ItemsSource = InSpecSettingsViewUtility.QueryMonitorIntervalOptions();
        FieldRowsItemsControl.ItemsSource = FieldRows;
    }

    public void ApplyHostContext(IShellContext context)
    {
        _context = context;
        LoadSettings();
    }

    public void ClearHostContext()
    {
        _context = null;
        FieldRows.Clear();
        StatusTextBlock.Text = string.Empty;
    }

    private void LoadSettings()
    {
        if (_context is null)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var settings = _context.Settings;
            ScanIntervalCombo.SelectedItem = InSpecSettingsViewUtility.QuerySavedValue(settings, "scanIntervalMs", "80");
            MonitorIntervalCombo.SelectedItem = InSpecSettingsViewUtility.QuerySavedValue(settings, "monitorIntervalMs", "500");
            EnableAutoMonitoringCheckBox.IsChecked = InSpecSettingsViewUtility.QuerySavedBoolean(settings, "enableAutoMonitoring", true);
            EnableAutoRunCheckBox.IsChecked = InSpecSettingsViewUtility.QuerySavedBoolean(settings, "enableAutoRun", true);
            SearchDirectoryTextBox.Text = InSpecSettingsViewUtility.QuerySavedValue(settings, "searchDirectory");
            SystemLogDirectoryTextBox.Text = InSpecSettingsViewUtility.QuerySavedValue(settings, "systemLogDirectory");
            BackupSavePathTextBox.Text = InSpecSettingsViewUtility.QuerySavedValue(settings, "backupSavePath");

            FieldRows.Clear();
            foreach (var row in InSpecSettingsViewUtility.QueryFieldRows(settings))
            {
                FieldRows.Add(row);
            }

            StatusTextBlock.Text = "已載入目前模組設定。";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ScanIntervalCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ScanIntervalCombo.SelectedItem is not string value)
        {
            return;
        }

        SaveSetting("scanIntervalMs", value, "已更新條碼輸入延遲。");
    }

    private void MonitorIntervalCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || MonitorIntervalCombo.SelectedItem is not string value)
        {
            return;
        }

        SaveSetting("monitorIntervalMs", value, "已更新監控輪詢間隔。");
    }

    private void EnableAutoMonitoringCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveBooleanSetting("enableAutoMonitoring", EnableAutoMonitoringCheckBox.IsChecked == true, "已更新自動監控設定。");
    }

    private void EnableAutoRunCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveBooleanSetting("enableAutoRun", EnableAutoRunCheckBox.IsChecked == true, "已更新自動執行設定。");
    }

    private void SearchDirectoryTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SaveSetting("searchDirectory", SearchDirectoryTextBox.Text, "已更新搜尋目錄。");

    private void SystemLogDirectoryTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SaveSetting("systemLogDirectory", SystemLogDirectoryTextBox.Text, "已更新系統記錄目錄。");

    private void BackupSavePathTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SaveSetting("backupSavePath", BackupSavePathTextBox.Text, "已更新備援儲存目錄。");

    private void DirectoryTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (sender == SearchDirectoryTextBox)
        {
            SaveSetting("searchDirectory", SearchDirectoryTextBox.Text, "已更新搜尋目錄。");
        }
        else if (sender == SystemLogDirectoryTextBox)
        {
            SaveSetting("systemLogDirectory", SystemLogDirectoryTextBox.Text, "已更新系統記錄目錄。");
        }
        else if (sender == BackupSavePathTextBox)
        {
            SaveSetting("backupSavePath", BackupSavePathTextBox.Text, "已更新備援儲存目錄。");
        }
    }

    private void SearchDirectoryBrowseButton_OnClick(object sender, RoutedEventArgs e) =>
        BrowseDirectory(SearchDirectoryTextBox, "searchDirectory", "選擇搜尋目錄", "已更新搜尋目錄。");

    private void SystemLogDirectoryBrowseButton_OnClick(object sender, RoutedEventArgs e) =>
        BrowseDirectory(SystemLogDirectoryTextBox, "systemLogDirectory", "選擇系統記錄目錄", "已更新系統記錄目錄。");

    private void BackupSavePathBrowseButton_OnClick(object sender, RoutedEventArgs e) =>
        BrowseDirectory(BackupSavePathTextBox, "backupSavePath", "選擇備援儲存目錄", "已更新備援儲存目錄。");

    private void AddFieldButton_OnClick(object sender, RoutedEventArgs e)
    {
        FieldRows.Add(new InSpecSettingsFieldRow
        {
            TargetMode = InSpecFieldDefinition.DirectInputTargetMode,
            IsEditable = true
        });
        StatusTextBlock.Text = "已新增欄位列，記得按「儲存欄位結構」。";
    }

    private void DeleteFieldButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not InSpecSettingsFieldRow row)
        {
            return;
        }

        FieldRows.Remove(row);
        StatusTextBlock.Text = "已移除欄位列，記得按「儲存欄位結構」。";
    }

    private void SaveFieldSchemaButton_OnClick(object sender, RoutedEventArgs e)
    {
        var payload = InSpecSettingsViewUtility.SerializeFieldRows(FieldRows);
        SaveSetting("fieldSchema", payload, "已更新欄位結構。");
    }

    private void BrowseDirectory(TextBox targetTextBox, string settingKey, string title, string successMessage)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = targetTextBox.Text
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        targetTextBox.Text = dialog.FolderName;
        SaveSetting(settingKey, dialog.FolderName, successMessage);
    }

    private void SaveSetting(string key, string? value, string successMessage)
    {
        if (_context is null || _isLoading)
        {
            return;
        }

        var result = InSpecSettingsViewUtility.SaveString(_context.Settings, key, value);
        if (!result.IsSuccess)
        {
            StatusTextBlock.Text = $"儲存失敗：{result.DetailText}";
            return;
        }

        StatusTextBlock.Text = successMessage;
    }

    private void SaveBooleanSetting(string key, bool value, string successMessage)
    {
        if (_context is null || _isLoading)
        {
            return;
        }

        var result = InSpecSettingsViewUtility.SaveBoolean(_context.Settings, key, value);
        if (!result.IsSuccess)
        {
            StatusTextBlock.Text = $"儲存失敗：{result.DetailText}";
            return;
        }

        StatusTextBlock.Text = successMessage;
    }

    private void Root_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var parentScrollViewer = QueryAncestorScrollViewer(this);
        if (parentScrollViewer is null)
        {
            return;
        }

        parentScrollViewer.ScrollToVerticalOffset(parentScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? QueryAncestorScrollViewer(DependencyObject? source)
    {
        var current = VisualTreeHelper.GetParent(source);
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
