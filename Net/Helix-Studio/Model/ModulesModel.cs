using System.Text.Json;
namespace Helix_Studio.Model.Modules;

/// <summary>模組管理列表中單一模組列項目。</summary>
public sealed class ModuleListItem
{
    /// <summary>模組識別碼。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>版本字串。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>狀態顯示文字。</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>是否已啟用載入。</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>作者資訊。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>模組說明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>所需主程式最低版本。</summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>圖示 PNG 位元組（可為 null）。</summary>
    public byte[]? Icon { get; init; }

    /// <summary>安裝資料夾完整路徑。</summary>
    public string InstalledFolderPath { get; init; } = string.Empty;

    /// <summary>README 文字內容。</summary>
    public string ReadmeText { get; init; } = string.Empty;

    /// <summary>是否具有 README 內容。</summary>
    public bool HasReadme => !string.IsNullOrEmpty(ReadmeText);
}

/// <summary>舊版模組封包 manifest（JSON 反序列化結構）。</summary>
public sealed class ModulePackageManifest
{
    /// <summary>模組識別碼。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>版本字串。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>說明文字。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>圖示檔名。</summary>
    public string IconFile { get; init; } = string.Empty;

    /// <summary>進入點組件檔名。</summary>
    public string EntryAssemblyFile { get; init; } = string.Empty;

    /// <summary>進入點型別名稱。</summary>
    public string EntryType { get; init; } = string.Empty;

    /// <summary>README 檔名。</summary>
    public string ReadmeFile { get; init; } = string.Empty;

    /// <summary>所需主程式最低版本。</summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>檢視區尺寸 manifest。</summary>
    public ModulePackageViewManifest View { get; init; } = new();

    /// <summary>設定項目 manifest 陣列。</summary>
    public ModulePackageSettingManifest[] Settings { get; init; } = [];
}

/// <summary>解析後之模組封包描述（供 UI 與安裝流程）。</summary>
public sealed class ModulePackageDescriptor
{
    /// <summary>模組識別碼。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>版本字串。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>說明文字。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>封包資料夾路徑。</summary>
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>導覽路由標籤。</summary>
    public string RouteTag { get; init; } = string.Empty;

    /// <summary>圖示檔案完整路徑。</summary>
    public string IconFilePath { get; init; } = string.Empty;

    /// <summary>進入點組件完整路徑。</summary>
    public string EntryAssemblyFilePath { get; init; } = string.Empty;

    /// <summary>進入點型別名稱。</summary>
    public string EntryType { get; init; } = string.Empty;

    /// <summary>README 檔案完整路徑。</summary>
    public string ReadmeFilePath { get; init; } = string.Empty;

    /// <summary>README 文字內容。</summary>
    public string ReadmeContent { get; init; } = string.Empty;

    /// <summary>所需主程式最低版本。</summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>是否標記為停用。</summary>
    public bool IsDisabled { get; init; }

    /// <summary>檢視區尺寸描述。</summary>
    public ModulePackageViewDescriptor View { get; init; } = new();

    /// <summary>設定項目描述清單。</summary>
    public IReadOnlyList<ModulePackageSettingDescriptor> Settings { get; init; } = [];
}

/// <summary>封包 manifest 中之建議視窗尺寸。</summary>
public sealed class ModulePackageViewManifest
{
    /// <summary>偏好寬度。</summary>
    public double PreferredWidth { get; init; }

    /// <summary>偏好高度。</summary>
    public double PreferredHeight { get; init; }

    /// <summary>最小寬度。</summary>
    public double MinWidth { get; init; }

    /// <summary>最小高度。</summary>
    public double MinHeight { get; init; }
}

/// <summary>封包描述中之建議視窗尺寸。</summary>
public sealed class ModulePackageViewDescriptor
{
    /// <summary>偏好寬度。</summary>
    public double PreferredWidth { get; init; }

    /// <summary>偏好高度。</summary>
    public double PreferredHeight { get; init; }

    /// <summary>最小寬度。</summary>
    public double MinWidth { get; init; }

    /// <summary>最小高度。</summary>
    public double MinHeight { get; init; }
}

/// <summary>封包 manifest 中單一設定欄位定義。</summary>
public sealed class ModulePackageSettingManifest
{
    /// <summary>設定鍵。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>說明文字。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>值型別字串。</summary>
    public string ValueType { get; init; } = string.Empty;

    /// <summary>預設值（JSON）。</summary>
    public JsonElement DefaultValue { get; init; }

    /// <summary>最小值（JSON）。</summary>
    public JsonElement MinValue { get; init; }

    /// <summary>最大值（JSON）。</summary>
    public JsonElement MaxValue { get; init; }
}

/// <summary>封包描述中單一設定欄位（已解析型別與字串邊界）。</summary>
public sealed class ModulePackageSettingDescriptor
{
    /// <summary>設定鍵。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>說明文字。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>值型別列舉。</summary>
    public ModulePackageSettingValueType ValueType { get; init; }

    /// <summary>預設值字串。</summary>
    public string DefaultValue { get; init; } = string.Empty;

    /// <summary>最小值字串。</summary>
    public string MinValue { get; init; } = string.Empty;

    /// <summary>最大值字串。</summary>
    public string MaxValue { get; init; } = string.Empty;
}

/// <summary>模組封包設定值型別。</summary>
public enum ModulePackageSettingValueType
{
    /// <summary>字串。</summary>
    String,

    /// <summary>整數。</summary>
    Integer,

    /// <summary>浮點數。</summary>
    Double,

    /// <summary>布林。</summary>
    Boolean
}

/// <summary>匯入模組封包之結果。</summary>
public sealed class ModulePackageImportResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>訊息資源鍵。</summary>
    public string MessageResourceKey { get; init; } = string.Empty;

    /// <summary>詳細說明文字。</summary>
    public string DetailText { get; init; } = string.Empty;
}

/// <summary>模組封包操作（停用／解除安裝等）之結果。</summary>
public sealed class ModulePackageActionResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>訊息資源鍵。</summary>
    public string MessageResourceKey { get; init; } = string.Empty;

    /// <summary>詳細說明文字。</summary>
    public string DetailText { get; init; } = string.Empty;
}

/// <summary>模組設定寫入結果（殼層內部模型，非 SDK）。</summary>
public sealed class ModuleSettingSaveResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>已儲存值字串。</summary>
    public string SavedValue { get; init; } = string.Empty;

    /// <summary>詳細說明或錯誤文字。</summary>
    public string DetailText { get; init; } = string.Empty;
}
