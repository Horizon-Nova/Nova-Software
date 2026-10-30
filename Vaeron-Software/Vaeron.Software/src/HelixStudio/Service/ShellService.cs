using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>外殼這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。授權、工具金鑰的驗證交給 LicenceStore，套件的檔案交給 PackageStore。</remarks>
public class ShellService
{
    private readonly ShellStore shell = new();

    private readonly LogStore log = new();

    private readonly LicenceStore licence = new();

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

    /// <summary>平台啟動：設成自動啟動、授權准裝、工具金鑰配得上、組件檔在的每一支工具</summary>
    public List<string> LoadAutostarts()
    {
        // 這台電腦的授權准裝哪幾支；不算數就是一支都不准
        var allowed = LicenceLogic.Allowed(licence.Licence(), DateOnly.FromDateTime(DateTime.Today)) ?? [];

        return shell.QueryShellItems()
            .Where(row => row.type == "tool" && row.autostart == true)
            .Where(row => allowed.Contains($"{row.tool_no}"))
            .Where(row => licence.Fits($"{row.code}", $"{row.tool_no}").Length == 0)
            .Where(row => packages.QueryPresent($"{row.home}", $"{row.entry}"))
            .Select(row => $"{row.tool_no}")
            .ToList();
    }

    /// <summary>開一支工具（側欄、雙擊、開啟鈕）：這一刻重驗授權與工具金鑰，開得起來就記一筆開啟</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public ShellLogic.OpenView LoadOpened(string toolNo)
    {
        // 這一刻自己重驗一次授權，不看別處算過的結果
        var paper = licence.Licence();
        var allowed = LicenceLogic.Allowed(paper, DateOnly.FromDateTime(DateTime.Today));
        var row = shell.QueryShellItem(toolNo).FirstOrDefault(one => one.type == "tool");

        // 工具金鑰：授權准了才驗；存著的那一把這一刻重驗一次
        var keyFault = allowed?.Contains(toolNo) == true ? licence.Fits($"{row?.code}", toolNo) : string.Empty;

        var view = ShellLogic.Opened(toolNo, row, allowed, keyFault, paper.Ask);

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

    /// <summary>啟用一支工具：驗貼上的工具金鑰，配得上就存起來（含到期日）、記一筆啟用；回傳配不上的原因（配得上是空字串）與工具名稱</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    /// <param name="code">貼上的那一串</param>
    public (string Fault, string Name) LoadActivated(string toolNo, string code)
    {
        var fault = licence.Fits(code, toolNo);
        var name = shell.QueryShellItem(toolNo).Where(one => one.type == "tool").Select(one => one.name).FirstOrDefault() ?? toolNo;
        var now = DateTime.UtcNow;

        // 配得上：存這一把，記一筆啟用
        new[] { fault }.Where(one => one.Length == 0).ToList().ForEach(_ =>
        {
            shell.InsertToolList([ShellLogic.Key(toolNo, code.Trim(), licence.Read(code), now)]);
            log.InsertEventLogList([new event_log
            {
                type = "activate",
                tool_no = toolNo,
                account = Environment.UserName,
                function_name = $"{nameof(ShellService)}.{nameof(LoadActivated)}",
                message = $"貼上工具金鑰，啟用 {name}",
                created_at = now,
                updated_at = now,
            }]);
        });

        return (fault, name);
    }

    /// <summary>匯入工具套件：一個一個驗簽章、驗授權、看是給哪一版平台、看有沒有正在用；驗得過的解開並記一次匯入；回傳要跟操作員說的每一句（字、info 或 warn）</summary>
    /// <param name="paths">挑的每一個套件檔</param>
    public List<(string Text, string Kind)> LoadImported(IReadOnlyList<string> paths)
    {
        // 這一刻自己重驗一次授權
        var allowed = LicenceLogic.Allowed(licence.Licence(), DateOnly.FromDateTime(DateTime.Today));
        var said = new List<(string Text, string Kind)>();

        said.AddRange(new[] { "這台電腦沒有有效的授權，不能匯入工具。" }.Where(_ => allowed is null && paths.Count > 0).Select(one => (one, "warn")));

        // 一個一個開起來、驗簽章
        var read = paths.Where(_ => allowed is not null).Select(packages.Read).ToList();

        // 這把授權准裝哪幾支：只有清單裡的匯入得了
        read.Where(one => one.Manifests.Count > 0)
            .Where(one => !allowed!.Contains($"{one.Manifests.First().tool_no}"))
            .ToList()
            .ForEach(one => one.Faults.Add($"這台電腦的授權沒有包含 {one.Manifests.First().tool_no}。跟供應商加購這一支，換一把新的授權。"));

        // 套件是給哪一版平台編的：跟這一版平台的 .NET 不同就載不起來，照實說要拿哪一版
        var host = $"net{Environment.Version.Major}";

        read.Where(one => one.Manifests.Count > 0)
            .Where(one => (one.Manifests.First().runtime ?? "net10") != host)
            .ToList()
            .ForEach(one => one.Faults.Add(
                $"這個套件是給 {Editions.GetValueOrDefault(one.Manifests.First().runtime ?? "net10", one.Manifests.First().runtime ?? "net10")} 版平台的，這台是 {Editions.GetValueOrDefault(host, host)} 版。跟資訊部拿 {Editions.GetValueOrDefault(host, host)} 版的套件。"));

        // 同一版正在使用中：Windows 鎖著它的檔，換不掉
        read.Where(one => one.Manifests.Count > 0)
            .Where(one => packages.QueryInUse(PackageStore.Home(one.Manifests.First())))
            .ToList()
            .ForEach(one => one.Faults.Add($"{one.Manifests.First().name} {one.Manifests.First().version} 正在使用中，換不掉。關掉平台再開，再匯入一次。"));

        // 驗不過的把每一項原因說出來，這台裝置不動
        said.AddRange(read.Where(one => one.Faults.Count > 0).Select(one => ($"「{one.Name}」沒有匯入：{string.Join(" ", one.Faults)}", "warn")));

        // 驗得過的解開到這台裝置，記一次匯入（同一個代碼再匯入就是新的一列，最新的那一次是裝著的版本）
        var done = read
            .Where(one => one.Faults.Count == 0)
            .Select(one => packages.Install(one, "本機套件檔", $"{LoadTool($"{one.Manifests.First().tool_no}")?.home}"))
            .ToList();

        shell.InsertToolList(done);

        said.AddRange(done.Select(one => ($"已匯入「{one.name}」，它在工具目錄裡", "info")));

        return said;
    }

    #endregion

    #region 對照

    /// <summary>平台各版用的 .NET 對到給人看的名稱</summary>
    private static readonly Dictionary<string, string> Editions = new()
    {
        ["net10"] = "Win10～11",
        ["net6"] = "Win7",
    };

    #endregion
}
