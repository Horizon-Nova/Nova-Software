using Microsoft.CodeAnalysis;

namespace Vaeron.Analyzers;

// 五條診斷的單一定義處。只實作平台原理上擋不住、且違反會造成實際故障的檢查。
internal static class Descriptors
{
    private const string Category = "Vaeron";

    public static readonly DiagnosticDescriptor EventPairing = new(
        "VAE001",
        "事件訂閱未配對解除",
        "'{0}' 有 += 訂閱但同型別內找不到對應的 -= 解除",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "長命物件持有短命物件的委派時,短命物件永遠不會被回收.");

    public static readonly DiagnosticDescriptor CatchLocation = new(
        "VAE002",
        "try-catch 位置不在白名單",
        "型別 '{0}' 不在 try-catch 白名單內,請讓例外往上傳播或改回傳 Result",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "例外只在能對它做出決定的地方被攔截.");

    public static readonly DiagnosticDescriptor PrimitiveSignature = new(
        "VAE003",
        "public 簽名使用裸原始型別",
        "'{0}' 的 {1} 應改用具名型別或 Result",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "多個原始型別參數並排時呼叫端無法分辨,可能失敗的操作必須回傳 Result.");

    public static readonly DiagnosticDescriptor BannedSymbol = new(
        "VAE004",
        "使用被禁用的符號",
        "'{0}' 被禁用：{1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "被禁用的符號有不可測或不安全的行為.");

    public static readonly DiagnosticDescriptor InternalVisibility = new(
        "VAE006",
        "Internal 資料夾下的型別對外可見",
        "'{0}' 位於 Internal 資料夾但對外可見,請改為 internal",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "位置即宣告：放進 Internal 等同宣告不對外,可見性必須與該宣告一致.");

    public static readonly DiagnosticDescriptor EntityLocation = new(
        "VAE007",
        "資料表映射型別位置錯誤",
        "'{0}' 帶資料表映射屬性,須位於 Internal\\Models\\ 之下",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "綁定自身資料庫結構的型別與綁定外部系統格式的型別分開存放,兩者變化源不同.");

    public static readonly DiagnosticDescriptor SqlConcat = new(
        "VAE005",
        "SQL 字串拼接",
        "偵測到以字串串接組成 SQL,請改用參數化查詢",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "字串串接組成的 SQL 易受注入攻擊.");

    public static readonly DiagnosticDescriptor NamingDirection = new(
        "VAE008",
        "方法命名前綴宣告錯誤方向",
        "'{0}' 使用了 '{1}' 前綴,此角色不得宣告該方向",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "方法名前綴宣告服務方向：對呼叫方或對資源.借用另一方向的前綴等於謊報角色.");

    public static readonly DiagnosticDescriptor RegionSet = new(
        "VAE009",
        "Region 集合不符角色規範",
        "'{0}'：{1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Facade/Service/Store 依角色固定 Region 集合,案例集合放在資料表,不是程式分支.");

    public static readonly DiagnosticDescriptor SummaryFormat = new(
        "VAE010",
        "<summary> 必須單行",
        "<summary> 內容跨越多行,請併成單行",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "單行是工作區慣例,不是語言規則.doc comment 位置錯誤（寫進函式主體）已由編譯器 CS1587 擋下,不重複檢查.");

    public static readonly DiagnosticDescriptor QuerySourceBypass = new(
        "VAE011",
        "專用查詢方法繞過查詢來源",
        "'{0}' 直接存取 '{1}',須改為透過「統一的查詢來源」屬性",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "查詢來源集中套用基礎過濾條件,個別查詢方法繞過它,基礎過濾就形同虛設,這是資料庫查詢品質的單一實作點.");

    // VAE012 編號不使用。

    public static readonly DiagnosticDescriptor FolderSuffixConsistency = new(
        "VAE013",
        "同資料夾同基底類別後綴不一致",
        "'{0}' 與同資料夾內共用基底 '{1}' 的其他類別後綴不一致,多數後綴為 '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "同一資料夾內共用非 object 基底型別/介面的類別是同一角色的重複實例,後綴須彼此一致;後綴文字本身不規定,與資料夾名稱無關.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor PipelineDependency = new(
        "VAE014",
        "管線基礎設施依賴業務層型別",
        "'{0}' 的建構子參數 '{1}' 型別為 '{2}',Middleware/Filter 不得直接依賴業務層型別",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Middleware/Filter 是跨橫切基礎設施,不是業務入口,直接依賴 *Service/*Store/*Facade 代表業務邏輯正在往基礎設施層滲透.");

    public static readonly DiagnosticDescriptor UtilityBoundary = new(
        "VAE015",
        "Utility 未跨出 C# 生態",
        "'{0}' 沒有任何 Process 呼叫或 P/Invoke,不該歸類為 Utility",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Utility 的定位是包住 C#/.NET 生態原生沒有的功能,沒有真的跨出受管環境,就只是普通邏輯借用這個角色名.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor CoreRemarks = new(
        "VAE016",
        "Core 類別缺少 <remarks>",
        "'{0}' 缺少 <remarks>,須說明取代了什麼平台預設機制",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Core 存在的理由是刻意不用平台預設做法,<remarks> 是 CLAUDE.md 已定義給這種情況的地方,沒寫等於沒交代為什麼繞開平台預設.");

    public static readonly DiagnosticDescriptor FacadeSelfInjection = new(
        "VAE017",
        "Service/ViewModel 注入自己 Feature 的 Facade",
        "'{0}' 注入了 '{1}',同一個 Feature 不得注入自己的 Facade,只有別的 Feature 才需要經過它",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Facade 是別的 Feature 與殼層存取本 Feature 的入口;同 Feature 的 Service/ViewModel 呼叫它只是純委派,不做任何轉換.");

    public static readonly DiagnosticDescriptor StoreBypass = new(
        "VAE018",
        "Service/ViewModel 繞過 Logic 直接依賴 Store",
        "'{0}' 注入了 '{1}',須改為經由 Logic 存取,不得直接注入 Store",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Logic 集中業務規則,Service/ViewModel 若能繞過去直接碰 Store,規則就有第二條不受管的路徑,Logic 集中規則這件事形同虛設.");

    public static readonly DiagnosticDescriptor ViewModelRegionShape = new(
        "VAE019",
        "ViewModel Region 內容不符宣告的契約形狀",
        "'{0}' 放在「{1}」,但缺少 {2},不具備這個 Region 宣告的契約",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Region 存在的理由是內容有可反查的外部契約,宣告放在這裡卻不具備對應標注,代表位置對了但內容沒對上.");

    public static readonly DiagnosticDescriptor ViewOnlyQuery = new(
        "VAE020",
        "專用查詢方法直接查主表",
        "'{0}' 使用了 '{1}',其承載型別 '{2}' 不是 view,查詢須改走 view",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "view 為 keyless 型別,EF Core 寫不回去,讀寫路徑分離由型別保證;過濾條件定義在 view,經過它的查詢一律套用.");

    public static readonly DiagnosticDescriptor MasterTableLocation = new(
        "VAE021",
        "主表映射型別不在共用層",
        "'{0}' 是主表映射型別,須宣告於 '{1}' 之下,不得宣告在功能組件內",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "主表跨功能共用,新功能需要欄位時加進既有主表;宣告位置是這件事的機械保證.view 為功能專屬,不受本規則限制.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor DbContextLocation = new(
        "VAE022",
        "DbContext 不在共用層",
        "'{0}' 是 DbContext 衍生類別,須宣告於 '{1}' 之下,不得宣告在功能組件內",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "一個 DbContext 即一個主題,主題底下的多個功能共用同一組表;功能組件內宣告 DbContext 等於功能自己生出一個主題與一組專屬的表.");

    public static readonly DiagnosticDescriptor SchemaSourceDirection = new(
        "VAE023",
        "由 C# 定義資料庫結構",
        "'{0}' 以 C# 推導並套用資料庫結構,結構的來源是資料庫,型別由映射工具產生",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "型別由資料庫映射產生;反方向同時存在時,同一份結構有兩個互相衝突的來源.桌面軟體以打包的結構定義檔或範本檔自建資料庫,來源仍在資料庫端.");

    public static readonly DiagnosticDescriptor FacadeDependency = new(
        "VAE024",
        "Facade 跳過串接層",
        "'{0}' 注入了 '{1}',Facade 只能注入串接層(*Service/*ViewModel)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Facade 是呼叫鏈對外的入口,直接注入 Logic 或 Store 會讓外部呼叫端接上的位置比內部 UI 更深,同一功能出現兩條深度不同的進入路徑.");
}
