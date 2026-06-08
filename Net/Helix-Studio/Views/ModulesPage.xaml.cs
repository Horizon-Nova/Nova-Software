using System;
using System.Windows.Controls;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Helix_Studio.Views;

/// <summary>
/// 模組管理頁面。
/// </summary>
public partial class ModulesPage : UserControl
{
    /// <summary>
    /// 建立模組管理頁面並繫結 ViewModel。
    /// </summary>
    public ModulesPage()
    {
        InitializeComponent();

        var provider = ((App)System.Windows.Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<ModulesPageViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
        viewModel.ImportRequested += OnImportRequested;
        viewModel.ModuleOpenRequested += OnModuleOpenRequested;
        viewModel.ModuleSettingsRequested += OnModuleSettingsRequested;
    }

    /// <summary>
    /// 依模組識別碼選取指定模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    internal void ApplySelectModule(string moduleId)
    {
        if (DataContext is ModulesPageViewModel viewModel)
        {
            viewModel.ApplySelectModule(moduleId);
        }
    }

    private void OnModuleOpenRequested(object? sender, string moduleId)
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ApplyNavigateToModule(moduleId);
        }
    }

    private void OnModuleSettingsRequested(object? sender, string moduleId)
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
        {
            var displayName = DataContext is ModulesPageViewModel viewModel
                ? viewModel.SelectedItem?.DisplayName
                : null;
            mainWindow.ApplyNavigateToModuleSettings(moduleId, displayName);
        }
    }

    private void OnImportRequested(object? sender, EventArgs e)
    {
        if (sender is not ModulesPageViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "選取模組資料夾",
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.ImportModule(dialog.FolderName);
        }
    }
}
