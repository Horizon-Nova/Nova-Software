namespace Helix_Studio.Model;

/// <summary>模組資料模型。</summary>
public sealed record ModuleModel
{
    /// <summary>模組識別碼。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>版本字串。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>作者。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>說明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>最低主程式版本。</summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>模組資料夾路徑。</summary>
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>入口 DLL 路徑。</summary>
    public string DllPath { get; init; } = string.Empty;

    /// <summary>是否需要授權。</summary>
    public bool RequiresLicense { get; init; }
}

/// <summary>
/// 模組頁面用的已安裝模組項目。
/// </summary>
public sealed record ModulesInstalledItemModel
{
    /// <summary>
    /// 模組識別值。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 模組顯示名稱。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 模組版本文字。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 模組作者。
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// 模組描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 最低殼層版本。
    /// </summary>
    public string MinHostVersion { get; init; } = string.Empty;

    /// <summary>
    /// 安裝資料夾路徑。
    /// </summary>
    public string InstalledFolderPath { get; init; } = string.Empty;

    /// <summary>
    /// 模組狀態文字。
    /// </summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>
    /// 模組圖示位元組。
    /// </summary>
    public byte[]? Icon { get; init; }

    /// <summary>
    /// README Markdown 文字。
    /// </summary>
    public string ReadmeText { get; init; } = string.Empty;
}
