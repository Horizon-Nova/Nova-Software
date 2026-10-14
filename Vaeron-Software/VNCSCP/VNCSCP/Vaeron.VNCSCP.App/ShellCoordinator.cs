using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.VNCSCP.EventLog;
using Vaeron.VNCSCP.Internal.Shell;

namespace Vaeron.VNCSCP.App;

/// <summary>確認框、狀態列通知、次視窗導覽三項能力的唯一實作。導覽鍵與畫面型別的對映表只有這一份。</summary>
public sealed class ShellCoordinator(IServiceProvider services) : IConfirmationService, INotificationService, INavigator
{
    /// <summary>導覽鍵對應的次視窗標題。</summary>
    private static readonly Dictionary<string, string> TitleByModuleKey = new()
    {
        ["EventLog"] = "事件日誌"
    };

    private readonly Dictionary<string, Window> _secondaryWindows = [];
    private readonly Dictionary<string, Func<IServiceProvider, UserControl>> _contentByModuleKey = new()
    {
        ["EventLog"] = provider => provider.GetRequiredService<EventLogView>()
    };

    private Window? _owner;
    private TextBlock? _statusText;

    public void Attach(Window owner, TextBlock statusText)
    {
        _owner = owner;
        _statusText = statusText;
    }

    public ValueTask<bool> AskAsync(string message)
    {
        var owner = RequireOwner();
        var dialog = new ConfirmationDialog(message) { Owner = owner };
        return ValueTask.FromResult(dialog.ShowDialog() is true);
    }

    public void ShowResult(OperationOutcome outcome)
    {
        var owner = RequireOwner();
        var status = _statusText ?? throw new InvalidOperationException("主視窗尚未建立");
        var brushKey = new[] { "Success" }.Where(_ => outcome.Success).DefaultIfEmpty("Danger").First();

        status.Text = outcome.Message;
        status.Foreground = (Brush)owner.FindResource(brushKey);
    }

    public void NavigateTo(string moduleKey)
    {
        var owner = RequireOwner();
        var existing = new[] { _secondaryWindows.GetValueOrDefault(moduleKey) }.OfType<Window>().ToList();

        existing.ForEach(Reactivate);

        foreach (var _ in new[] { moduleKey }.Where(_2 => existing.Count == 0))
        {
            Open(moduleKey, owner);
        }
    }

    private static void Reactivate(Window window)
    {
        window.Activate();
        window.WindowState = WindowStateAfterActivate.GetValueOrDefault(window.WindowState, window.WindowState);
    }

    /// <summary>被叫回前景的次視窗若處於最小化，回到一般大小。</summary>
    private static readonly Dictionary<WindowState, WindowState> WindowStateAfterActivate = new()
    {
        [WindowState.Minimized] = WindowState.Normal,
        [WindowState.Normal] = WindowState.Normal,
        [WindowState.Maximized] = WindowState.Maximized
    };

    private void Open(string moduleKey, Window owner)
    {
        var build = _contentByModuleKey.GetValueOrDefault(moduleKey);

        foreach (var factory in new[] { build }.OfType<Func<IServiceProvider, UserControl>>())
        {
            var window = new Window
            {
                Title = TitleByModuleKey.GetValueOrDefault(moduleKey, moduleKey),
                Content = factory(services),
                Owner = owner,
                Width = 820,
                Height = 620,
                MinWidth = 480,
                MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Brush)owner.FindResource("PageBackground")
            };

            window.Closed += OnSecondaryWindowClosed;
            _secondaryWindows[moduleKey] = window;
            window.Show();
        }
    }

    private void OnSecondaryWindowClosed(object? sender, EventArgs e)
    {
        foreach (var window in new[] { sender }.OfType<Window>())
        {
            window.Closed -= OnSecondaryWindowClosed;

            var keys = _secondaryWindows
                .Where(entry => ReferenceEquals(entry.Value, window))
                .Select(entry => entry.Key)
                .ToList();

            keys.ForEach(key => _secondaryWindows.Remove(key));
        }
    }

    private Window RequireOwner() => _owner ?? throw new InvalidOperationException("主視窗尚未建立");
}

