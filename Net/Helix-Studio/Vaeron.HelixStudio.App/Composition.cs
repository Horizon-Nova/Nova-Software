using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.App.Views;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.Sampling;
using Vaeron.HelixStudio.Shell;
using Vaeron.HelixStudio.SystemAssistant;

namespace Vaeron.HelixStudio.App;

/// <summary>組裝的唯一位置。只有註冊，沒有判斷。</summary>
internal static class Composition
{
    public static void Register(IServiceCollection services)
    {
        services.AddHelixStudioData();
        services.AddSampling();
        services.AddShell();
        services.AddSystemAssistant();

        services.AddSingleton<SystemAssistantView>();
        services.AddSingleton<PlaceholderView>();
        services.AddSingleton<MainWindow>();
    }
}
