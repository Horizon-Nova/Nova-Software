namespace Helix_Studio.Models;

/// <summary>工作區應用程式的執行狀態。</summary>
public enum AppStatus
{
    Stopped,
    Starting,
    Running,
    Error,
    Locked,
}
