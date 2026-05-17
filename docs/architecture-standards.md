# Windows Presentation Foundation (WPF) 架構規範

| 項目 | 內容 |
| --- | --- |
| 文件編號 | WPF-ARCH-001 |
| 版本 | 2.0 |
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
├── AppHost/             Application Runtime 管理，扁平結構，禁止任何子資料夾
├── Views/               XAML + code-behind，允許一層子資料夾（如 Dialogs/）
├── ViewModels/          UI 狀態機，扁平結構，僅允許 Base/ 子資料夾
│   └── Base/            ViewModelBase、RelayCommand 等基礎設施類別
├── Extensions/          DI 註冊管理，扁平結構，禁止任何子資料夾
├── Domain/              業務規則與判斷，扁平結構，禁止任何子資料夾
├── Data/                資料存取，允許 Repositories/ 子資料夾
│   └── Repositories/    Repository 實作
├── Utils/               純工具方法，扁平結構，禁止任何子資料夾
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
| `AppHost` | `Manager` 或 `Runtime` | `ThemeManager.cs`、`WorkspaceRuntime.cs` | `ThemeService.cs`、`ThemeCore.cs` |
| `Views` | `Page` / `Window` / `Dialog` | `SettingsPage.xaml` | `SettingsView.xaml` |
| `ViewModels` | `PageViewModel` / `WindowViewModel` | `SettingsPageViewModel.cs` | `SettingsVM.cs` |
| `Domain` | `Domain` | `SettingsDomain.cs` | `SettingsLogic.cs`、`SettingsService.cs` |
| `Data/Repositories` | `Repository` | `UserRepository.cs` | `UserRepo.cs`、`UserData.cs` |
| `Model` | `Model` | `SettingsModel.cs` | `AppSettings.cs`（置於 Model 層時） |
| `Utils` | 語意命名（無固定後綴） | `StringHelper.cs`、`IniHelper.cs` | `StringUtil.cs`、`StringCore.cs` |

### §3.2 一個主題，一個檔案

同一功能主題在同一層只能有一個檔案。若同主題存在多個類別，合併至同一檔案。

```
❌ Domain/SettingsDomain.cs + Domain/SettingsValidator.cs
✅ Domain/SettingsDomain.cs（合併，一個主題一個檔案）

❌ AppHost/Theme/ThemeManager.cs（子資料夾）
✅ AppHost/ThemeManager.cs

❌ Data/Repositories/User/UserRepository.cs（子資料夾過深）
✅ Data/Repositories/UserRepository.cs
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
Views → ViewModels → Domain → Data
                       ↓
                     Utils

AppHost（獨立，可被 Views、ViewModels 讀取）
  ↓
Utils
```

**依賴規則：**

- ViewModels 必須透過建構子注入 Domain
- Domain 必須透過建構子注入 Data（Repositories）
- AppHost 不依賴 Domain、ViewModels、Views
- Domain 不依賴 AppHost、ViewModels、Views
- Data 不依賴任何功能層
- Utils 不依賴任何層
- 禁止跨層：Views 不得直接呼叫 Domain 或 Data
- 禁止反向：任何層不得依賴上層

### §4.2 AppHost

**職責**：AppHost 是 **Application Runtime 管理層**，負責 Application 級狀態與 WPF Runtime 操作。

**必須**：
- 管理 Application 級全域狀態（Theme、Workspace、Module）
- 操作 WPF Runtime（`Application.Current`、`Dispatcher`、`ResourceDictionary`）
- 觸發全域事件通知
- 在 `App.xaml.cs` 的 `OnStartup` 中初始化
- 透過 `App.Current` 靜態屬性暴露給其他層

**禁止**：
- 包含業務邏輯或資料驗證
- 呼叫 Domain 或 Data 層
- 持有 ViewModel 狀態（`IsBusy`、`IsDirty` 等）
- 直接操作 UI 元件

**正確樣態：**

