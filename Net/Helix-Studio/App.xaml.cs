using System.Windows;
using Helix_Studio.Infrastructure.Localization;
using Helix_Studio.Services;
using Helix_Studio.Views;

namespace Helix_Studio;

/// <summary>WPF 應用程式進入點：初始化服務、套用啟動時本地化與主題，並顯示主視窗。</summary>
public partial class App : Application
{
    /// <summary>應用程式啟動時建立服務容器、套用設定並顯示 <see cref="MainWindow"/>。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppServices.Initialize();

        var config = AppServices.Config.QueryCurrent();
        UiLocale.Apply(config.Ui.CultureName, persist: false);
        UiTheme.Apply(config.Ui.ThemeMode, persist: false);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
