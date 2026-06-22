using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        RootGrid.Margin = WindowState == WindowState.Maximized
            ? SystemParameters.WindowResizeBorderThickness
            : default;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (vm.IsCommandPaletteOpen)
            {
                vm.IsCommandPaletteOpen = false;
                vm.CommandPaletteSearch = string.Empty;
            }
            else
            {
                vm.OpenCommandPalette();
                Dispatcher.BeginInvoke(() => CommandPaletteInput.Focus(), DispatcherPriority.Input);
            }
            e.Handled = true;
            return;
        }

        if (!vm.IsCommandPaletteOpen) return;

        switch (e.Key)
        {
            case Key.Escape:
                vm.IsCommandPaletteOpen = false;
                vm.CommandPaletteSearch = string.Empty;
                e.Handled = true;
                break;
            case Key.Down:
                vm.CommandPaletteSelectedIndex = Math.Min(
                    vm.CommandPaletteSelectedIndex + 1,
                    vm.FilteredPaletteItems.Count - 1);
                PaletteList.ScrollIntoView(PaletteList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                vm.CommandPaletteSelectedIndex = Math.Max(
                    vm.CommandPaletteSelectedIndex - 1, 0);
                PaletteList.ScrollIntoView(PaletteList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                if (vm.CommandPaletteSelectedIndex >= 0 &&
                    vm.CommandPaletteSelectedIndex < vm.FilteredPaletteItems.Count)
                    vm.ExecutePaletteItem(vm.FilteredPaletteItems[vm.CommandPaletteSelectedIndex]);
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteScrim_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsCommandPaletteOpen = false;
            vm.CommandPaletteSearch = string.Empty;
        }
    }

    private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
        => Dispatcher.BeginInvoke(() => CommandPaletteInput.Focus(), DispatcherPriority.Input);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Normal
                ? WindowState.Maximized
                : WindowState.Normal;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Normal
            ? WindowState.Maximized
            : WindowState.Normal;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
}
