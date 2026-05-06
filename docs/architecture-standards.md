# Windows Presentation Foundation (WPF) 架構規範

| 項目 | 內容 |
| --- | --- |
| 文件編號 | WPF-ARCH-001 |
| 版本 | 1.6 |
| 適用範圍 | Windows Presentation Foundation (WPF) |
| 規範強度 | 強制，無例外 |
| 目標讀者 | 開發者、AI 代理人 |

---

## §1 規範用語

| 用語 | 意義 |
| --- | --- |
| **必須 (MUST)** | 強制條款，違反即為不合規 |
| **禁止 (MUST NOT)** | 強制禁止，無任何例外 |

---

## §2 專案目錄結構

以下是本專案唯一正確的目錄結構。AI 代理人不得新增層次、不得在禁止子資料夾的層內建立子資料夾。

```
Net/YourProject/
├── Views/               XAML + code-behind，允許一層子資料夾（如 Dialogs/）
├── ViewModels/          UI 狀態機，扁平結構，僅允許 Base/ 子資料夾
│   └── Base/            ViewModelBase、RelayCommand 等基礎設施類別
├── Extensions/          DI 註冊管理，扁平結構，禁止任何子資料夾
├── Services/            零邏輯純接口，扁平結構，禁止任何子資料夾
├── Domain/              業務規則與判斷，扁平結構，禁止任何子資料夾
├── Core/                跨功能共用能力，扁平結構，禁止任何子資料夾
├── Model/               資料結構，扁平結構，禁止任何子資料夾
├── Components/          跨 View 共用的 UserControl（至少 2 個 View 使用）
├── Themes/              樣式、色彩、控制項範本
├── Assets/              靜態資源（Icons/、Images/、Legal/）
└── Regions/             導航區域名稱常數
```

**本專案採用單一 `.csproj`，禁止將各層拆分為獨立 Class Library 專案。此決策屬架構遷移層級，AI 代理人不得自行判斷或執行。**

---

## §3 檔案命名規則

### §3.1 各層強制後綴

每個功能主題（如 Settings、License、Workspace）在每一層只能有一個對應檔案，以固定後綴命名。

| 層次 | 強制後綴 | 正確 | 禁止 |
| --- | --- | --- | --- |
| `Views` | `Page` / `Window` / `Dialog` | `SettingsPage.xaml` | `SettingsView.xaml` |
| `ViewModels` | `PageViewModel` / `WindowViewModel` | `SettingsPageViewModel.cs` | `SettingsVM.cs` |
| `Services` | `Services`（複數，對應 View 名稱） | `MainWindowServices.cs`、`SettingsServices.cs` | `SettingsPageService.cs`、`DataServices.cs` |
| `Model` | `Model` | `SettingsModel.cs` | `AppSettings.cs`（置於 Model 層時） |
| `Domain` | `Domain` | `SettingsDomain.cs` | `SettingsPolicy.cs`、`SettingsLogic.cs`、`SettingsRule.cs` |
| `Core` | 語意命名（無固定後綴） | `AppSettings.cs`、`QueryableExtensions.cs` | `AppSettingsHelper.cs`、`AppSettingsUtil.cs` |

### §3.2 一個主題，一個檔案

同一功能主題在同一層只能有一個檔案。若同主題存在多個類別，合併至同一檔案。

```
❌ Services/SettingsPageService.cs + Services/SettingsRuntimeService.cs
✅ Services/SettingsServices.cs（合併，對應 Settings.xaml）

❌ Services/DataServices.cs（無對應 View，Services 不存在）
✅ Domain/DataDomain.cs（無 View 的邏輯直接放 Domain）

❌ Model/Settings/AppSettings.cs（子資料夾）
✅ Model/SettingsModel.cs（扁平，一個檔案可含多個相關 record/class）

❌ Domain/Settings/SettingsPolicy.cs（錯誤後綴 + 子資料夾）
✅ Domain/SettingsDomain.cs
```

### §3.3 函式命名前綴

函式名稱必須透過前綴明確表達動作性質，讀者不進入函式體即可判斷行為。