```csharp
/// <summary>
/// 主題管理器，負責 Application 級主題切換。
/// </summary>
public class ThemeManager
{
    private string _currentTheme = "Dark";
    
    public string CurrentTheme => _currentTheme;
    
    public event EventHandler<string> ThemeChanged;
    
    /// <summary>
    /// 套用主題至 WPF Runtime。
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        _currentTheme = themeName;
        
        // ✅ AppHost 可操作 Application.Current
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
        };
        
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
        
        ThemeChanged?.Invoke(this, themeName);
        
        // ✅ 持久化透過 Utils
        IniHelper.WriteValue("config.ini", "UI", "Theme", themeName);
    }
}
```

```csharp
// App.xaml.cs
public partial class App : Application
{
    public static ThemeManager ThemeManager { get; private set; }
    public static WorkspaceManager WorkspaceManager { get; private set; }
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 初始化 AppHost
        ThemeManager = new ThemeManager();
        WorkspaceManager = new WorkspaceManager();
        
        ThemeManager.ApplyTheme("Dark");
        
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
```

**反例（禁止）：**

```csharp
// ❌ 禁止：AppHost 包含業務邏輯
public class ThemeManager
{
    public void ApplyTheme(string themeName)
    {
        if (IsEnterpriseUser() && themeName == "Free")
            throw new Exception("企業用戶不可使用免費主題");
    }
}

// ❌ 禁止：AppHost 呼叫 Domain
public class WorkspaceManager
{
    private readonly WorkspaceDomain _domain; // ❌ 不得依賴 Domain
}
```

### §4.3 View

**職責**：呈現畫面、接收使用者輸入、透過資料繫結連接 ViewModel、切換視覺狀態。

**code-behind 只允許**：
- UI 初始化（`InitializeComponent`、事件訂閱）
- 無法以繫結完成的 UI 事件轉發（拖放、複雜滑鼠行為）
- 動畫觸發、視覺狀態切換
- 對話框結果處理

**code-behind 禁止**：
- 執行業務邏輯、資料驗證、資料存取
- 呼叫 Domain 或 Data
- 直接存取檔案、網路、資料庫

**判定式**：code-behind 方法名稱出現以下動詞，即必須移至 ViewModel：
`Validate`、`Save`、`Load`、`Calculate`、`Query`、`Fetch`、`Export`、`Import`

### §4.4 ViewModel

**職責**：ViewModel 是 **UI 狀態機**，負責 UI 狀態管理與使用者交互邏輯。

ViewModel 負責且僅負責以下工作：

1. **可繫結屬性**：持有 View 顯示所需之資料，狀態改變時通知 View 更新
2. **ICommand 封裝**：將使用者操作（按鈕點擊、表單提交）包裝為 Command，在 Command 內呼叫 Domain
3. **UI 執行狀態**：`IsBusy`、`IsLoading`、`HasError` 等 View 切換視覺狀態所需的旗標
4. **Dirty Flag 與欄位驗證訊息**：追蹤使用者是否已修改資料（`IsDirty`）、持有各欄位的驗證錯誤訊息
5. **資料形態轉換**：將 Domain 回傳的資料轉為 View 可直接顯示的型別（`DateTime` → 格式化字串）

**正確樣態：**

```csharp
/// <summary>
/// 設定頁面的 UI 狀態機。
/// </summary>
public sealed class SettingsPageViewModel(SettingsDomain domain) : ViewModelBase
{
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private bool _isDirty;
    public bool IsDirty { get => _isDirty; set => SetProperty(ref _isDirty, value); }

    private int _languageIndex;
    public int LanguageIndex
    {
        get => _languageIndex;
        set { SetProperty(ref _languageIndex, value); IsDirty = true; }
    }

    private string _logFolderError = string.Empty;
    public string LogFolderError
    {
        get => _logFolderError;
        set => SetProperty(ref _logFolderError, value);
    }

    public ICommand SaveCommand { get; }

    public SettingsPageViewModel()
    {
        SaveCommand = new RelayCommand(ExecuteSave, () => IsDirty && !IsBusy);
    }

    private void ExecuteSave()
    {
        if (!ValidateForm()) return;
        IsBusy = true;
        domain.SaveSettings(BuildSettingsSnapshot());
        IsDirty = false;
        IsBusy = false;
    }

    private bool ValidateForm()
    {
        LogFolderError = Directory.Exists(LogExportFolder) ? string.Empty : "路徑不存在";
        return string.IsNullOrEmpty(LogFolderError);
    }
}
```

