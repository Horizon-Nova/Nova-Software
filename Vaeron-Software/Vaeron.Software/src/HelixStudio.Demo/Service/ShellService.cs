using System.IO;
using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>外殼這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。Demo 版不驗授權、不用工具金鑰，工具跟安裝檔一起來；套件的檔案交給 PackageStore。</remarks>
public class ShellService
{
    private readonly ShellStore shell = new();

    private readonly LogStore log = new();

    private readonly PackageStore packages = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 外殼

    /// <summary>打開平台、工具被改過：側欄、狀態列的每一格</summary>
    public ShellLogic.ShellView LoadShell() =>
        ShellLogic.Shell(shell.QueryShellItems(), icons);

    /// <summary>掛一支工具：開它要的組件、畫面型別與資料夾；沒裝是 null</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public vw_shell? LoadTool(string toolNo) =>
        shell.QueryShellItem(toolNo).FirstOrDefault(row => row.type == "tool");

    /// <summary>平台啟動：設成自動啟動、組件檔在的每一支工具（Demo 版不驗授權與工具金鑰）</summary>
    public List<string> LoadAutostarts() =>
        shell.QueryShellItems()
            .Where(row => row.type == "tool" && row.autostart == true)
            .Where(row => packages.QueryPresent($"{row.home}", $"{row.entry}"))
            .Select(row => $"{row.tool_no}")
            .ToList();

    /// <summary>開一支工具（側欄、雙擊、開啟鈕）：Demo 版直接開，記一筆開啟</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public ShellLogic.OpenView LoadOpened(string toolNo)
    {
        var row = shell.QueryShellItem(toolNo).FirstOrDefault(one => one.type == "tool");

        var view = ShellLogic.Opened(toolNo, row);

        // 開得起來才記一筆開啟
        log.InsertEventLogList([.. new[] { view }.Where(one => one.Open).Select(one => new event_log
        {
            type = "open",
            tool_no = toolNo,
            account = Environment.UserName,
            function_name = $"{nameof(ShellService)}.{nameof(LoadOpened)}",
            message = $"開啟 {one.Name}",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        })]);

        return view;
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>平台啟動：裝上跟安裝檔一起來的工具（安裝資料夾 tools 底下的每一個套件，驗過 Demo 的簽章）；這台裝的不是這一版才裝（第一次開、安裝檔換了新版）；回傳要跟操作員說的每一句（字、warn）</summary>
    public List<(string Text, string Kind)> LoadBundled()
    {
        // 安裝資料夾 tools 底下的每一個套件，一個一個開起來、驗簽章
        var read = packages.Library(Path.Combine(AppContext.BaseDirectory, "tools"));

        // 這台現在裝著的每一支
        var known = shell.QueryShellItems().Where(row => row.type == "tool").ToList();

        // 驗得過、這台還沒裝這一版的解開到這台裝置，記一次匯入（同一個代碼再匯入就是新的一列，最新的那一次是裝著的版本）
        var done = read
            .Where(one => one.Faults.Count == 0)
            .Where(one => known.All(row => row.tool_no != one.Manifests.First().tool_no
                                        || !string.Equals(row.home, PackageStore.Home(one.Manifests.First()), StringComparison.OrdinalIgnoreCase)))
            .Select(one => packages.Install(one, "Demo 安裝檔", $"{LoadTool($"{one.Manifests.First().tool_no}")?.home}"))
            .ToList();

        shell.InsertToolList(done);

        // 驗不過的套件（檔案壞了、不是 Demo 簽的）不裝，把每一項原因說出來
        return read.Where(one => one.Faults.Count > 0).Select(one => ($"「{one.Name}」裝不上：{string.Join(" ", one.Faults)}", "warn")).ToList();
    }

    #endregion
}