| 前綴 | 語意 | 範例 |
| --- | --- | --- |
| `Query` | 讀取，不修改狀態 | `QuerySettings()`、`QueryActiveWorkspace()` |
| `Save` | 寫入至持久化儲存 | `SaveSettings()` |
| `Apply` | 套用至執行時期（非持久化） | `ApplyTheme()`、`ApplyScale()` |
| `Load` | 從來源載入至記憶體 | `LoadModule()` |
| `Export` | 輸出至外部 | `ExportLogs()` |
| `Create` | 建立新資源 | `CreateWorkspace()` |
| `Delete` | 刪除資源 | `DeleteWorkspace()` |
| `Update` | 修改現有資源 | `UpdateWorkspace()` |
| `Open` | 開啟外部資源 | `OpenLegalNotices()` |
| `Activate` | 啟用或切換使用中狀態 | `ActivateWorkspace()` |
| `Validate` | 規則判斷，回傳是否成立 | `ValidateLicense()`、`ValidateWorkspaceName()` |
| `Calculate` | 數值或結果計算 | `CalculateTimeoutDuration()` |
| `Resolve` | 從輸入推導出對應結果 | `ResolveLanguage()`、`ResolveScaleFactor()` |

**禁止** `Process`、`Handle`、`Do`、`Execute`、`Run`、`Manager` 作為函式名稱的主動詞。

---

## §4 各層職責

### §4.1 依賴方向

```
Views → ViewModels → Services → Domain → Model
                                  ↓
                                Core
```

- 禁止跳層：Views 不得直接呼叫 Services 或 Domain
- 禁止跳層：ViewModels 不得直接呼叫 Domain
- 禁止反向：Domain 不得依賴 Services、ViewModels、Views
- 禁止反向：Model 不得依賴任何其他功能層
- 禁止：Core 以外的層（Services、ViewModels、Views）直接呼叫 Core

### §4.2 View

**職責**：呈現畫面、接收使用者輸入、透過資料繫結連接 ViewModel、切換視覺狀態。

**code-behind 只允許**：
- UI 初始化（`InitializeComponent`、事件訂閱）
- 無法以繫結完成的 UI 事件轉發（拖放、複雜滑鼠行為）
- 動畫觸發、視覺狀態切換
- 對話框結果處理

**code-behind 禁止**：
- 執行業務邏輯、資料驗證、資料存取
- 呼叫 Service 或 Domain
- 直接存取檔案、網路、資料庫

**判定式**：code-behind 方法名稱出現以下動詞，即必須移至 ViewModel：
`Validate`、`Save`、`Load`、`Calculate`、`Query`、`Fetch`、`Export`、`Import`

### §4.3 ViewModel

**職責**：ViewModel 是 **UI 狀態機**，等同於前端框架中的元件狀態層（React 的 `useState` + `useReducer` + event handlers）。它的厚度在「UI 交互狀態的管理」，不在業務邏輯的執行。

ViewModel 負責且僅負責以下五類工作：

1. **可繫結屬性**：持有 View 顯示所需之資料，狀態改變時通知 View 更新
2. **ICommand 封裝**：將使用者操作（按鈕點擊、表單提交）包裝為 Command，在 Command 內呼叫 Service
3. **UI 執行狀態**：`IsBusy`、`IsLoading`、`HasError` 等 View 切換視覺狀態所需的旗標
4. **Dirty Flag 與欄位驗證訊息**：追蹤使用者是否已修改資料（`IsDirty`）、持有各欄位的驗證錯誤訊息
5. **資料形態轉換**：將 Service 回傳的 Model 轉為 View 可直接顯示的型別（`DateTime` → 格式化字串、`enum` → 顯示文字）

**正確樣態：**

