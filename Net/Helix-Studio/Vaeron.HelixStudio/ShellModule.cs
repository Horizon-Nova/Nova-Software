using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.HelixStudio;

/// <summary>App 根層級殼層的 DI 組裝入口，跟各 Feature 的 {模組}Module.cs 同一個慣例——
/// solution-structure.md／app-host.md 禁止 App.xaml.cs 散落具體 AddSingleton/AddTransient 呼叫，
/// MainShellViewModel/MainWindow 不屬於任何 Feature，收在這裡而不是散落在 OnStartup 內。</summary>
public static class ShellModule
{
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
