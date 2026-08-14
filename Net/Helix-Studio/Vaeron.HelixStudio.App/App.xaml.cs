using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.HelixStudio.App;

public partial class App(IServiceProvider services) : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        InitializeComponent();
        base.OnStartup(e);
        services.GetRequiredService<MainWindow>().Show();
    }
}
