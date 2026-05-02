namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>模組唯一識別（反向網域格式），例如「com.horizonnova.demo」。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleIdAttribute(string id) : Attribute
{
    /// <summary>模組識別字串。</summary>
    public string Id { get; } = id;
}

/// <summary>側邊欄與模組管理員中顯示的可讀名稱。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleDisplayNameAttribute(string displayName) : Attribute
{
    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; } = displayName;
}

/// <summary>語意化版本字串，例如「1.2.3」。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleVersionAttribute(string version) : Attribute
{
    /// <summary>版本字串。</summary>
    public string Version { get; } = version;
}

/// <summary>詳細面板中顯示的作者或組織名稱。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleAuthorAttribute(string author) : Attribute
{
    /// <summary>作者名稱或組織。</summary>
    public string Author { get; } = author;
}

/// <summary>模組詳細面板中顯示的簡短說明。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleDescriptionAttribute(string description) : Attribute
{
    /// <summary>說明文字。</summary>
    public string Description { get; } = description;
}

/// <summary>所需的 Helix Studio 主程式最低版本，例如「1.0.0」。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleMinHostVersionAttribute(string minVersion) : Attribute
{
    /// <summary>最低主程式版本字串。</summary>
    public string MinVersion { get; } = minVersion;
}

/// <summary>
/// 作為側邊欄圖示之內嵌 PNG 資源名稱，例如「MyNamespace.Assets.icon.png」；若省略則殼層使用預設圖示。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleIconResourceAttribute(string resourceName) : Attribute
{
    /// <summary>內嵌資源完整名稱。</summary>
    public string ResourceName { get; } = resourceName;
}

/// <summary>
/// 宣告模組須具備有效的硬體綁定授權金鑰後，殼層才會啟用；若省略則視為無須授權即可啟用。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleRequiresLicenseAttribute : Attribute { }

