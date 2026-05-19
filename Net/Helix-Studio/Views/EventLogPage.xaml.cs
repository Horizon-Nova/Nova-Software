using System;
using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Views;

/// <summary>
/// 事件日誌頁面的檢視層。
/// </summary>
public partial class EventLogPage : UserControl
{
    /// <summary>
    /// 建立事件日誌頁面並接上 ViewModel。
    /// </summary>
    public EventLogPage()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<EventLogPageViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
    }
}

