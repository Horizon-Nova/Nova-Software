using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.AppLibrary;

public partial class AppLibraryView : UserControl
{
    public AppLibraryView(AppLibraryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((AppLibraryViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((AppLibraryViewModel)DataContext).ApplyViewUnloaded();

    private void OnDragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>拖放檔案匯入</summary>
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;

        await ((AppLibraryViewModel)DataContext).ApplyImportFileAsync(files[0]);
    }
}
