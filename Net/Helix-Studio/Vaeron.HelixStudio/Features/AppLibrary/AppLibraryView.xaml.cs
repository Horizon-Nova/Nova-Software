using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Features.AppLibrary;

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

    /// <summary>拖放的副檔名/大小驗證不在這裡重寫一套——丟給 ApplyImportFileAsync 走
    /// Domain 既有的 ImportToolAsync 規則，這裡只負責把拖放事件轉成檔案路徑。</summary>
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;

        await ((AppLibraryViewModel)DataContext).ApplyImportFileAsync(files[0]);
    }
}
