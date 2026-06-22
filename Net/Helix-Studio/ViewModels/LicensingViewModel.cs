using Helix_Studio.Mvvm;
using Helix_Studio.Services;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 授權驗證視窗的 ViewModel（FL-13 N-003 / N-004）。
/// 使用者輸入 L1 平台金鑰，在背景執行緒驗證後回報結果。
/// </summary>
public sealed class LicensingViewModel : ObservableObject
{
    private readonly LicenseService _license;
    private string _keyInput     = string.Empty;
    private string _errorMessage = string.Empty;
    private bool   _isVerifying;
    private bool   _isRebind;

    /// <summary>驗證通過後觸發，由視窗訂閱後關閉並讓 App 繼續建立主視窗。</summary>
    public event Action? LicenseActivated;

    public LicensingViewModel(LicenseService license, bool isRebind = false)
    {
        _license  = license;
        _isRebind = isRebind;
        VerifyCommand = new RelayCommand(_ => { _ = VerifyAsync(); }, _ => !_isVerifying);
    }

    // ── 屬性 ──────────────────────────────────────────────

    public string KeyInput
    {
        get => _keyInput;
        set
        {
            SetProperty(ref _keyInput, value);
            ErrorMessage = string.Empty;
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            SetProperty(ref _errorMessage, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public bool IsVerifying
    {
        get => _isVerifying;
        private set
        {
            SetProperty(ref _isVerifying, value);
            OnPropertyChanged(nameof(IsInputEnabled));
            OnPropertyChanged(nameof(VerifyButtonText));
        }
    }

    public bool IsInputEnabled => !_isVerifying;

    /// <summary>Rebind 場景（硬體大幅變更），控制視窗說明文字。</summary>
    public bool IsRebind
    {
        get => _isRebind;
        set => SetProperty(ref _isRebind, value);
    }

    public string VerifyButtonText
        => IsVerifying
            ? LocalizationService.Instance.GetString("L.License.Verifying")
            : LocalizationService.Instance.GetString("L.License.Verify");

    public RelayCommand VerifyCommand { get; }

    // ── 驗證邏輯 ──────────────────────────────────────────

    private async Task VerifyAsync()
    {
        var key = _keyInput.Trim();

        if (!LicenseService.IsValidFormat(key))
        {
            ErrorMessage = LocalizationService.Instance.GetString("L.License.Err.Format");
            return;
        }

        IsVerifying  = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _license.VerifyPlatformKeyAsync(key);
            switch (result)
            {
                case VerifyResult.Pass:
                    LicenseActivated?.Invoke();
                    break;

                case VerifyResult.Rebind:
                    IsRebind     = true;
                    ErrorMessage = LocalizationService.Instance.GetString("L.License.RebindDesc");
                    break;

                default:
                    ErrorMessage = LocalizationService.Instance.GetString("L.License.Err.Invalid");
                    break;
            }
        }
        catch (System.Management.ManagementException)
        {
            ErrorMessage = LocalizationService.Instance.GetString("L.License.Err.Wmi");
        }
        catch
        {
            ErrorMessage = LocalizationService.Instance.GetString("L.License.Err.Invalid");
        }
        finally
        {
            IsVerifying = false;
        }
    }
}
