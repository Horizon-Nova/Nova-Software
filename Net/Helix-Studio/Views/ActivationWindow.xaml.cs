using System.Windows;
using System.Windows.Input;
using Helix_Studio.Components;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

/// <summary>首次執行或需重新啟用時顯示的授權啟用視窗。</summary>
public partial class ActivationWindow : Window
{
    private readonly ActivationWindowViewModel _viewModel;

    /// <summary>建立視窗、繫結 <see cref="ActivationWindowViewModel"/> 並掛載關閉與啟用成功行為。</summary>
    public ActivationWindow()
    {
        InitializeComponent();

        _viewModel = new ActivationWindowViewModel();
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.LoadedCommand.Execute(null);
        Closed += (_, _) =>
        {
            _viewModel.CancelRequested -= OnCancelRequested;
            _viewModel.ActivationSucceeded -= OnActivationSucceeded;
            _viewModel.ToastRequested -= OnToastRequested;
        };

        _viewModel.CancelRequested += OnCancelRequested;
        _viewModel.ActivationSucceeded += OnActivationSucceeded;
        _viewModel.ToastRequested += OnToastRequested;
    }

    private static void OnCancelRequested()
    {
        Application.Current.Shutdown();
    }

    private void OnActivationSucceeded()
    {
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }

    private void OnToastRequested(string title, string message)
    {
        Toast.Show(this, title, message, ToastSeverity.Warning, 2800);
    }

    private void CopyDeviceInfoText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Clipboard.SetText($"{_viewModel.BoundDeviceName} | {_viewModel.ShortFingerprint}");
        Toast.Show(this, _viewModel.IsEnglish ? "Copied" : "已複製", _viewModel.IsEnglish ? "Device information copied." : "已複製裝置資訊。", ToastSeverity.Information, 2800);
    }

    private void EnterpriseAgreement_Click(object sender, RoutedEventArgs e)
        => _viewModel.OpenEnterpriseAgreementCommand.Execute(null);

    private void DataProcessingTerms_Click(object sender, RoutedEventArgs e)
        => _viewModel.OpenDataProcessingTermsCommand.Execute(null);

    private void ContactAdministrator_Click(object sender, RoutedEventArgs e)
        => _viewModel.ContactAdministratorCommand.Execute(null);
}
