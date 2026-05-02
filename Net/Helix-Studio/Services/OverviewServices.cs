using System.IO;
using System.Reflection;
using Helix_Studio.Model.Overview;
using Helix_Studio.Model.Workspace;

namespace Helix_Studio.Services;

/// <summary>首頁「概覽」分頁的系統與執行時期資訊、使用中工作區查詢。</summary>
public sealed class OverviewServices
{
    private readonly WorkspaceServices _workspaceService;

    /// <summary>以工作區服務建立概覽資料來源。</summary>
    public OverviewServices(WorkspaceServices workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>組合裝置、作業系統、版本、執行時間與本機應用程式資料路徑等顯示用資訊。</summary>
    public OverviewInfo QueryOverviewInfo()
    {
        var deviceName = Environment.MachineName;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        var versionText = version is not null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v—";

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeText = uptime.TotalHours >= 1
            ? $"執行時間：{(int)uptime.TotalHours} 小時 {uptime.Minutes} 分"
            : $"執行時間：{uptime.Minutes} 分 {uptime.Seconds} 秒";

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HorizonNovaStudio");

        return new OverviewInfo(
            deviceName,
            os,
            versionText,
            uptimeText,
            appDataPath,
            $"Helix Studio 正在 {deviceName} 上執行。  {DateTime.Now:yyyy-MM-dd HH:mm}");
    }

    /// <summary>查詢目前啟用中的工作區模型。</summary>
    public WorkspaceModel? QueryActiveWorkspace() => _workspaceService.GetActive();
}


