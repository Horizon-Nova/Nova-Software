namespace Helix_Studio.Model.Workspace;

/// <summary>工作區列表與編輯所用之資料模型。</summary>
public class WorkspaceModel
{
    /// <summary>資料庫識別。</summary>
    public int    Id          { get; set; }

    /// <summary>工作區名稱。</summary>
    public string Name        { get; set; } = string.Empty;

    /// <summary>工作區描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否為目前使用中工作區。</summary>
    public bool   IsActive    { get; set; }

    /// <summary>建立時間顯示字串。</summary>
    public string CreatedAt   { get; set; } = string.Empty;
}
