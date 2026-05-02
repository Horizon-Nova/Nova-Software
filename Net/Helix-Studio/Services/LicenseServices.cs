using Helix_Studio.Domain.License;
using Helix_Studio.Infrastructure.Persistence;
using Helix_Studio.Model.License;

namespace Helix_Studio.Services;

/// <summary>主程式授權金鑰正規化、啟用狀態持久化與裝置／到期驗證的服務。</summary>
public sealed class LicenseServices
{
    private readonly LicenseStateStore _store;

    /// <summary>以授權狀態儲存機制建立服務。</summary>
    /// <param name="store">授權狀態的讀寫儲存。</param>
    public LicenseServices(LicenseStateStore store)
    {
        _store = store;
    }

    /// <summary>驗證並正規化金鑰後寫入啟用狀態。</summary>
    public void Activate(string rawLicenseKey)
    {
        var normalized = LicenseDomain.NormalizeLicenseKey(rawLicenseKey);
        LicenseDomain.EnsureLicenseKeyValid(normalized, nameof(rawLicenseKey));
        var state = LicenseDomain.BuildActivationState(normalized);
        _store.Save(state);
    }

    /// <summary>讀取目前儲存的授權啟用狀態，若無則為 <c>null</c>。</summary>
    public LicenseActivationState? QueryCurrent() => _store.TryLoad();

    /// <summary>判斷目前是否視為已啟用（裝置名稱相符且未過期）。</summary>
    public bool IsActivated()
    {
        var state = QueryCurrent();
        if (state is null)
        {
            return false;
        }

        if (!string.Equals(state.DeviceName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return state.ExpiresAtUtc > DateTime.UtcNow;
    }

    /// <summary>查詢本機裝置指紋的 SHA-256 十六進位字串。</summary>
    public string QueryDeviceFingerprintSha256() => LicenseDomain.QueryDeviceFingerprintSha256();
}

