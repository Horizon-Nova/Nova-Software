namespace Helix_Studio.Model;

/// <summary>
/// 模組健康狀態。
/// </summary>
public enum ModuleHealth
{
    /// <summary>
    /// 正常。
    /// </summary>
    Healthy,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 故障。
    /// </summary>
    Faulted,
}

/// <summary>
/// 模組中繼資料。
/// </summary>
public sealed record ModuleModel
{
    /// <summary>
    /// 模組識別碼。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 顯示名稱。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 版本字串。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 開發者名稱。
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// 模組描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 最低主機版本。
    /// </summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>
    /// 模組資料夾路徑。
    /// </summary>
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>
    /// 模組 DLL 路徑。
    /// </summary>
    public string DllPath { get; init; } = string.Empty;

    /// <summary>
    /// 是否需要授權。
    /// </summary>
    public bool RequiresLicense { get; init; }

    /// <summary>
    /// 是否可被主機載入。
    /// </summary>
    public bool IsLoadable { get; init; }

    /// <summary>
    /// 是否為啟用狀態。
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 是否提供設定頁。
    /// </summary>
    public bool HasSettings { get; init; }
}

/// <summary>
/// Modules 頁使用的已安裝模組項目。
/// </summary>
public sealed record ModulesInstalledItemModel
{
    /// <summary>
    /// 模組識別碼。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 顯示名稱。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 版本字串。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 開發者名稱。
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// 模組描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 最低主機版本。
    /// </summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>
    /// 已安裝模組資料夾。
    /// </summary>
    public string InstalledFolderPath { get; init; } = string.Empty;

    /// <summary>
    /// 模組 DLL 路徑。
    /// </summary>
    public string DllPath { get; init; } = string.Empty;

    /// <summary>
    /// 狀態文字。
    /// </summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>
    /// 模組圖示資料。
    /// </summary>
    public byte[]? Icon { get; init; }

    /// <summary>
    /// README 內容。
    /// </summary>
    public string ReadmeText { get; init; } = string.Empty;

    /// <summary>
    /// 是否可被主機載入。
    /// </summary>
    public bool IsLoadable { get; init; }

    /// <summary>
    /// 是否為啟用狀態。
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 是否提供設定頁。
    /// </summary>
    public bool HasSettings { get; init; }
}
