using Helix_Studio.Model.Configuration;
using Helix_Studio.Infrastructure.Utilities;

namespace Helix_Studio.Services;

/// <summary>應用程式層設定（<see cref="AppConfig"/>）的讀取與持久化，並於變更時引發通知。</summary>
public sealed class ConfigurationServices
{
    private readonly IniConfigUtility _configUtility;

    /// <summary>以 INI 設定工具建立服務。</summary>
    /// <param name="configUtility">應用程式設定讀寫工具。</param>
    public ConfigurationServices(IniConfigUtility configUtility)
    {
        _configUtility = configUtility;
    }

    /// <summary>當語系、主題或整份設定儲存後觸發。</summary>
    public event EventHandler<AppConfig>? Changed;

    /// <summary>查詢目前磁碟上的應用程式層設定快照。</summary>
    public AppConfig QueryCurrent() => _configUtility.QueryCurrent();

    /// <summary>儲存介面文化名稱並通知訂閱者。</summary>
    public void SaveUiCulture(string cultureName)
    {
        var config = _configUtility.SaveUiCulture(cultureName);
        Changed?.Invoke(this, config);
    }

    /// <summary>儲存介面主題模式並通知訂閱者。</summary>
    public void SaveTheme(AppThemeMode themeMode)
    {
        var config = _configUtility.SaveTheme(themeMode);
        Changed?.Invoke(this, config);
    }

    /// <summary>寫入完整應用程式層設定並通知訂閱者。</summary>
    public void Save(AppConfig config)
    {
        var savedConfig = _configUtility.Save(config);
        Changed?.Invoke(this, savedConfig);
    }
}