```csharp
/// <summary>
/// 設定頁面的 UI 狀態機，管理表單狀態、驗證訊息與使用者操作。
/// </summary>
public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly SettingsServices _service;

    private bool _isBusy;
    /// <summary>儲存操作執行中，用於控制 UI 的載入遮罩。</summary>
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private bool _isDirty;
    /// <summary>使用者是否已修改但尚未儲存。</summary>
    public bool IsDirty { get => _isDirty; set => SetProperty(ref _isDirty, value); }

    private int _languageIndex;
    /// <summary>語言下拉選單的選中索引。</summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set { SetProperty(ref _languageIndex, value); IsDirty = true; }
    }

    private string _logFolderError = string.Empty;
    /// <summary>匯出路徑欄位的驗證錯誤訊息；空字串表示無錯誤。</summary>
    public string LogFolderError
    {
        get => _logFolderError;
        set => SetProperty(ref _logFolderError, value);
    }

    /// <summary>觸發儲存設定，並在完成後清除 Dirty 旗標。</summary>
    public ICommand SaveCommand { get; }

    /// <summary>初始化 SettingsPageViewModel 並建立 Command。</summary>
    public SettingsPageViewModel(SettingsServices service)
    {
        _service = service;
        SaveCommand = new RelayCommand(ExecuteSave, () => IsDirty && !IsBusy);
    }

    private void ExecuteSave()
    {
        if (!ValidateForm()) return;
        IsBusy = true;
        _service.SaveSettings(BuildSettingsSnapshot());
        IsDirty = false;
        IsBusy = false;
    }

    private bool ValidateForm()
    {
        LogFolderError = System.IO.Directory.Exists(LogExportFolder) ? string.Empty : "路徑不存在";
        return string.IsNullOrEmpty(LogFolderError);
    }
}
```

**常見誤解對照：**

| 誤解 | 正確理解 |
| --- | --- |
| ViewModel 是 Service 的薄殼，public 方法一一對應 Service 方法 | ViewModel 持有 UI 狀態，操作透過 ICommand 觸發，不應暴露裸方法給 View |
| `IsBusy`、`IsDirty`、驗證訊息應放在 Service | 這些是 UI 狀態，屬於 ViewModel；Service 不知道 UI 存在 |
| 欄位格式驗證（必填、路徑存在）屬於 Domain | 欄位格式驗證屬 ViewModel；業務規則驗證（「信用額度不足」）才屬 Domain |
| ViewModel 太肥就拆分成多個 ViewModel 檔案 | ViewModel 肥代表有業務邏輯混入，應將計算 / 判斷下推至 Domain |

**必須**：
- 繼承 `ViewModelBase`（已實作 `INotifyPropertyChanged`）
- 透過建構式注入 Service，不在內部 `new` Service
- 使用者操作以 `ICommand` 封裝，不以 public 方法直接暴露給 View
- `IsBusy`、`IsDirty`、欄位驗證訊息在 ViewModel 持有與管理

**禁止**：
- 引用 `System.Windows.Controls`、`System.Windows.Media` 等 WPF 視覺命名空間
- 直接呼叫 `MessageBox.Show`
- 在 ViewModel 寫業務規則判斷
- 直接呼叫 Domain（必須經 Service）

### §4.4 Services

**職責**：Service 是 ViewModel 與 Domain 之間的**零邏輯純接口**。它的唯一工作是將 ViewModel 的呼叫**原封不動地轉發給 Domain**，自身不做任何判斷、計算或流程組合。Service 不知道 UI 存在。

**存在原則**：Service 跟著 View 走，**有 View 才有對應的 Service**。一個 View 對應一個 Service，命名與 View 一致。沒有 View 的功能邏輯直接放 Domain，不建立 Service。

```
MainWindow.xaml      → MainWindowServices.cs
Settings.xaml        → SettingsServices.cs
（無對應 View 的邏輯） → Domain/*.cs，不建立 Service
```

**一句話定義**：Service 方法就是 Domain 方法的公開入口，除了轉發呼叫之外什麼都不做。

**正確樣態：**

```csharp
/// <summary>
/// 將文字輸入轉發至 Domain 處理。
/// </summary>
public string InputText(string str) => _domain.TextFunction(str);

/// <summary>
/// 查詢當前套用的設定值。
/// </summary>
public AppSettings QuerySettings() => _domain.QuerySettings();

/// <summary>
/// 儲存設定。
/// </summary>
public void SaveSettings(AppSettings settings) => _domain.SaveSettings(settings);
```

**反例（禁止）：**