**必須**：
- 繼承 `ViewModelBase`（已實作 `INotifyPropertyChanged`）
- 透過建構子注入 Domain（使用 Primary Constructor 語法）
- 使用者操作以 `ICommand` 封裝，不以 public 方法直接暴露給 View
- `IsBusy`、`IsDirty`、欄位驗證訊息在 ViewModel 持有與管理

**禁止**：
- 引用 `System.Windows.Controls`、`System.Windows.Media` 等 WPF 視覺命名空間
- 直接呼叫 `MessageBox.Show`
- 在 ViewModel 寫業務規則判斷（應在 Domain）
- 在 ViewModel 內部 `new` Domain 物件（必須透過建構子注入）

### §4.5 Domain

**職責**：Domain 是**所有功能邏輯的唯一實作地點**，包含業務規則、判斷、計算、流程組合。

**必須**：
- 透過建構子注入 Data（Repositories）
- 以純 .NET 型別實作，不依賴 WPF、UI 框架
- 可獨立單元測試，不需啟動 WPF 或資料庫
- 所有業務規則判斷與計算集中於此層

**禁止**：
- 引用 `PresentationFramework`、`PresentationCore`、`WindowsBase`
- 引用 `System.Windows.*` 任何命名空間
- 持有 UI 狀態（`IsBusy`、`IsDirty` 等）
- 依賴 AppHost、ViewModel、View

**正確樣態：**

```csharp
/// <summary>
/// 設定相關業務邏輯。
/// </summary>
public class SettingsDomain(SettingsRepository repository)
{
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

    /// <summary>
    /// 儲存設定。
    /// </summary>
    public void SaveSettings(SettingsModel settings)
    {
        repository.Save(settings);
    }

    /// <summary>
    /// 查詢設定。
    /// </summary>
    public SettingsModel QuerySettings()
    {
        return repository.Get();
    }
}
```

### §4.6 Data

**職責**：Data 層負責資料持久化與讀取。

**必須**：
- Repository 必須透過建構子注入 DbContext（若使用資料庫）
- 使用 Primary Constructor 語法
- 方法名稱使用明確動詞前綴（Get, Save, Delete 等）

**禁止**：
- 包含業務邏輯
- 資料驗證（應在 Domain）
- 使用 Interface 抽象（本專案禁止使用 Interface）

**正確樣態：**

```csharp
/// <summary>
/// 設定資料存取。
/// </summary>
public class SettingsRepository
{
    private readonly string _iniPath = "config.ini";
    
    public SettingsModel Get()
    {
        return new SettingsModel
        {
            Language = IniHelper.ReadValue(_iniPath, "UI", "Language"),
            Theme = IniHelper.ReadValue(_iniPath, "UI", "Theme")
        };
    }
    
    public void Save(SettingsModel settings)
    {
        IniHelper.WriteValue(_iniPath, "UI", "Language", settings.Language);
        IniHelper.WriteValue(_iniPath, "UI", "Theme", settings.Theme);
    }
}
```

```csharp
/// <summary>
/// 使用者資料存取（使用 EF Core）。
/// </summary>
public class UserRepository(AppDbContext context)
{
    public async Task<User> GetByIdAsync(int id)
    {
        return await context.Users.FindAsync(id);
    }
    
    public async Task SaveAsync(User user)
    {
        if (user.Id == 0)
            context.Users.Add(user);
        else
            context.Users.Update(user);
            
        await context.SaveChangesAsync();
    }
}
```

### §4.7 Model

