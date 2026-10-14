namespace Vaeron.Kernel.Data.Models;

/// <summary>connection 主表映射。欄位對映集中在 VncscpDbContext，型別本身不帶映射屬性。</summary>
public sealed class ConnectionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; }

    /// <summary>DPAPI 加密後的密碼密文。</summary>
    public byte[] PasswordCipher { get; set; } = [];

    public int Status { get; set; }
    public DateTimeOffset? LastTestedAt { get; set; }
    public string LastTestMessage { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>所屬群組名稱，null 表示未分組。</summary>
    public string? GroupName { get; set; }
}

/// <summary>event_log 主表映射。</summary>
public sealed class EventLogEntity
{
    public int Id { get; set; }

    /// <summary>發生時間，以 UTC ticks 存放。</summary>
    public long OccurredUtcTicks { get; set; }

    public int Level { get; set; }
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string Detail { get; set; } = "";

    /// <summary>此事件關聯的連線；null 表示與特定連線無關。</summary>
    public int? RelatedConnectionId { get; set; }
}

/// <summary>deployment_history 主表映射。一列對應一次真正送出的傳輸嘗試，不是一個任務。</summary>
public sealed class DeploymentHistoryEntity
{
    public int Id { get; set; }

    /// <summary>部署時間，以 UTC ticks 存放。</summary>
    public long DeployedTicks { get; set; }

    public int ConnectionId { get; set; }
    public string TargetName { get; set; } = "";

    /// <summary>格式 ip:port，純顯示用。</summary>
    public string TargetIp { get; set; } = "";

    public string LocalPath { get; set; } = "";
    public string RemotePath { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string OperatedBy { get; set; } = "";
}
