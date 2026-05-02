using System;
using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;
using WinForms = System.Windows.Forms;

namespace Helix_Studio.Views;

/// <summary>模組目錄分頁：匯入、掃描、開啟、停用與解除安裝之 UI 轉接。</summary>
public partial class ModulesPage : UserControl
{
    private readonly ModulesPageViewModel _vm;

    /// <summary>建立分頁與 <see cref="ModulesPageViewModel"/>，並將開啟模組導向主視窗導覽。</summary>
    public ModulesPage()
    {
        InitializeComponent();
        _vm = new ModulesPageViewModel();
        DataContext = _vm;

        _vm.ImportRequested    += OnModuleFolderPickRequested;
        _vm.DisableRequested   += OnDisableRequested;
        _vm.UninstallRequested += OnUninstallRequested;

        _vm.OpenModuleRequested += moduleId =>
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo($"Module:{moduleId}");
        };
    }

    private void OnDisableRequested()
    {
        var selected = _vm.SelectedItem;
        if (selected is null) return;

        var result = System.Windows.MessageBox.Show(
            $"確定要停用模組「{selected.DisplayName}」嗎？\n模組檔案將保留，可重新掃描後再次啟用。",
            "停用模組",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _vm.DisableSelectedCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"無法停用模組：{ex.Message}",
                "停用失敗",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnModuleFolderPickRequested()
    {
        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "選擇模組資料夾（包含模組 DLL 的根目錄）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        try
        {
            _vm.ImportFromFolderCommand.Execute(dlg.SelectedPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"無法導入模組：{ex.Message}",
                "導入失敗",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnUninstallRequested()
    {
        var selected = _vm.SelectedItem;
        if (selected is null) return;

        var result = System.Windows.MessageBox.Show(
            $"確定要移除模組「{selected.DisplayName}」嗎？\n此操作將刪除模組資料夾，且無法復原。",
            "移除模組",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _vm.UninstallSelectedCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"無法移除模組：{ex.Message}",
                "移除失敗",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