**職責**：跨層傳遞的純資料結構（DTO、序列化結構）。Model 不含業務行為。

**必須**：
- 僅含屬性與建構式
- 若含方法，僅限序列化標記或簡單格式投影（如 `ToDisplayString()`）

**禁止**：
- 內嵌業務邏輯
- 直接操作 I/O、資料庫、網路
- 引用 Domain 型別

**正確樣態：**

```csharp
/// <summary>
/// 設定資料模型。
/// </summary>
public record SettingsModel
{
    public string Language { get; init; } = "zh-TW";
    public string Theme { get; init; } = "Dark";
    public string LogPath { get; init; } = string.Empty;
}
```

### §4.8 Utils

**職責**：Utils 存放**純工具方法**，無狀態，任何層都可使用。

**必須**：
- 靜態方法
- 無狀態
- 不依賴任何功能層

**禁止**：
- 持有狀態
- 包含業務邏輯
- 依賴 AppHost、Domain、ViewModel、Data

**正確樣態：**

```csharp
/// <summary>
/// INI 檔案操作工具。
/// </summary>
public static class IniHelper
{
    [DllImport("kernel32")]
    private static extern int GetPrivateProfileString(
        string section, string key, string def,
        StringBuilder retVal, int size, string filePath);
        
    [DllImport("kernel32")]
    private static extern long WritePrivateProfileString(
        string section, string key, string val, string filePath);
    
    public static string ReadValue(string filePath, string section, string key)
    {
        var temp = new StringBuilder(255);
        GetPrivateProfileString(section, key, "", temp, 255, filePath);
        return temp.ToString();
    }
    
    public static void WriteValue(string filePath, string section, string key, string value)
    {
        WritePrivateProfileString(section, key, value, filePath);
    }
}
```

```csharp
/// <summary>
/// 字串處理工具。
/// </summary>
public static class StringHelper
{
    public static bool IsNullOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
    
    public static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
            
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }
}
```

### §4.9 Components

**職責**：跨 View 共用的 UserControl。

**必須**：至少被 **2 個以上**的 View 使用，才能放入 Components。單一 View 專用的 UserControl 放在該 View 的子目錄內。

**禁止**：元件內部直接存取 Domain 或 Data（應透過 `DependencyProperty` 接收資料）。

### §4.10 Extensions（DI 註冊管理）

**職責**：集中管理所有 DI 註冊，每個層對應一個 Module 擴充方法。`Extensions/` 是唯一允許呼叫 `services.Add*` 的地方。

#### §4.10.1 檔案結構

```
Extensions/
└── ServiceExtensions.cs    所有 Module 方法集中於此一檔案
```

#### §4.10.2 生命週期原則

生命週期依**物件的狀態與資源擁有權**決定，不依層次統一套用：

| 生命週期 | 適用條件 | 典型對象 |
| --- | --- | --- |
| `AddTransient` | 有狀態、與畫面綁定、每次使用需全新實例 | ViewModel |
| `AddSingleton` | 無狀態、可跨畫面共享、或初始化昂貴 | Domain、Data、AppHost |

#### §4.10.3 結構範本

```csharp
namespace YourProject.Extensions;

public static class ServiceExtensions
{
    /// <summary>DI 註冊管理 AppHost。</summary>
    public static IServiceCollection AppHostModule(this IServiceCollection services)
    {
        services.AddSingleton<ThemeManager>();
        services.AddSingleton<WorkspaceManager>();
        return services;
    }

    /// <summary>DI 註冊管理 ViewModels。</summary>
    public static IServiceCollection ViewModelsModule(this IServiceCollection services)
    {
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<WorkspaceWindowViewModel>();
        return services;
    }

    /// <summary>DI 註冊管理 Domain。</summary>
    public static IServiceCollection DomainModule(this IServiceCollection services)
    {
        services.AddSingleton<SettingsDomain>();
        services.AddSingleton<WorkspaceDomain>();
        return services;
    }

    /// <summary>DI 註冊管理 Data。</summary>
    public static IServiceCollection DataModule(this IServiceCollection services)
    {
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<UserRepository>();
        return services;
    }
}
```

