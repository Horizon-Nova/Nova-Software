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

    /// <summary>整列點擊展開</summary>
    private void OnRowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { DataContext: EventEntrySnapshot entry })
            ((EventLogViewModel)DataContext).ApplyToggleExpandCommand.Execute(entry);
    }
}