```csharp
// 禁止：Service 內自行串接多個呼叫，這是邏輯，屬 Domain
public void SaveSettings(AppSettings settings)
{
    var resolved = _domain.ResolveLanguage(settings.LanguageIndex);
    _repository.Save(settings);
    _runtime.ApplyLanguage(resolved);
}

// 禁止：Service 內有 if 判斷，這是邏輯，屬 Domain
public string InputText(string str)
{
    if (string.IsNullOrEmpty(str)) return "空白";
    return _domain.TextFunction(str);
}
```

**必須**：
- 每個方法只做一件事：呼叫對應的 Domain 方法並回傳結果
- 方法簽章與 Domain 方法保持一致（相同參數、相同回傳型別）

**禁止**：
- 在 Service 內出現任何 `if`、`switch`、計算、字串處理等邏輯
- 在 Service 內串接多個 Domain 呼叫
- 持有 `IsBusy`、`IsDirty` 等 UI 狀態
- 自行決定業務上該怎麼做（一切判斷屬 Domain）

**判定式**：Service 方法若超過 **1 行**（不含 `summary`），即代表有邏輯混入，必須下推至 Domain。

### §4.5 Domain

**職責**：Domain 是**所有功能邏輯的唯一實作地點**，包含業務規則、判斷、計算、流程組合。ViewModel 透過 Service 呼叫 Domain，Domain 包辦所有實際工作後回傳結果。Domain 不知道 UI 存在，也不知道資料怎麼儲存。

**判定方式**：所有需要 `if` 判斷、`switch` 對應、公式計算、流程組合的邏輯，一律放 Domain。Service 只轉發，Domain 包辦一切功能邏輯。

```csharp
/// <summary>
/// 依語言索引解析對應的語言代碼。
/// </summary>
public string ResolveLanguage(int index) => index switch
{
    0 => "zh-TW",
    1 => "en-US",
    2 => "ja-JP",
    _ => throw new ArgumentOutOfRangeException(nameof(index))
};

/// <summary>
/// 依非活躍逾時索引換算對應的 TimeSpan。
/// </summary>
public TimeSpan CalculateTimeoutDuration(int index) => index switch
{
    0 => TimeSpan.FromMinutes(5),
    1 => TimeSpan.FromMinutes(15),
    2 => TimeSpan.FromMinutes(30),
    _ => TimeSpan.FromMinutes(15)
};
```

**必須**：
- 以純 .NET 型別實作，不依賴 WPF、UI 框架、持久化技術
- 可獨立單元測試，不需啟動 WPF 或資料庫
- 所有業務規則判斷與計算集中於此層，Service 不得自行實作

**禁止**：
- 引用 `PresentationFramework`、`PresentationCore`、`WindowsBase`
- 引用 `System.Windows.*` 任何命名空間
- 呼叫 Service（Domain 不知道外部世界）

### §4.6 Model

**職責**：跨層傳遞的純資料結構（DTO、序列化結構）。Model 不含業務行為。

**必須**：
- 僅含屬性與建構式
- 若含方法，僅限序列化標記或簡單格式投影（如 `ToDisplayString()`）

**禁止**：
- 內嵌業務邏輯
- 直接操作 I/O、資料庫、網路
- 引用 Service 或 Domain 型別

### §4.7 Core

**職責**：Core 存放**跨功能模組共用的應用程式級能力**。當一個類別或工具被兩個以上不同功能模組依賴，且不屬於任何單一功能的業務邏輯，即歸屬 Core。

**判定方式**：

> 「這個能力是否被兩個以上不同功能模組依賴，且不屬於任何單一功能？」
> 是 → `Core/`，否 → 放到對應功能的層次。

**典型內容：**

| 類型 | 說明 | 範例 |
| --- | --- | --- |
| 應用程式設定 | 讀取設定檔，供整個應用程式使用 | `AppSettings.cs` |
| 跨功能擴充方法 | 服務對象是整個應用程式的通用工具 | `QueryableExtensions.cs` |
| 跨功能共用能力 | 任何功能都可能需要的基礎能力 | `OrganizationScope.cs` |

**正確樣態：**

