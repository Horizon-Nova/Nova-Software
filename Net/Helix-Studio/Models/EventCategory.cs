namespace Helix_Studio.Models;

/// <summary>
/// 事件類別：區分事件屬於哪一種系統活動，與嚴重程度（EventLevel）正交。
/// </summary>
public enum EventCategory
{
    /// <summary>系統層級：啟動、工作區切換、設定變更。</summary>
    System,

    /// <summary>工具安裝層級：安裝、解除安裝、匯入、升級。</summary>
    Install,

    /// <summary>工具執行層級：啟動、停止、非預期結束。</summary>
    Runtime,
}
