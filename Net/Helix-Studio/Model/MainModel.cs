namespace Helix_Studio.Model;

/// <summary>
/// 主視窗設定模型。
/// </summary>
public sealed record MainModel
{
    /// <summary>
    /// 使用中的工作區名稱。
    /// </summary>
    public string Workspace { get; set; } = string.Empty;

    /// <summary>
    /// 使用中的工作區描述。
    /// </summary>
    public string WorkspaceDescription { get; set; } = string.Empty;
}
