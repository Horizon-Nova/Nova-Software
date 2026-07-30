using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.AppLibrary;
using Vaeron.HelixStudio.AppsManagement;
using Vaeron.HelixStudio.EventLog;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.RemoteBatchTransfer;
using Vaeron.HelixStudio.Settings;
using Vaeron.HelixStudio.UsbVoltageControl;
using Vaeron.HelixStudio.WorkspaceManagement;

namespace Vaeron.HelixStudio;

/// <summary>組裝的唯一位置。只有註冊,沒有判斷。</summary>
internal static class Composition
{
    public static void Register(IServiceCollection services)
    {
        services.AddHelixStudioRuntime();
        services.AddWorkspaceManagementFeature();
        services.AddEventLogFeature();
        services.AddAppsManagementFeature();
        services.AddAppLibraryFeature();
        services.AddSettingsFeature();
        services.AddUsbVoltageControlFeature();
        services.AddRemoteBatchTransferFeature();
        services.AddShell();
    }
}
