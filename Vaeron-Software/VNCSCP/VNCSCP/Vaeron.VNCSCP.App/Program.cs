using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaeron.Kernel.Data;
using Vaeron.VNCSCP.Internal.Wpf;
using Vaeron.VNCSCP.Internal.Wpf.Components;

namespace Vaeron.VNCSCP.App;

internal static class Program
{
    private const string AppName = "VNCSCP";

    /// <summary>沒有 App.xaml，資源字典在程式碼合併，來源是 Internal.Wpf 這個獨立組件。</summary>
    private static readonly string[] ThemeUris =
    [
        "pack://application:,,,/Vaeron.VNCSCP.Internal.Wpf;component/Themes/Tokens.xaml",
        "pack://application:,,,/Vaeron.VNCSCP.Internal.Wpf;component/Themes/Icons.xaml",
        "pack://application:,,,/Vaeron.VNCSCP.Internal.Wpf;component/Themes/Controls.xaml"
    ];

    [STAThread]
    private static void Main(string[] args)
    {
        // 觸發靜態建構子，密碼框浮水印依賴的類別處理常式要在第一個密碼框出現前註冊。
        PasswordBoxHelper.EnsureRegistered();

        using var host = HostSetup.Create(args, AppName).Build();
        host.Start();

        var application = new Application();
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        ViewModelBase.UnhandledAsyncError = ReportBackgroundError;

        foreach (var uri in ThemeUris)
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri) });
        }

        host.Services.InitializeVncscpSchemaAsync(CancellationToken.None).GetAwaiter().GetResult();

        application.Run(host.Services.GetRequiredService<MainWindow>());
        application.DispatcherUnhandledException -= OnDispatcherUnhandledException;

        host.StopAsync().GetAwaiter().GetResult();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => ShowFailure(e.Exception);

    private static void ReportBackgroundError(Exception error)
        => Application.Current.Dispatcher.Invoke(() => ShowFailure(error));

    private static void ShowFailure(Exception error)
        => MessageBox.Show(error.Message, "VNCSCP", MessageBoxButton.OK, MessageBoxImage.Error);
}
