using System.IO;
using System.Text;
using System.Text.Json;
using Helix_Studio.Infrastructure.Utilities;
using Helix_Studio.Model.License;

namespace Helix_Studio.Infrastructure.Persistence;

/// <summary>產品授權啟用狀態之 JSON 持久化。</summary>
public sealed class LicenseStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _statePath;

    /// <summary>建立儲存體並使用預設應用程式資料路徑下之檔名。</summary>
    public LicenseStateStore()
    {
        _statePath = Path.Combine(AppPathUtility.QueryAppDataDirectory(), "license-state.json");
    }

    /// <summary>嘗試載入授權狀態；不存在或失敗時回傳 null。</summary>
    public LicenseActivationState? TryLoad()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return null;
            }

            var json = File.ReadAllText(_statePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<LicenseActivationState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>將授權狀態寫入 JSON 檔。</summary>
    public void Save(LicenseActivationState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_statePath, json, Encoding.UTF8);
    }
}
