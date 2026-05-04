using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExternalModules.InSpecTool.ViewModels;

namespace ExternalModules.InSpecTool.Views;

/// <summary>
/// InSpecTool 主頁 View。
/// </summary>
public partial class InSpecToolPage : UserControl
{
    private readonly InSpecToolPageViewModel _viewModel;

    /// <summary>
    /// 建立主頁 View。
    /// </summary>
    /// <param name="viewModel">主頁 ViewModel。</param>
    internal InSpecToolPage(InSpecToolPageViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.ValuePromptRequested += ApplyValuePromptRequested;
        _viewModel.MessageRequested += ApplyMessageRequested;
    }

    private void ApplyFileResultMouseDoubleClick(object sender, MouseButtonEventArgs eventArgs)
    {
        if (_viewModel.OpenSelectedFileCommand.CanExecute(FileResultListView.SelectedItem))
        {
            _viewModel.OpenSelectedFileCommand.Execute(FileResultListView.SelectedItem);
        }
    }

    private void ApplyValuePromptRequested(object? sender, InSpecValuePromptRequestModel eventArgs)
    {
        var confirmedValue = Dialogs.InSpecValuePromptDialog.Show(Window.GetWindow(this), eventArgs.InputTypeName, eventArgs.DefaultValue);
        eventArgs.ApplyConfirmedValue(confirmedValue);
    }

    private void ApplyMessageRequested(object? sender, InSpecMessageRequestModel eventArgs)
    {
        var icon = eventArgs.Level switch
        {
            InSpecMessageLevel.Information => MessageBoxImage.Information,
            InSpecMessageLevel.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Error
        };

        MessageBox.Show(eventArgs.Message, eventArgs.Title, MessageBoxButton.OK, icon);
    }
}
