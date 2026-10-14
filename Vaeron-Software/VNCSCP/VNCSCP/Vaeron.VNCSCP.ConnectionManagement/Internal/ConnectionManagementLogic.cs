using System.Text;
using Vaeron.Kernel;
using Vaeron.Kernel.Data.Models;
using Vaeron.Kernel.Guarding;
using Vaeron.Kernel.Secrets;
using Vaeron.Kernel.Windows;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.ConnectionManagement.Internal;

/// <summary>連線的業務規則集中處。取得事實、決定結果的形狀，不碰畫面也不記事件。</summary>
internal sealed class ConnectionManagementLogic(ConnectionManagementStore store, RfbAuthClient rfbClient)
{
    internal Task<IReadOnlyList<ConnectionRow>> LoadConnectionListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken)
        => store.QueryConnectionListAsync(filter, cancellationToken);

    internal Task<IReadOnlyList<string>> LoadGroupNameListAsync(CancellationToken cancellationToken)
        => store.QueryGroupNameListAsync(cancellationToken);

    internal async Task<Result<ConnectionRow>> LoadConnectionAsync(int id, CancellationToken cancellationToken)
        => Result<ConnectionRow>.FromFirst(
            await store.QueryConnectionByIdAsync(id, cancellationToken),
            ResultKind.NotFound,
            "找不到該連線");

    internal async Task<Result<ConnectionEntity>> CreateConnectionAsync(
        CreateConnectionRequest request, SecretBuffer password, CancellationToken cancellationToken)
    {
        var prepared = Result
            .When(password.Length > 0, ResultKind.ValidationFailed, "VNC 密碼為必填")
            .Bind(() => ProtectPassword(password))
            .Map(cipher => new ConnectionEntity
            {
                Name = request.Name,
                Ip = request.Ip,
                Port = request.Port,
                PasswordCipher = cipher,
                Status = (int)ConnectionStatus.Unknown,
                LastTestMessage = "",
                GroupName = request.GroupName
            });

        foreach (var entity in prepared.Values)
        {
            await store.InsertConnectionAsync(entity, cancellationToken);
        }

        return prepared;
    }

    /// <summary>密碼為 null 代表不變更。</summary>
    internal async Task<Result> UpdateConnectionAsync(
        UpdateConnectionRequest request, SecretBuffer? password, CancellationToken cancellationToken)
    {
        var cipher = ResolveUpdateCipher(password);
        var affected = 0;

        foreach (var bytes in cipher.Values)
        {
            affected = await store.UpdateConnectionAsync(request, bytes, cancellationToken);
        }

        return cipher.Bind(_ => Result.When(affected > 0, ResultKind.NotFound, "找不到該連線"));
    }

    internal async Task<Result> DeleteConnectionAsync(int id, CancellationToken cancellationToken)
        => Result.When(
            await store.DeleteConnectionAsync(id, cancellationToken) > 0,
            ResultKind.NotFound,
            "找不到該連線");

    internal async Task<Result> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken)
        => Result.When(
            await store.RenameGroupAsync(request, cancellationToken) > 0,
            ResultKind.NotFound,
            "找不到該群組");

    internal async Task<Result> UngroupAsync(string name, CancellationToken cancellationToken)
        => Result.When(
            await store.UngroupAsync(name, cancellationToken) > 0,
            ResultKind.NotFound,
            "找不到該群組");

    /// <summary>解出單筆密碼明文。未設密碼時為空字串。</summary>
    internal async Task<Result<string>> RevealPasswordAsync(int id, CancellationToken cancellationToken)
        => (await LoadConnectionAsync(id, cancellationToken)).Bind(DecryptPassword);

    /// <summary>測試後把結果寫回連線，回傳寫回後的最新資料。</summary>
    internal async Task<Result<TestedConnection>> TestConnectionAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await LoadConnectionAsync(id, cancellationToken);
        var tested = new List<TestedConnection>();

        foreach (var row in loaded.Values)
        {
            tested.AddRange(await RunTestAsync(row, cancellationToken));
        }

        return loaded.Bind(_ => Result<TestedConnection>.FromFirst(
            tested, ResultKind.InfrastructureError, "密碼無法解開，請重新設定此連線的密碼"));
    }

    private async Task<IReadOnlyList<TestedConnection>> RunTestAsync(ConnectionRow row, CancellationToken cancellationToken)
    {
        var unprotected = DpapiProtector.Unprotect(row.PasswordCipher);
        var results = new List<TestedConnection>();

        foreach (var plainBytes in unprotected.Values)
        {
            using var password = new SecretBuffer(plainBytes);
            var outcome = await ProbeAsync(row, password, cancellationToken);
            await store.UpdateStatusAsync(row.Id, outcome.Status, outcome.Message, cancellationToken);

            var refreshed = await store.QueryConnectionByIdAsync(row.Id, cancellationToken);
            results.AddRange(refreshed.Select(fresh => new TestedConnection(fresh, outcome)));
        }

        return results;
    }

    /// <summary>socket 層的失敗是這支工具的正常輸出，不是例外流程，由守衛收成「無法連線」。</summary>
    private async Task<RfbTestOutcome> ProbeAsync(ConnectionRow row, SecretBuffer password, CancellationToken cancellationToken)
    {
        var outcome = new RfbTestOutcome(ConnectionStatus.Unreachable, "連線逾時", "");

        await Guard.RunAsync(
            async () => outcome = await rfbClient.TestAsync(new RfbTestTarget(row.Ip, row.Port), password, cancellationToken),
            error => outcome = new RfbTestOutcome(ConnectionStatus.Unreachable, error.Message, error.ToString()));

        return outcome;
    }

    internal async Task<Result<SecretBuffer>> ResolveSecretAsync(int connectionId, CancellationToken cancellationToken)
        => (await LoadConnectionAsync(connectionId, cancellationToken)).Bind(OpenSecret);

    private static Result<SecretBuffer> OpenSecret(ConnectionRow row)
        => Result<byte[]>
            .FromFirst(
                row.PasswordCipher.Take(1).Select(_ => row.PasswordCipher),
                ResultKind.ValidationFailed,
                "該連線未設定密碼")
            .Bind(_ => DpapiProtector.Unprotect(row.PasswordCipher))
            .Map(plainBytes => new SecretBuffer(plainBytes));

    private static Result<string> DecryptPassword(ConnectionRow row)
        => row.PasswordCipher
            .Take(1)
            .Select(_ => DpapiProtector.Unprotect(row.PasswordCipher).Bind(ToPlainText))
            .DefaultIfEmpty(Result<string>.Ok(""))
            .First();

    private static Result<string> ToPlainText(byte[] plainBytes)
    {
        using var secret = new SecretBuffer(plainBytes);
        return Result<string>.Ok(secret.Use(bytes => Encoding.UTF8.GetString(bytes)));
    }

    private static Result<byte[]> ProtectPassword(SecretBuffer password)
        => password.Use(bytes => DpapiProtector.Protect(bytes.ToArray()));

    /// <summary>密碼為 null 時給空密文，代表這次更新不動密碼。</summary>
    private static Result<byte[]> ResolveUpdateCipher(SecretBuffer? password)
        => new[] { password }
            .OfType<SecretBuffer>()
            .Select(ProtectPassword)
            .DefaultIfEmpty(Result<byte[]>.Ok([]))
            .First();
}

/// <summary>測試前的連線資料與這次測試的結果。</summary>
internal sealed record TestedConnection(ConnectionRow Row, RfbTestOutcome Outcome);
