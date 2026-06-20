using System.Windows;
using System.Windows.Input;

namespace Helix_Studio.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

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
        => Close();
}
