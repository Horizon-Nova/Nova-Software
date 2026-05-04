using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExternalModules.InSpecTool.ViewModels;
using Microsoft.Win32;

namespace ExternalModules.InSpecTool.Views;

/// <summary>
/// InSpecTool 設定頁 View。
/// </summary>
public partial class InSpecToolSettingsPage : UserControl
{
    private readonly InSpecToolSettingsPageViewModel _viewModel;

    /// <summary>
    /// 建立設定頁 View。
    /// </summary>
    /// <param name="viewModel">設定頁 ViewModel。</param>
    internal InSpecToolSettingsPage(InSpecToolSettingsPageViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void ApplySearchDirectoryBrowse(object sender, RoutedEventArgs eventArgs)
    {
        var directoryPath = QueryFolderSelection(SearchDirectoryTextBox.Text, "選擇搜尋目錄");
        if (!string.IsNullOrWhiteSpace(directoryPath) && _viewModel.ApplySearchDirectoryDialogCommand.CanExecute(directoryPath))
        {
            _viewModel.ApplySearchDirectoryDialogCommand.Execute(directoryPath);
        }
    }

    private void ApplySystemLogDirectoryBrowse(object sender, RoutedEventArgs eventArgs)
    {
        var directoryPath = QueryFolderSelection(SystemLogDirectoryTextBox.Text, "選擇系統記錄目錄");
        if (!string.IsNullOrWhiteSpace(directoryPath) && _viewModel.ApplySystemLogDirectoryDialogCommand.CanExecute(directoryPath))
        {
            _viewModel.ApplySystemLogDirectoryDialogCommand.Execute(directoryPath);
        }
    }

    private void ApplyBackupSavePathBrowse(object sender, RoutedEventArgs eventArgs)
    {
        var directoryPath = QueryFolderSelection(BackupSavePathTextBox.Text, "選擇備援儲存目錄");
        if (!string.IsNullOrWhiteSpace(directoryPath) && _viewModel.ApplyBackupSavePathDialogCommand.CanExecute(directoryPath))
        {
            _viewModel.ApplyBackupSavePathDialogCommand.Execute(directoryPath);
        }
    }

    private void ApplyRootPreviewMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        var scrollViewer = QueryAncestorScrollViewer(this);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - eventArgs.Delta);
        eventArgs.Handled = true;
    }

    private string? QueryFolderSelection(string initialDirectory, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private ScrollViewer? QueryAncestorScrollViewer(DependencyObject? source)
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