在 `App.xaml.cs` 或 DI 初始化入口呼叫：

```csharp
services
    .AppHostModule()
    .ViewModelsModule()
    .DomainModule()
    .DataModule();
```

#### §4.10.4 建構式注入語法

本專案統一使用 .NET 主要建構式（Primary Constructor）語法進行注入，不使用欄位手動賦值：

```csharp
// ✅ 正確：Primary Constructor 注入
public class SettingsPageViewModel(SettingsDomain domain) : ViewModelBase
{
    private void ExecuteSave() => domain.SaveSettings(BuildSnapshot());
}

// ❌ 禁止：舊式欄位注入
public class SettingsPageViewModel : ViewModelBase
{
    private readonly SettingsDomain _domain;
    public SettingsPageViewModel(SettingsDomain domain) { _domain = domain; }
}
```

#### §4.10.5 規範

**必須**：
- 所有 `services.Add*` 呼叫集中於 `Extensions/ServiceExtensions.cs`
- 每個層對應一個 Module 方法（`AppHostModule`、`ViewModelsModule`、`DomainModule`、`DataModule`）
- 生命週期依物件狀態與資源擁有權判斷，不得全部統一用同一種

**禁止**：
- 在 ViewModel、Domain、Data 內部 `new` 依賴物件
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
public SettingsModel QuerySettings() => repository.Get();

/// <summary>
/// 儲存設定。
/// </summary>
/// <param name="settings">欲儲存之設定值快照。</param>
public void SaveSettings(SettingsModel settings) => repository.Save(settings);
```

**禁止：**

```csharp
// 查詢設定（禁止：用行內注釋替代 summary）
public SettingsModel QuerySettings() => repository.Get();

public void SaveSettings(SettingsModel settings)
{
    // 先儲存（禁止：方法體內的邏輯說明注釋）
    repository.Save(settings);
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
| V-02 | Views 直接持有或呼叫 Domain、Data 物件 |
| V-03 | ViewModel 引用 `System.Windows.*` 視覺型別 |
| V-04 | ViewModel 以 public 方法（非 ICommand）暴露操作給 View |
| V-05 | ViewModel 在內部 `new` Domain 物件（必須透過建構子注入） |
| V-06 | Domain 引用 WPF 具體型別（`PresentationFramework`、`System.Windows.*`） |
| V-07 | Domain 持有 UI 狀態（`IsBusy`、`IsDirty` 等） |
| V-08 | Domain 依賴 AppHost、ViewModel、View |
| V-09 | Data 包含業務邏輯或資料驗證 |
| V-10 | Model 內含業務邏輯或 I/O 操作 |
| V-11 | AppHost 包含業務邏輯或資料驗證 |
| V-12 | AppHost 呼叫 Domain 或 Data |
| V-13 | AppHost 持有 ViewModel 狀態 |
| V-14 | Utils 持有狀態或依賴其他層 |
| V-15 | `Domain/`、`Model/`、`AppHost/`、`Utils/` 內出現子資料夾 |
| V-16 | 層內檔案後綴不符規定 |
| V-17 | 同功能主題在同一層拆分為多個檔案 |
| V-18 | `public` / `internal` 類別或方法缺少 `<summary>` XML 文件注釋 |
| V-19 | 方法體內出現邏輯說明的行內注釋 |
| V-20 | 單一 `.csproj` 被拆分為多個專案（未經開發者明確指示） |
| V-21 | 在 ViewModel、Domain、Data 內部 `new` 依賴物件，未透過 DI 注入 |
| V-22 | `services.Add*` 呼叫出現在 `Extensions/ServiceExtensions.cs` 以外的地方 |
| V-23 | 使用 Interface 進行抽象注入（本專案禁止使用 Interface） |
| V-24 | 未使用 Primary Constructor 語法進行注入，改用舊式欄位手動賦值 |