namespace Helix_Studio.Model;

/// <summary>
/// 主設定中的 Main 區段資料模型。
/// </summary>
public sealed record MainModel
{
    /// <summary>
    /// 目前啟用的工作區名稱。
    /// </summary>
    public string Workspace { get; set; } = string.Empty;
}
