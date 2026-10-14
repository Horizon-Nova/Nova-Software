using Microsoft.EntityFrameworkCore;

namespace Vaeron.Kernel.Data.Models;

/// <summary>Vw_Connection 映射。分類與顯示文字由 View 算好，取用端只讀欄位。</summary>
[Keyless]
public sealed class ConnectionRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; }
    public byte[] PasswordCipher { get; set; } = [];
    public int Status { get; set; }
    public string StatusText { get; set; } = "";
    public int IsAbnormal { get; set; }
    public DateTimeOffset? LastTestedAt { get; set; }
    public string LastTestedDisplay { get; set; } = "";
    public string LastTestMessage { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string? GroupName { get; set; }
    public string GroupDisplayName { get; set; } = "";
}

/// <summary>Vw_ConnectionGroup 映射。目前正被使用的群組名稱，群組不是獨立實體。</summary>
[Keyless]
public sealed class ConnectionGroupRow
{
    public string Name { get; set; } = "";
}

/// <summary>Vw_EventLog 映射。</summary>
[Keyless]
public sealed class EventLogRow
{
    public int Id { get; set; }
    public long OccurredUtcTicks { get; set; }
    public int Level { get; set; }
    public string LevelText { get; set; } = "";
    public string OccurredDisplay { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string Detail { get; set; } = "";
    public int HasDetail { get; set; }
    public int? RelatedConnectionId { get; set; }
}

/// <summary>Vw_ConnectionRecentEvent 映射。每個連線最近幾筆，筆數由 View 決定。</summary>
[Keyless]
public sealed class ConnectionRecentEventRow
{
    public int Id { get; set; }
    public long OccurredUtcTicks { get; set; }
    public int Level { get; set; }
    public string LevelText { get; set; } = "";
    public string OccurredDisplay { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string Detail { get; set; } = "";
    public int HasDetail { get; set; }
    public int RelatedConnectionId { get; set; }
}

/// <summary>Vw_DeploymentHistory 映射。</summary>
[Keyless]
public sealed class DeploymentHistoryRow
{
    public int Id { get; set; }
    public long DeployedTicks { get; set; }
    public string DeployedDisplay { get; set; } = "";
    public int ConnectionId { get; set; }
    public string TargetName { get; set; } = "";
    public string TargetIp { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string RemotePath { get; set; } = "";
    public bool Success { get; set; }
    public string SuccessText { get; set; } = "";
    public string Message { get; set; } = "";
    public string OperatedBy { get; set; } = "";
}