```csharp
/// <summary>
/// 讀取應用程式設定檔的核心能力，供所有 Domain 共用。
/// </summary>
public class AppSettings
{
    public string DatabaseConnection { get; init; } = string.Empty;
    public string LogPath { get; init; } = string.Empty;
}

/// <summary>
/// 條件式查詢擴充，供所有 Domain 的查詢邏輯共用。
/// </summary>
public static class QueryableExtensions
{
    public static IQueryable<T> WhereWhen<T>(
        this IQueryable<T> source,
        bool condition,
        Expression<Func<T, bool>> predicate)
        => condition ? source.Where(predicate) : source;
}
```

**必須**：
- 扁平結構，禁止任何子資料夾
- 只由 `Domain` 呼叫，其他層不得直接引用 Core
- 在 `Extensions/ServiceExtensions.cs` 的 `CoreModule` 中統一註冊

**禁止**：
- 單一功能專屬的邏輯放入 Core（只有一個功能用到的東西屬於該功能的 Domain）
- Views、ViewModels、Services 直接引用 Core 類別
- Core 內含 UI 狀態或業務規則判斷

### §4.8 Components

**職責**：跨 View 共用的 UserControl。

**必須**：至少被 **2 個以上**的 View 使用，才能放入 Components。單一 View 專用的 UserControl 放在該 View 的子目錄內。

**禁止**：元件內部直接存取 Service 或 Domain（應透過 `DependencyProperty` 接收資料）。

### §4.9 Extensions（DI 註冊管理）

**職責**：集中管理所有 DI 註冊，每個層對應一個 Module 擴充方法。`Extensions/` 是唯一允許呼叫 `services.Add*` 的地方。

#### §4.9.1 檔案結構

```
Extensions/
└── ServiceExtensions.cs    所有 Module 方法集中於此一檔案
```

#### §4.9.2 生命週期原則

生命週期依**物件的狀態與資源擁有權**決定，不依層次統一套用：

| 生命週期 | 適用條件 | 典型對象 |
| --- | --- | --- |
| `AddTransient` | 有狀態、與畫面綁定、每次使用需全新實例 | ViewModel |
| `AddSingleton` | 無狀態、可跨畫面共享、或初始化昂貴 | Services、Domain、Core |

#### §4.9.3 結構範本

```csharp
namespace YourProject.Extensions;

public static class ServiceExtensions
{
    /// <summary>DI 註冊管理 ViewModels。</summary>
    public static IServiceCollection ViewModelsModule(this IServiceCollection services)
    {
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<WorkspaceWindowViewModel>();
        return services;
    }

    /// <summary>DI 註冊管理 Services。</summary>
    public static IServiceCollection ServicesModule(this IServiceCollection services)
    {
        services.AddSingleton<SettingsServices>();
        services.AddSingleton<WorkspaceServices>();
        return services;
    }

    /// <summary>DI 註冊管理 Domain。</summary>
    public static IServiceCollection DomainModule(this IServiceCollection services)
    {
        services.AddSingleton<SettingsDomain>();
        services.AddSingleton<WorkspaceDomain>();
        return services;
    }

    /// <summary>DI 註冊管理 Core。</summary>
    public static IServiceCollection CoreModule(this IServiceCollection services)
    {
        services.AddSingleton<AppSettings>();
        return services;
    }
}
```

在 `App.xaml.cs` 或 DI 初始化入口呼叫：

```csharp
services
    .ViewModelsModule()
    .ServicesModule()
    .DomainModule()
    .CoreModule();
```

#### §4.9.4 建構式注入語法

本專案統一使用 .NET 主要建構式（Primary Constructor）語法進行注入，不使用欄位手動賦值：

```csharp
// 正確：Primary Constructor 注入
public class SettingsPageViewModel(SettingsServices services) : ViewModelBase
{
    private void ExecuteSave() => services.SaveSettings(BuildSnapshot());
}

// 禁止：舊式欄位注入
public class SettingsPageViewModel : ViewModelBase
{
    private readonly SettingsServices _services;
    public SettingsPageViewModel(SettingsServices services) { _services = services; }
}
```

#### §4.9.5 規範

