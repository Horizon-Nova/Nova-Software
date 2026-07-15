using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vaeron.HelixStudio.Features.EventLog.Contracts;

namespace Vaeron.HelixStudio.Features.EventLog;

public partial class EventLogView : UserControl
{
    public EventLogView(EventLogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((EventLogViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((EventLogViewModel)DataContext).ApplyViewUnloaded();

    /// <summary>ListViewItem 沒有 Command 屬性可以直接綁——整列可點擊展開的唯一接線點，
    /// 見 EventLogView.xaml 的 EventSetter 註解。</summary>
    private void OnRowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem { DataContext: EventEntrySnapshot entry })
            ((EventLogViewModel)DataContext).ApplyToggleExpandCommand.Execute(entry);
    }
}
