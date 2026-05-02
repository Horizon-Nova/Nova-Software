using System.Windows.Controls;
using System.Windows.Input;
using ExternalModules.DeploymentTool.Utilities;
using HN_Studio.ModuleSdk;

namespace ExternalModules.DeploymentTool.Views;

public partial class DeploymentToolSettingsView : UserControl, IHnModuleSettingsView
{
    private HnModuleContext? _context;
    private bool _isLoading;

    public DeploymentToolSettingsView()
    {
        InitializeComponent();
        TemplateComboBox.ItemsSource = DeploymentToolSettingsViewUtility.QueryLinuxTemplateOptions();
        DefaultCpuComboBox.ItemsSource = DeploymentToolSettingsViewUtility.QueryDefaultCpuOptions();
        DefaultMemoryComboBox.ItemsSource = DeploymentToolSettingsViewUtility.QueryDefaultMemoryOptions();
        DockerModeComboBox.ItemsSource = DeploymentToolSettingsViewUtility.QueryDockerModeOptions();
    }

    public void ApplyHostContext(HnModuleContext context)
    {
        _context = context;
        LoadSettings();
    }

    public void ClearHostContext()
    {
        _context = null;
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
            TemplateComboBox.SelectedItem = DeploymentToolSettingsViewUtility.QuerySavedValue(
                settings,
                "linuxTemplate",
                "Ubuntu 24.04 + Docker Base");
            DefaultCpuComboBox.SelectedItem = DeploymentToolSettingsViewUtility.QuerySavedValue(settings, "defaultCpuCores", "4");
            DefaultMemoryComboBox.SelectedItem = DeploymentToolSettingsViewUtility.QuerySavedValue(settings, "defaultMemoryMb", "8192");
            DockerModeComboBox.SelectedItem = DeploymentToolSettingsViewUtility.QuerySavedValue(settings, "dockerMode", "docker compose");
            WorkspaceTextBox.Text = DeploymentToolSettingsViewUtility.QuerySavedValue(settings, "workspaceDirectory");
            AutoStartCheckBox.IsChecked = DeploymentToolSettingsViewUtility.QuerySavedBoolean(settings, "autoStartAfterCreate", true);
            StatusTextBlock.Text = "部署工具設定已載入。";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void TemplateComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || TemplateComboBox.SelectedItem is not string value)
        {
            return;
        }

        SaveString("linuxTemplate", value, "已更新 Linux 模板。");
    }

    private void DefaultCpuComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DefaultCpuComboBox.SelectedItem is not string value)
        {
            return;
        }

        SaveString("defaultCpuCores", value, "已更新預設 vCPU。");
    }

    private void DefaultMemoryComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DefaultMemoryComboBox.SelectedItem is not string value)
        {
            return;
        }

        SaveString("defaultMemoryMb", value, "已更新預設 RAM。");
    }

    private void DockerModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DockerModeComboBox.SelectedItem is not string value)
        {
            return;
        }

        SaveString("dockerMode", value, "已更新 Docker 模式。");
    }

    private void WorkspaceTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SaveString("workspaceDirectory", WorkspaceTextBox.Text, "已更新專案工作目錄。");

    private void AutoStartCheckBox_OnChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveBoolean("autoStartAfterCreate", AutoStartCheckBox.IsChecked == true, "已更新建立後自動啟動設定。");
    }

    private void SaveString(string key, string? rawValue, string successMessage)
    {
        if (_context is null || _isLoading)
        {
            return;
        }

        var result = DeploymentToolSettingsViewUtility.SaveString(_context.Settings, key, rawValue);
        StatusTextBlock.Text = result.IsSuccess
            ? successMessage
            : $"儲存失敗：{result.DetailText}";
    }

    private void SaveBoolean(string key, bool value, string successMessage)
    {
        if (_context is null || _isLoading)
        {
            return;
        }

        var result = DeploymentToolSettingsViewUtility.SaveBoolean(_context.Settings, key, value);
        StatusTextBlock.Text = result.IsSuccess
            ? successMessage
            : $"儲存失敗：{result.DetailText}";
    }
}
