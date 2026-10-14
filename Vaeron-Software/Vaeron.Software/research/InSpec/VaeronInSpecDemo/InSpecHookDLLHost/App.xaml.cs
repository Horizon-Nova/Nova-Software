using System.Windows;

namespace InSpecHookDLLHost;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Info("App", $"啟動;log={AppLog.LogFilePath}");

        // 捕捉所有未攔截例外(三層)進 log,錯誤不消失。
        DispatcherUnhandledException += (_, ev) =>
        {
            AppLog.Error("DispatcherUnhandled", ev.Exception);
            ev.Handled = true; // UI 執行緒例外記錄後續命,不整個崩潰
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            if (ev.ExceptionObject is Exception ex) AppLog.Error("AppDomainUnhandled", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, ev) =>
        {
            AppLog.Error("UnobservedTask", ev.Exception);
            ev.SetObserved();
        };
    }
}
