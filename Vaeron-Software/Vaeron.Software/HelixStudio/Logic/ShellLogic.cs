using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>外殼的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class ShellLogic
{
    #region 開啟平台

    /// <summary>開啟平台：側邊導覽列放哪幾支工具、狀態列每一格寫什麼</summary>
    /// <param name="rows">vw_shell 查到的每一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static ShellView Shell(IReadOnlyList<vw_shell> rows, Dictionary<string, List<string>> icons)
    {
        // 工具、這台裝置、用它的人，各自是哪幾列
        var tools = rows.Where(row => row.type == "tool").ToList();
        var device = rows.Where(row => row.type == "device").ToList();
        var person = rows.Where(row => row.type == "person").ToList();

        // 裝好的工具：狀態是 installed 或 update
        var installed = tools.Where(row => Installed.Contains($"{row.state}")).ToList();

        // 側邊導覽列：第一列固定是工具目錄，接著是裝好的每一支工具；AI 助理在下方有自己的按鈕
        var rail = Enumerable.Repeat(new RailItem
            {
                Key = string.Empty,
                Name = "工具目錄",
                Paths = icons["layout-grid"],
                Tip = "工具目錄",
            }, 1)
            .Concat(installed
            .Where(row => row.tool_no != Assistant)
            .Select(row => new RailItem
            {
                Key = $"{row.tool_no}",
                Name = $"{row.name}",
                Paths = icons[$"{row.icon}"],
                Badges = new[] { row.badge ?? 0 }.Where(count => count > 0).ToList(),
                Tip = $"{row.name} · {StateText[$"{row.state}"]}",
            }))
            .ToList();

        // 必裝而沒裝好的工具，決定合規那一格
        var missing = tools.Where(row => row.required == true && !Installed.Contains($"{row.state}")).ToList();

        // 可更新的工具數
        var updatable = tools.Count(row => row.state == "update");

        return new ShellView
        {
            Product = "HelixStudio",
            Crumb = "— 工具目錄",
            SearchHint = "搜尋工具與動作",
            SearchLabel = "搜尋工具、維護團隊或動作",
            Icons = icons,
            Rail = rail,
            Initial = person.Select(row => $"{row.name}"[..1]).DefaultIfEmpty(string.Empty).First(),
            PersonTip = person.Select(row => $"{row.name} · {row.dept}").DefaultIfEmpty(string.Empty).First(),
            Compliance = missing
                .Select(_ => $"{missing.Count} 項必裝未安裝")
                .DefaultIfEmpty("這台裝置合規")
                .First(),
            ComplianceTip = missing
                .Select(_ => $"合規狀態：{missing.Count} 項必裝未安裝")
                .DefaultIfEmpty("合規狀態：符合要求")
                .First(),
            Missing = missing.Count,
            CompliancePaths = missing
                .Select(_ => icons["triangle-alert"])
                .DefaultIfEmpty(icons["check"])
                .First(),
            ComplianceInk = missing.Select(_ => "#a4262c").DefaultIfEmpty("#3b3b3b").First(),
            ComplianceBack = missing.Select(_ => "#fdf3f2").DefaultIfEmpty("#f3f3f3").First(),
            ComplianceLine = missing.Select(_ => "#66a4262c").DefaultIfEmpty("#24000000").First(),
            Update = new[] { updatable }
                .Where(count => count > 0)
                .Select(count => $"{count} 支可更新")
                .DefaultIfEmpty("無待更新")
                .First(),
            InstalledCount = $"{installed.Count} 支已安裝",
            Host = device.Select(row => $"{row.host}").DefaultIfEmpty(string.Empty).First(),
            DeviceLine = device.Select(row => $"{row.host} · {row.ip}").DefaultIfEmpty(string.Empty).First(),
            DeviceTip = device
                .Select(row => $"電腦名稱 {row.host}\n作業系統 {row.os}\n內網 IP {row.ip}")
                .Concat(person.Select(row => $"使用者 {row.name}（{row.dept}）"))
                .DefaultIfEmpty(string.Empty)
                .Aggregate((first, next) => first + "\n" + next),
            Connection = "離線部署 · 未連外",
            Sync = device.Select(row => $"套件庫 {row.sync_at}").DefaultIfEmpty(string.Empty).First(),
            Client = device.Select(row => $"用戶端 {row.client} ({row.build})").DefaultIfEmpty(string.Empty).First(),
            Bubble = new ComplianceView
            {
                Paths = missing.Select(_ => icons["triangle-alert"]).DefaultIfEmpty(icons["check"]).First(),
                Ink = missing.Select(_ => "#a4262c").DefaultIfEmpty("#3b3b3b").First(),
                Back = missing.Select(_ => Ink["DangerBack"]).DefaultIfEmpty(Ink["WindowBack"]).First(),
                Line = missing.Select(_ => Ink["DangerLine"]).DefaultIfEmpty(Ink["HairLine"]).First(),
                ClosePaths = icons["x"],
                Title = missing
                    .Select(_ => $"公司政策要求安裝：{string.Join("、", missing.Select(one => one.name))}")
                    .DefaultIfEmpty("這台裝置符合 IT 必裝要求")
                    .First(),
                Text = missing
                    .Select(_ => "未安裝將於 7 日後限制內網存取。安裝約需 2 分鐘，過程中可以繼續使用其他工具。")
                    .DefaultIfEmpty($"最後檢查 {device.Select(one => $"{one.sync_at}").DefaultIfEmpty(string.Empty).First()}。"
                                  + "必裝工具有新版時，資訊部會自動推送並在這裡提醒。")
                    .First(),
                ReviewLabel = "查看清單",
                FixLabel = missing
                    .Skip(1)
                    .Select(_ => $"安裝這 {missing.Count} 支")
                    .Concat(missing.Take(1).Select(one => $"安裝 {one.name}"))
                    .DefaultIfEmpty("重新檢查")
                    .First(),
                FixBack = missing.Select(_ => Ink["Accent"]).DefaultIfEmpty(Ink["Surface"]).First(),
                FixInk = missing.Select(_ => Ink["OnAccent"]).DefaultIfEmpty(Ink["TextPrimary"]).First(),
                Fixes = missing
                    .Select(row => new CatalogLogic.RunItem
                    {
                        Key = $"{row.tool_no}",
                        Name = $"{row.name}",
                        Action = "install",
                        State = "installed",
                        Version = $"{row.version}",
                        FromState = $"{row.state}",
                        FromVersion = $"{row.installed_version}",
                    })
                    .ToList(),
                Said = missing
                    .Select(_ => $"已開始安裝 {missing.Count} 支必裝工具")
                    .DefaultIfEmpty("已重新檢查合規狀態，這台裝置符合要求")
                    .First(),
                ReviewSaid = missing
                    .Select(_ => $"有 {missing.Count} 項必裝工具未安裝，已篩出「IT 必裝」分類")
                    .DefaultIfEmpty("這台裝置符合所有 IT 必裝要求")
                    .First(),
                ReviewKind = missing.Select(_ => "warn").DefaultIfEmpty("info").First(),
            },
            ClientTip = device
                .Select(row => $"HelixStudio 用戶端 {row.client} build {row.build}\n{row.os}")
                .DefaultIfEmpty(string.Empty)
                .First(),
        };
    }

    #endregion


    #region 跳出一則通知

    /// <summary>跳出一則通知：說什麼、左邊那條色條什麼顏色、停多久</summary>
    /// <param name="text">要說的話</param>
    /// <param name="kind">哪一種：info 或 warn</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="undos">按了復原要把工具還原成什麼樣；不能復原就是零筆</param>
    public static NoteItem Note(string text, string kind, Dictionary<string, List<string>> icons,
        IReadOnlyList<CatalogLogic.RunItem> undos) =>
        new()
        {
            Text = text,
            Paths = icons[NoteIcon[kind]],
            Ink = NoteInk[kind],

            // 附復原的那一則要留久一點，人才來得及撤回
            Seconds = undos.Select(_ => Undoable).DefaultIfEmpty(NoteSeconds[kind]).First(),
            Undos = [.. undos],
        };

    #endregion

    #region 出了狀況

    /// <summary>出了狀況：說發生什麼事、附上原文、給一條出路</summary>
    /// <param name="detail">系統回報的原文</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static FaultItem Fault(string detail, Dictionary<string, List<string>> icons) =>
        new()
        {
            Text = "讀不到資料，畫面上的內容可能不是最新的。",
            Detail = detail,
            RetryLabel = "重試",
            Paths = icons["triangle-alert"],
            Ink = Ink["Danger"],
            Back = Ink["DangerBack"],
            Line = Ink["DangerLine"],
        };

    #endregion

    #region 對照

    /// <summary>啟動時讀一次的那一份設定；要改設定就重啟</summary>
    public static SettingsLogic.SetupView Setup { get; private set; } = new();

    /// <summary>把這一份設定定下來</summary>
    /// <param name="setup">啟動時讀出來的設定</param>
    public static void Adopt(SettingsLogic.SetupView setup) =>
        Setup = setup;

    /// <summary>這一套顏色：畫面與運算都從這裡取</summary>
    public static IReadOnlyDictionary<string, string> Ink { get; } = new Dictionary<string, string>
    {
        ["WindowBack"] = "#f3f3f3",
        ["ContentBack"] = "#fafafa",
        ["Surface"] = "#ffffff",
        ["TextPrimary"] = "#1b1b1b",
        ["TextSecondary"] = "#3b3b3b",
        ["TextMuted"] = "#5c5c5c",
        ["TextFaint"] = "#767676",
        ["TextGhost"] = "#a0a0a0",
        ["HairLine"] = "#14000000",
        ["CheckLine"] = "#66000000",
        ["Divider"] = "#1a000000",
        ["HoverBack"] = "#0f000000",
        ["PressBack"] = "#1a000000",
        ["Track"] = "#ebebeb",
        ["Danger"] = "#a4262c",
        ["DangerBack"] = "#fdf3f2",
        ["DangerLine"] = "#66a4262c",
        ["CloseHover"] = "#c42b1c",
        ["Badge"] = "#c42b1c",
        ["Scrim"] = "#40000000",
        ["ShineDim"] = "#ededed",
        ["ShineLit"] = "#f6f6f6",
        ["OnAccent"] = "#ffffff",
        ["Accent"] = "#0f6cbd",
        ["AccentHover"] = "#0057a6",
    };

    /// <summary>主色由淺到深九階：以 #0f6cbd 的 OKLCH（L 0.526、C 0.149、H -108.4°）為基準，亮度等距、兩端收斂彩度</summary>
    public static IReadOnlyDictionary<int, string> Brand { get; } = new Dictionary<int, string>
    {
        [100] = "#e4f8ff",
        [200] = "#c5e8ff",
        [300] = "#98cfff",
        [400] = "#61a9f6",
        [500] = "#0f6cbd",
        [600] = "#0057a6",
        [700] = "#004086",
        [800] = "#002861",
        [900] = "#00133b",
    };

    /// <summary>每一種通知的圖示</summary>
    public static IReadOnlyDictionary<string, string> NoteIcon { get; } = new Dictionary<string, string>
    {
        ["info"] = "check",
        ["warn"] = "triangle-alert",
    };

    /// <summary>每一種通知左邊色條的顏色</summary>
    public static IReadOnlyDictionary<string, string> NoteInk => new Dictionary<string, string>
    {
        ["info"] = Ink["Accent"],
        ["warn"] = Ink["Danger"],
    };

    /// <summary>附復原的通知停幾秒</summary>
    private const int Undoable = 10;

    /// <summary>每一種通知停幾秒：資訊 4、警告 7</summary>
    public static IReadOnlyDictionary<string, int> NoteSeconds { get; } = new Dictionary<string, int>
    {
        ["info"] = 4,
        ["warn"] = 7,
    };

    /// <summary>算得上裝好的那幾種狀態</summary>
    public static IReadOnlyCollection<string> Installed { get; } = ["installed", "update"];

    /// <summary>AI 助理這一支：它在側欄下方有自己的按鈕，不排進上面那一列</summary>
    public const string Assistant = "ai.assistant";

    /// <summary>每一種狀態在畫面上的字：設計檔 stateMeta</summary>
    public static IReadOnlyDictionary<string, string> StateText { get; } = new Dictionary<string, string>
    {
        ["installed"] = "已安裝",
        ["update"] = "可更新",
        ["not_installed"] = "未安裝",
        ["required_missing"] = "必裝未安裝",
    };

    #endregion

    #region 模型

    /// <summary>外殼畫面上的每一格</summary>
    public class ShellView
    {
        /// <summary>標題列的產品名</summary>
        public string Product { get; set; } = string.Empty;

        /// <summary>標題列的麵包屑</summary>
        public string Crumb { get; set; } = string.Empty;

        /// <summary>搜尋藥丸的滑鼠提示</summary>
        public string SearchHint { get; set; } = string.Empty;

        /// <summary>搜尋藥丸上的字</summary>
        public string SearchLabel { get; set; } = string.Empty;

        /// <summary>圖示名稱對到的每一段路徑</summary>
        public Dictionary<string, List<string>> Icons { get; set; } = [];

        /// <summary>側邊導覽列上的每一支工具</summary>
        public List<RailItem> Rail { get; set; } = [];

        /// <summary>頭像上的字：姓名第一個字</summary>
        public string Initial { get; set; } = string.Empty;

        /// <summary>頭像的滑鼠提示</summary>
        public string PersonTip { get; set; } = string.Empty;

        /// <summary>狀態列第一格：合規狀態</summary>
        public string Compliance { get; set; } = string.Empty;

        /// <summary>合規那一格的滑鼠提示</summary>
        public string ComplianceTip { get; set; } = string.Empty;

        /// <summary>必裝而沒裝好的工具數</summary>
        public int Missing { get; set; }

        /// <summary>合規那一格的圖示路徑</summary>
        public List<string> CompliancePaths { get; set; } = [];

        /// <summary>合規那一格的文字色</summary>
        public string ComplianceInk { get; set; } = string.Empty;

        /// <summary>合規那一格圖示圈的底色</summary>
        public string ComplianceBack { get; set; } = string.Empty;

        /// <summary>合規那一格圖示圈的邊線色</summary>
        public string ComplianceLine { get; set; } = string.Empty;

        /// <summary>狀態列第二格：可更新數</summary>
        public string Update { get; set; } = string.Empty;

        /// <summary>狀態列第三格：已安裝數</summary>
        public string InstalledCount { get; set; } = string.Empty;

        /// <summary>這台電腦叫什麼</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>狀態列的裝置那一格</summary>
        public string DeviceLine { get; set; } = string.Empty;

        /// <summary>裝置那一格的滑鼠提示</summary>
        public string DeviceTip { get; set; } = string.Empty;

        /// <summary>狀態列的連線那一格</summary>
        public string Connection { get; set; } = string.Empty;

        /// <summary>狀態列的套件庫同步那一格</summary>
        public string Sync { get; set; } = string.Empty;

        /// <summary>狀態列的用戶端版本那一格</summary>
        public string Client { get; set; } = string.Empty;

        /// <summary>版本那一格的滑鼠提示</summary>
        public string ClientTip { get; set; } = string.Empty;

        /// <summary>點合規那一格跳出來的氣泡</summary>
        public ComplianceView Bubble { get; set; } = new();
    }






    /// <summary>合規那一格跳出來的氣泡</summary>
    public class ComplianceView
    {
        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>圖示與標題的顏色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>圖示圈的底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>圖示圈的框線色</summary>
        public string Line { get; set; } = string.Empty;

        /// <summary>關閉按鈕的每一段路徑</summary>
        public List<string> ClosePaths { get; set; } = [];

        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>左下那顆按鈕上的字</summary>
        public string ReviewLabel { get; set; } = string.Empty;

        /// <summary>右下那顆按鈕上的字</summary>
        public string FixLabel { get; set; } = string.Empty;

        /// <summary>右下那顆按鈕的底色</summary>
        public string FixBack { get; set; } = string.Empty;

        /// <summary>右下那顆按鈕的文字色</summary>
        public string FixInk { get; set; } = string.Empty;

        /// <summary>按右下那顆要裝的每一支；都裝好了就是零筆</summary>
        public List<CatalogLogic.RunItem> Fixes { get; set; } = [];

        /// <summary>按右下那顆之後說的那一句</summary>
        public string Said { get; set; } = string.Empty;

        /// <summary>按查看清單之後說的那一句</summary>
        public string ReviewSaid { get; set; } = string.Empty;

        /// <summary>那一句是哪一種通知</summary>
        public string ReviewKind { get; set; } = string.Empty;
    }

    /// <summary>跳出來的一則通知</summary>
    public class NoteItem
    {
        /// <summary>說了什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>左邊色條與圖示的顏色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>停幾秒</summary>
        public int Seconds { get; set; }

        /// <summary>按了復原要把工具還原成什麼樣；不能復原就是零筆</summary>
        public List<CatalogLogic.RunItem> Undos { get; set; } = [];

        /// <summary>滑鼠停在上面時計時跟著停</summary>
        public bool Held { get; set; }
    }

    /// <summary>出了狀況時橫幅上的那一段</summary>
    public class FaultItem
    {
        /// <summary>發生了什麼事</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>系統回報的原文</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>出路那顆按鈕上的字</summary>
        public string RetryLabel { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>文字與圖示的顏色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>框線色</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>側邊導覽列上的一顆按鈕</summary>
    public class RailItem
    {
        /// <summary>工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>未讀數；沒有未讀就是零筆</summary>
        public List<int> Badges { get; set; } = [];

        /// <summary>滑鼠提示</summary>
        public string Tip { get; set; } = string.Empty;

        /// <summary>這一格現在長什麼樣；重讀時拿它比，一樣就不動那一格</summary>
        public string Mark => $"{Key}|{Tip}|{string.Join(",", Badges)}";
    }

    #endregion
}
