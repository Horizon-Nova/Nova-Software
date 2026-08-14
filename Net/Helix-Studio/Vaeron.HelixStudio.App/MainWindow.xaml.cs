using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Vaeron.HelixStudio.App.Views;
using Vaeron.HelixStudio.Shell;

namespace Vaeron.HelixStudio.App;

/// <summary>導覽目的地 → 顯示哪個畫面。每個目的地都有一列，未實作者共用佔位。</summary>
internal sealed record ScreenRegistration(string NavId, UserControl View);

public partial class MainWindow : Window
{
    private readonly ShellViewModel shell;
    private readonly IReadOnlyList<ScreenRegistration> screens;

    public MainWindow(ShellViewModel viewModel, SystemAssistantView assistant, PlaceholderView placeholder)
    {
        InitializeComponent();

        shell = viewModel;
        screens =
        [
            new ScreenRegistration("assistant", assistant),
            new ScreenRegistration("library", placeholder),
            new ScreenRegistration("events", placeholder),
            new ScreenRegistration("settings", placeholder),
            new ScreenRegistration("usb", placeholder),
            new ScreenRegistration("vnc", placeholder)
        ];

        DataContext = viewModel;
        viewModel.PropertyChanged += OnShellPropertyChanged;
        viewModel.LoadCommand.Execute(null);
        ShowActiveScreen();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var _ in new[] { args.PropertyName }.Where(name => name == nameof(ShellViewModel.ActiveNavId)))
        {
            ShowActiveScreen();
        }
    }

    private void ShowActiveScreen()
        => ScreenHost.Content = screens
            .Where(screen => screen.NavId == shell.ActiveNavId)
            .Select(screen => screen.View)
            .Single();

    private void OnMinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MaximizeWindow(this);

    private void OnRestoreWindow(object sender, RoutedEventArgs e) => SystemCommands.RestoreWindow(this);

    private void OnCloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}
