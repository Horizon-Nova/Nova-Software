using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;

namespace Vaeron.VNCSCP.Internal.Contracts;

/// <summary>連線狀態。反映上次測試的結果，不代表現在連不連得上。狀態文字由 Vw_Connection 產生。</summary>
public enum ConnectionStatus
{
    Unknown,
    Reachable,
    WrongPassword,
    Locked,
    Unreachable
}

/// <summary>一筆連線的唯讀快照。顯示文字取自 View，不在此端二次加工。</summary>
public sealed record ConnectionSnapshot
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public required ConnectionStatus Status { get; init; }
    public required string StatusText { get; init; }
    public DateTimeOffset? LastTestedAt { get; init; }
    public required string LastTestedDisplay { get; init; }
    public required string LastTestMessage { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? GroupName { get; init; }
    public required string GroupDisplayName { get; init; }
}

/// <summary>連線清單的篩選條件。原樣送進 SQL，本端不解讀。</summary>
public sealed record ConnectionFilter
{
    public string Search { get; init; } = "";
    public bool OnlyAbnormal { get; init; }
}

/// <summary>新增連線。密碼以獨立的密文緩衝參數傳遞，不放進本型別。</summary>
public sealed record CreateConnectionRequest
{
    public required string Name { get; init; }
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public string? GroupName { get; init; }
}

public sealed record UpdateConnectionRequest
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public string? GroupName { get; init; }
}

public sealed record RenameGroupRequest
{
    public required string OldName { get; init; }
    public required string NewName { get; init; }
}

/// <summary>連線管理對外的唯一入口。</summary>
public interface IConnectionManagementFacade
{
    /// <summary>任何寫入完成後觸發，供已開啟的畫面重新載入。</summary>
    public event Action? Changed;

    public ValueTask<Result<IReadOnlyList<ConnectionSnapshot>>> LoadConnectionListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken);

    /// <summary>目前正被使用的群組名稱。群組不是獨立實體，清單來自連線資料。</summary>
    public ValueTask<Result<IReadOnlyList<string>>> LoadGroupNameListAsync(CancellationToken cancellationToken);

    public ValueTask<Result> CreateConnectionAsync(
        CreateConnectionRequest request, SecretBuffer password, CancellationToken cancellationToken);

    /// <summary>密碼為 null 代表不變更。</summary>
    public ValueTask<Result> UpdateConnectionAsync(
        UpdateConnectionRequest request, SecretBuffer? password, CancellationToken cancellationToken);

    public ValueTask<Result> DeleteConnectionAsync(int id, CancellationToken cancellationToken);

    /// <summary>解出單筆密碼明文供畫面顯示，按下顯示鈕的當下才呼叫。</summary>
    public ValueTask<Result<string>> RevealPasswordAsync(int id, CancellationToken cancellationToken);

    /// <summary>RFB 協定層測試，不開任何視窗。回傳值為測試後的最新快照，逾時與密碼錯誤都是正常結果。</summary>
    public ValueTask<Result<ConnectionSnapshot>> TestConnectionAsync(int id, CancellationToken cancellationToken);

    /// <summary>把該群組下的所有連線改標為新名稱。</summary>
    public ValueTask<Result> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken);

    /// <summary>把該群組下的所有連線改為未分組，不刪除連線本身。</summary>
    public ValueTask<Result> UngroupAsync(string name, CancellationToken cancellationToken);
}

/// <summary>可選取的部署目標。</summary>
public sealed record DeployTargetCandidate
{
    public required int ConnectionId { get; init; }
    public required string Name { get; init; }
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public string? GroupName { get; init; }
}

/// <summary>對外提供部署目標清單與單筆密碼解出。實作在連線管理。</summary>
public interface IDeployTargetProvider
{
    public ValueTask<Result<IReadOnlyList<DeployTargetCandidate>>> LoadTargetListAsync(CancellationToken cancellationToken);

    /// <summary>用畢由呼叫端釋放歸零，不快取明文。</summary>
    public ValueTask<Result<SecretBuffer>> ResolveSecretAsync(int connectionId, CancellationToken cancellationToken);
}
