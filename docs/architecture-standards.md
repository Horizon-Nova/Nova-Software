# Windows Presentation Foundation (WPF) 架構規範

| 項目 | 內容 |
| --- | --- |
| 文件編號 | WPF-ARCH-001 |
| 版本 | 1.0 |
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
├── Services/            薄 Facade，扁平結構，禁止任何子資料夾
├── Domain/              業務邏輯，扁平結構，禁止任何子資料夾
├── Model/               資料結構，扁平結構，禁止任何子資料夾
├── Infrastructure/      基礎設施，允許依技術類型分子資料夾，不超過一層
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
| `Services` | `Services`（複數） | `SettingsServices.cs` | `SettingsPageService.cs`、`SettingsRuntimeService.cs` |
| `Model` | `Model` | `SettingsModel.cs` | `AppSettings.cs`（置於 Model 層時） |
| `Domain` | `Domain` | `SettingsDomain.cs` | `SettingsPolicy.cs`、`SettingsLogic.cs`、`SettingsRule.cs` |

### §3.2 一個主題，一個檔案

同一功能主題在同一層只能有一個檔案。若同主題存在多個類別，合併至同一檔案。

```
❌ Services/SettingsPageService.cs + Services/SettingsRuntimeService.cs
✅ Services/SettingsServices.cs（合併）

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

**禁止** `Process`、`Handle`、`Do`、`Execute`、`Run`、`Manager` 作為函式名稱的主動詞。

---

## §4 各層職責

### §4.1 依賴方向

```
Views → ViewModels → Services → Domain → Model
                                       ↑
                               Infrastructure 實作 Domain 定義的介面
```

- 禁止跳層：Views 不得直接呼叫 Services 或 Domain
- 禁止跳層：ViewModels 不得直接呼叫 Domain 或 Infrastructure
- 禁止反向：Domain 不得依賴 Services、ViewModels、Views
- 禁止反向：Model 不得依賴任何其他功能層

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
- 直接呼叫 Domain 或 Infrastructure（必須經 Service）

### §4.4 Services

**職責**：Service 是 ViewModel 與 Domain 之間的薄 Facade，負責組合 Domain 呼叫，提供穩定的操作入口。Service 不知道 UI 存在。

**必須**：
- 方法以一或多個 Domain 方法呼叫為主體，整合結果後回傳
- 不承擔業務規則判斷（業務規則屬 Domain）

**禁止**：
- 持有 `IsBusy`、`IsDirty` 等 UI 狀態
- 累積業務判斷邏輯（成為第二套 Domain）

**判定式**：Service 方法若超過 **20 行**，或包含超過 **3 個 `if` 分支**，檢查是否有業務邏輯應下推至 Domain。

### §4.5 Domain

**職責**：業務邏輯、規則、計算、解析的所在層。Domain 不知道 UI 存在，也不知道持久化方式。

**必須**：
- 以純 .NET 型別實作，不依賴 WPF、UI 框架、持久化技術
- 可獨立單元測試，不需啟動 WPF 或資料庫

**禁止**：
- 引用 `PresentationFramework`、`PresentationCore`、`WindowsBase`
- 引用 Entity Framework、Dapper 等持久化框架的具體型別
- 引用 `System.Windows.*` 任何命名空間

**Domain 與 Service 的界線：**

| 情境 | 歸屬 |
| --- | --- |
| 「語言索引 0 對應繁體中文」的對應規則 | Domain |
| 「取得設定 → 套用語言 → 儲存設定」的流程串接 | Service |
| 「非活躍逾時時間由 index 換算為 TimeSpan」 | Domain |
| 「儲存設定並即時套用 UI 配置」的協調 | Service |

### §4.6 Model

**職責**：跨層傳遞的純資料結構（DTO、序列化結構）。Model 不含業務行為。

**必須**：
- 僅含屬性與建構式
- 若含方法，僅限序列化標記或簡單格式投影（如 `ToDisplayString()`）

**禁止**：
- 內嵌業務邏輯
- 直接操作 I/O、資料庫、網路
- 引用 Service 或 Domain 型別

### §4.7 Infrastructure

**職責**：基礎設施機制，包含 DI 容器、持久化實作、模組載入器、外部服務封裝、平台工具。

**必須**：
- 實作 Domain 定義的介面，供 DI 注入
- 將平台相依（WPF API、作業系統 API、檔案系統）隔離於此層

**禁止**：
- 定義業務規則（屬 Domain）
- 被 Domain 層反向依賴

### §4.8 Components

**職責**：跨 View 共用的 UserControl。

**必須**：至少被 **2 個以上**的 View 使用，才能放入 Components。單一 View 專用的 UserControl 放在該 View 的子目錄內。

**禁止**：元件內部直接存取 Service 或 Domain（應透過 `DependencyProperty` 接收資料）。

---

## §5 程式碼規範

### §5.1 XML 文件注釋

所有 `public` 與 `internal` 的類別、方法、屬性必須有 `<summary>` XML 文件注釋。

**正確：**

```csharp
/// <summary>
/// 查詢當前套用的應用程式設定值。
/// </summary>
public AppSettings QuerySettings() => _service.QuerySettings();

/// <summary>
/// 儲存設定並即時套用 UI 配置（語言、主題、縮放）。
/// </summary>
/// <param name="settings">欲儲存之設定值快照。</param>
public void SaveSettings(AppSettings settings)
{
    _service.Save(settings);
    SettingsRuntimeServices.Apply(settings);
}
```

**禁止：**

```csharp
// 查詢設定（禁止：用行內注釋替代 summary）
public AppSettings QuerySettings() => _service.QuerySettings();

public void SaveSettings(AppSettings settings)
{
    // 先儲存（禁止：方法體內的邏輯說明注釋）
    _service.Save(settings);
    // 再套用 UI
    SettingsRuntimeServices.Apply(settings);
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
| V-04 | ViewModel 直接呼叫 Domain 或 Infrastructure |
| V-05 | ViewModel 以 public 方法（非 ICommand）暴露操作給 View |
| V-06 | Service 持有 `IsBusy`、`IsDirty` 等 UI 狀態 |
| V-07 | Service 方法超過 20 行或 3 個 if 分支且含業務判斷 |
| V-08 | Domain 引用 WPF 或持久化框架具體型別 |
| V-09 | Model 內含業務邏輯或 I/O 操作 |
| V-10 | `Domain/`、`Model/`、`Services/` 內出現子資料夾 |
| V-11 | 層內檔案後綴不符規定（如 `*Policy.cs` 在 Domain、`*PageService.cs` 在 Services） |
| V-12 | 同功能主題在同一層拆分為多個檔案 |
| V-13 | `public` / `internal` 類別或方法缺少 `<summary>` XML 文件注釋 |
| V-14 | 方法體內出現邏輯說明的行內注釋 |
| V-15 | 單一 `.csproj` 被拆分為多個專案（未經開發者明確指示） |
