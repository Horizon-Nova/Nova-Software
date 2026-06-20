using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ExternalModules.InSpecTool.ViewModels;
using ExternalModules.InSpecTool.Views.Dialogs;

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
        Loaded += ApplyLoaded;
        IsVisibleChanged += ApplyIsVisibleChanged;
        _viewModel.ValuePromptRequested += ApplyValuePromptRequested;
        _viewModel.LicensePromptRequested += ApplyLicensePromptRequested;
        _viewModel.MessageRequested += ApplyMessageRequested;
    }

    private void ApplyLoaded(object sender, RoutedEventArgs eventArgs)
        => ApplyBarcodeInputFocus();

    private void ApplyIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.NewValue is true)
        {
            ApplyBarcodeInputFocus();
        }
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

    private void ApplyLicensePromptRequested(object? sender, InSpecLicensePromptRequestModel eventArgs)
    {
        var submittedKey = InSpecLicensePromptDialog.Show(Window.GetWindow(this), eventArgs.StatusText);
        eventArgs.ApplySubmittedLicenseKey(submittedKey);
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

    private void ApplyBarcodeInputFocus()
    {
        Dispatcher.BeginInvoke(() =>
        {
            BarcodeInputBox.Focus();
            Keyboard.Focus(BarcodeInputBox);
            BarcodeInputBox.SelectAll();
        }, DispatcherPriority.Background);
    }
}
