using System;
using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Helix_Studio.Views;

/// <summary>
/// 模組頁面的檢視層。
/// </summary>
public partial class ModulesPage : UserControl
{
    /// <summary>
    /// 建立模組頁面並接上 ViewModel。
    /// </summary>
    public ModulesPage()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<ModulesPageViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
        viewModel.ImportRequested += OnImportRequested;
        viewModel.ModuleOpenRequested += OnModuleOpenRequested;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 讓外部導覽可直接選取指定模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    internal void ApplySelectModule(string moduleId)
    {
        if (DataContext is ModulesPageViewModel viewModel)
        {
            viewModel.ApplySelectModule(moduleId);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModulesPageViewModel viewModel)
        {
            viewModel.ImportRequested -= OnImportRequested;
            viewModel.ModuleOpenRequested -= OnModuleOpenRequested;
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Unloaded -= OnUnloaded;
    }

    private void OnModuleOpenRequested(object? sender, string moduleId)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ApplyNavigateToModule(moduleId);
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

