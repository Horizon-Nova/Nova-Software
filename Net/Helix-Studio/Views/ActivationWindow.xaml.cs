using System;
using System.Windows;
using System.Windows.Input;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Views;

/// <summary>
/// 主程式一般金鑰啟用視窗。
/// </summary>
public partial class ActivationWindow : Window
{
    private readonly ActivationWindowViewModel _viewModel;

    /// <summary>
    /// 建立啟用視窗並接上 ViewModel。
    /// </summary>
    public ActivationWindow()
    {
        InitializeComponent();
        var provider = ((App)Application.Current).QueryServiceProvider();
        _viewModel = provider.GetRequiredService<ActivationWindowViewModel>();
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    private void CopyDeviceInfoText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => Clipboard.SetText(_viewModel.QueryClipboardText());

    private void EnterpriseAgreement_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "企業授權條款目前隨交付文件提供；一般金鑰啟用後會綁定此裝置。",
            "企業授權條款",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DataProcessingTerms_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "資料處理條款目前隨交付文件提供；授權驗證只會使用本機指紋，不會上傳到外部服務。",
            "資料處理條款",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ContactAdministrator_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "請向系統管理員索取一般金鑰與模組金鑰。一般金鑰用於啟用主程式，模組金鑰用於解鎖模組受管設定。",
            "聯絡管理員",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, bool isActivated)
    {
        DialogResult = isActivated;
        Close();
    }
}