**必須**：
- 所有 `services.Add*` 呼叫集中於 `Extensions/ServiceExtensions.cs`
- 每個層對應一個 Module 方法（`ViewModelsModule`、`ServicesModule`、`DomainModule`、`CoreModule`）
- 生命週期依物件狀態與資源擁有權判斷，不得全部統一用同一種

**禁止**：
- 在 ViewModel、Service、Domain 內部 `new` 依賴物件
- 使用 Interface 進行抽象（本專案不使用 Interface，直接注入具體類別）
- 在 `Extensions/` 以外的地方呼叫 `services.Add*`

---

## §5 程式碼規範

### §5.1 XML 文件注釋

所有 `public` 與 `internal` 的類別、方法、屬性必須有 `<summary>` XML 文件注釋。

**正確：**

```csharp
/// <summary>
/// 查詢當前套用的應用程式設定值。
/// </summary>
public AppSettings QuerySettings() => _services.QuerySettings();

/// <summary>
/// 儲存設定。
/// </summary>
/// <param name="settings">欲儲存之設定值快照。</param>
public void SaveSettings(AppSettings settings) => _services.SaveSettings(settings);
```

**禁止：**

```csharp
// 查詢設定（禁止：用行內注釋替代 summary）
public AppSettings QuerySettings() => _services.QuerySettings();

public void SaveSettings(AppSettings settings)
{
    // 先儲存（禁止：方法體內的邏輯說明注釋）
    _services.SaveSettings(settings);
}
```

### §5.2 注釋規則

- 禁止：在方法體內用行內注釋說明邏輯步驟（方法體應自我說明）
- 禁止：用 `/* */` 區塊注釋（統一用 `///` XML 文件注釋）
- 禁止：注釋說明顯而易見的程式碼（如 `// 設定為 true`）

---

## §6 合規判定

下列情況視為不合規，必須修正。

| 編號 | 違規情況 |
| --- | --- |
| V-01 | Views code-behind 執行業務邏輯（方法名出現 Validate、Save、Query 等） |
| V-02 | Views 直接持有或呼叫 Service、Domain 物件 |
| V-03 | ViewModel 引用 `System.Windows.*` 視覺型別 |
| V-04 | ViewModel 直接呼叫 Domain（必須經 Service） |
| V-05 | ViewModel 以 public 方法（非 ICommand）暴露操作給 View |
| V-06 | Service 持有 `IsBusy`、`IsDirty` 等 UI 狀態 |
| V-07 | Service 方法超過 1 行（含任何邏輯、串接或判斷） |
| V-08 | Service 內出現 `if`、`switch`、計算或多個呼叫串接，未下推至 Domain |
| V-09 | Domain 引用 WPF 具體型別（`PresentationFramework`、`System.Windows.*`） |
| V-10 | Domain 呼叫 Service |
| V-11 | Model 內含業務邏輯或 I/O 操作 |
| V-12 | `Domain/`、`Model/`、`Services/`、`Core/` 內出現子資料夾 |
| V-13 | 層內檔案後綴不符規定（如 `*Policy.cs` 在 Domain、`*PageService.cs` 在 Services） |
| V-14 | 同功能主題在同一層拆分為多個檔案 |
| V-15 | `public` / `internal` 類別或方法缺少 `<summary>` XML 文件注釋 |
| V-16 | 方法體內出現邏輯說明的行內注釋 |
| V-17 | 單一 `.csproj` 被拆分為多個專案（未經開發者明確指示） |
| V-18 | 在 ViewModel、Service、Domain 內部 `new` 依賴物件，未透過 DI 注入 |
| V-19 | `services.Add*` 呼叫出現在 `Extensions/ServiceExtensions.cs` 以外的地方 |
| V-20 | 使用 Interface 進行抽象注入（本專案禁止使用 Interface） |
| V-21 | 未使用 Primary Constructor 語法進行注入，改用舊式欄位手動賦值 |
| V-22 | 單一功能專屬的邏輯放入 Core（只有一個功能使用的東西屬於該功能的 Domain） |
| V-23 | Views、ViewModels、Services 直接引用 Core 類別（Core 只允許 Domain 呼叫） |
| V-24 | 建立了沒有對應 View 的 Service（無 View 則無 Service，邏輯直接放 Domain） |