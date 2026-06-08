using System;
using System.Windows.Input;
using Helix_Studio.Infrastructure.Licensing;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 啟用視窗的 UI 狀態與授權流程。
/// </summary>
public sealed class ActivationWindowViewModel : ViewModelBase
{
    private readonly IApplicationLicenseService _applicationLicenseService;
    private readonly LicenseFingerprintSnapshot _fingerprintSnapshot;
    private readonly RelayCommand _activateCommand;
    private readonly RelayCommand _cancelCommand;
    private string _licenseKey = string.Empty;
    private bool _agreeToTerms;
    private int _languageIndex;
    private string _activationTitleText = string.Empty;
    private string _activationDescriptionText = string.Empty;
    private string _licenseKeyLabelText = string.Empty;
    private string _licenseFormatPrefixText = string.Empty;
    private string _licenseFormatHintText = string.Empty;
    private string _deviceBindingPrefixText = string.Empty;
    private string _fingerprintPrefixText = string.Empty;
    private string _rebindingHintText = string.Empty;
    private string _copyDeviceInfoText = string.Empty;
    private string _agreementPrefixText = string.Empty;
    private string _enterpriseAgreementText = string.Empty;
    private string _agreementAndText = string.Empty;
    private string _dataProcessingTermsText = string.Empty;
    private string _needHelpText = string.Empty;
    private string _contactAdministratorText = string.Empty;
    private string _cancelButtonText = string.Empty;
    private string _activateButtonText = string.Empty;
    private string _statusModeText = string.Empty;
    private string _statusSecurityText = string.Empty;
    private string _statusDeviceText = string.Empty;
    private string _statusBuildText = string.Empty;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 建立啟用視窗 ViewModel。
    /// </summary>
    public ActivationWindowViewModel(IApplicationLicenseService applicationLicenseService)
    {
        _applicationLicenseService = applicationLicenseService;
        _fingerprintSnapshot = applicationLicenseService.QueryFingerprintSnapshot();
        _licenseKey = applicationLicenseService.QueryStoredLicenseKey();
        BoundDeviceName = _fingerprintSnapshot.DeviceName;
        FingerprintText = _fingerprintSnapshot.DisplayText;
        _activateCommand = new RelayCommand(_ => ApplyActivate(), _ => QueryCanActivate());
        _cancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, false));
        ApplyLanguageTexts();
    }

    /// <summary>
    /// 關閉視窗要求；`true` 代表已成功啟用。
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>
    /// 一般金鑰輸入值。
    /// </summary>
    public string LicenseKey
    {
        get => _licenseKey;
        set
        {
            if (!SetProperty(ref _licenseKey, value ?? string.Empty))
            {
                return;
            }

            ErrorMessage = string.Empty;
            _activateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 是否同意條款。
    /// </summary>
    public bool AgreeToTerms
    {
        get => _agreeToTerms;
        set
        {
            if (!SetProperty(ref _agreeToTerms, value))
            {
                return;
            }

            _activateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 語系索引；`0` 為繁體中文，`1` 為英文。
    /// </summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            if (!SetProperty(ref _languageIndex, value))
            {
                return;
            }

            ApplyLanguageTexts();
        }
    }

    /// <summary>
    /// 是否顯示中文輔助標籤。
    /// </summary>
    public bool ShowLicenseKeyLabelZh => LanguageIndex == 1;

    /// <summary>
    /// 裝置名稱。
    /// </summary>
    public string BoundDeviceName { get; }

    /// <summary>
    /// 指紋摘要。
    /// </summary>
    public string FingerprintText { get; }

    /// <summary>
    /// 是否有錯誤訊息。
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// 驗證失敗訊息。
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>
    /// 畫面主標題。
    /// </summary>
    public string ActivationTitleText
    {
        get => _activationTitleText;
        private set => SetProperty(ref _activationTitleText, value);
    }

    /// <summary>
    /// 畫面說明。
    /// </summary>
    public string ActivationDescriptionText
    {
        get => _activationDescriptionText;
        private set => SetProperty(ref _activationDescriptionText, value);
    }

    /// <summary>
    /// 金鑰標題。
    /// </summary>
    public string LicenseKeyLabelText
    {
        get => _licenseKeyLabelText;
        private set => SetProperty(ref _licenseKeyLabelText, value);
    }

    /// <summary>
    /// 金鑰格式前綴文案。
    /// </summary>
    public string LicenseFormatPrefixText
    {
        get => _licenseFormatPrefixText;
        private set => SetProperty(ref _licenseFormatPrefixText, value);
    }

    /// <summary>
    /// 金鑰格式說明。
    /// </summary>
    public string LicenseFormatHintText
    {
        get => _licenseFormatHintText;
        private set => SetProperty(ref _licenseFormatHintText, value);
    }

    /// <summary>
    /// 裝置綁定提示。
    /// </summary>
    public string DeviceBindingPrefixText
    {
        get => _deviceBindingPrefixText;
        private set => SetProperty(ref _deviceBindingPrefixText, value);
    }

    /// <summary>
    /// 指紋前綴文案。
    /// </summary>
    public string FingerprintPrefixText
    {
        get => _fingerprintPrefixText;
        private set => SetProperty(ref _fingerprintPrefixText, value);
    }

    /// <summary>
    /// 重綁定說明。
    /// </summary>
    public string RebindingHintText
    {
        get => _rebindingHintText;
        private set => SetProperty(ref _rebindingHintText, value);
    }

    /// <summary>
    /// 複製裝置資訊文案。
    /// </summary>
    public string CopyDeviceInfoText
    {
        get => _copyDeviceInfoText;
        private set => SetProperty(ref _copyDeviceInfoText, value);
    }

    /// <summary>
    /// 條款前綴文案。
    /// </summary>
    public string AgreementPrefixText
    {
        get => _agreementPrefixText;
        private set => SetProperty(ref _agreementPrefixText, value);
    }

    /// <summary>
    /// 企業協議文案。
    /// </summary>
    public string EnterpriseAgreementText
    {
        get => _enterpriseAgreementText;
        private set => SetProperty(ref _enterpriseAgreementText, value);
    }

    /// <summary>
    /// 條款連接詞文案。
    /// </summary>
    public string AgreementAndText
    {
        get => _agreementAndText;
        private set => SetProperty(ref _agreementAndText, value);
    }

    /// <summary>
    /// 資料處理條款文案。
    /// </summary>
    public string DataProcessingTermsText
    {
        get => _dataProcessingTermsText;
        private set => SetProperty(ref _dataProcessingTermsText, value);
    }

    /// <summary>
    /// 協助文案。
    /// </summary>
    public string NeedHelpText
    {
        get => _needHelpText;
        private set => SetProperty(ref _needHelpText, value);
    }

    /// <summary>
    /// 聯絡管理員文案。
    /// </summary>
    public string ContactAdministratorText
    {
        get => _contactAdministratorText;
        private set => SetProperty(ref _contactAdministratorText, value);
    }

    /// <summary>
    /// 取消按鈕文案。
    /// </summary>
    public string CancelButtonText
    {
        get => _cancelButtonText;
        private set => SetProperty(ref _cancelButtonText, value);
    }

    /// <summary>
    /// 啟用按鈕文案。
    /// </summary>
    public string ActivateButtonText
    {
        get => _activateButtonText;
        private set => SetProperty(ref _activateButtonText, value);
    }

    /// <summary>
    /// 狀態列模式文案。
    /// </summary>
    public string StatusModeText
    {
        get => _statusModeText;
        private set => SetProperty(ref _statusModeText, value);
    }

    /// <summary>
    /// 狀態列安全文案。
    /// </summary>
    public string StatusSecurityText
    {
        get => _statusSecurityText;
        private set => SetProperty(ref _statusSecurityText, value);
    }

    /// <summary>
    /// 狀態列裝置文案。
    /// </summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetProperty(ref _statusDeviceText, value);
    }

    /// <summary>
    /// 狀態列建置文案。
    /// </summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetProperty(ref _statusBuildText, value);
    }

    /// <summary>
    /// 啟用命令。
    /// </summary>
    public ICommand ActivateCommand => _activateCommand;

    /// <summary>
    /// 取消命令。
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// 複製裝置資訊到剪貼簿用文字。
    /// </summary>
    public string QueryClipboardText()
        => _fingerprintSnapshot.ClipboardText;

    private bool QueryCanActivate()
        => AgreeToTerms && !string.IsNullOrWhiteSpace(LicenseKey);

    private void ApplyActivate()
    {
        var verification = _applicationLicenseService.VerifyLicense(LicenseKey);
        if (!verification.IsValid)
        {
            ErrorMessage = verification.ErrorMessage;
            return;
        }

        _applicationLicenseService.SaveLicenseKey(LicenseKey);
        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(this, true);
    }

    private void ApplyLanguageTexts()
    {
        if (LanguageIndex == 1)
        {
            ActivationTitleText = "Activate Helix Studio";
            ActivationDescriptionText = "Enter the general license key to unlock this workstation. Module licenses remain independent per workspace and module.";
            LicenseKeyLabelText = "License Key";
            LicenseFormatPrefixText = "Format";
            LicenseFormatHintText = "GUID:HASH";
            DeviceBindingPrefixText = "Bound Device";
            FingerprintPrefixText = "Fingerprint";
            RebindingHintText = $"score {_fingerprintSnapshot.TotalScore}/60";
            CopyDeviceInfoText = "Copy";
            AgreementPrefixText = "I agree to the ";
            EnterpriseAgreementText = "Enterprise Agreement";
            AgreementAndText = " and ";
            DataProcessingTermsText = "Data Processing Terms";
            NeedHelpText = "Need help? ";
            ContactAdministratorText = "Contact administrator";
            CancelButtonText = "Cancel";
            ActivateButtonText = "Activate";
            StatusModeText = "Activation Required";
            StatusSecurityText = "General license is stored in App Home Root auth.json";
            StatusDeviceText = $"Device: {BoundDeviceName}";
            StatusBuildText = "Build: single delivery environment";
        }
        else
        {
            ActivationTitleText = "啟用 Helix Studio";
            ActivationDescriptionText = "請輸入一般金鑰以啟用這台工作站。模組金鑰仍維持各工作區、各模組獨立管理。";
            LicenseKeyLabelText = "一般金鑰";
            LicenseFormatPrefixText = "格式";
            LicenseFormatHintText = "GUID:HASH";
            DeviceBindingPrefixText = "綁定裝置";
            FingerprintPrefixText = "裝置指紋";
            RebindingHintText = $"目前指紋分數 {_fingerprintSnapshot.TotalScore}/60";
            CopyDeviceInfoText = "複製";
            AgreementPrefixText = "我已閱讀並同意 ";
            EnterpriseAgreementText = "企業授權條款";
            AgreementAndText = " 與 ";
            DataProcessingTermsText = "資料處理條款";
            NeedHelpText = "需要協助？";
            ContactAdministratorText = "聯絡管理員";
            CancelButtonText = "取消";
            ActivateButtonText = "啟用";
            StatusModeText = "需要一般金鑰";
            StatusSecurityText = "一般金鑰會獨立寫入 App Home Root 的 auth.json";
            StatusDeviceText = $"裝置：{BoundDeviceName}";
            StatusBuildText = "交付模式：單一環境";
        }

        OnPropertyChanged(nameof(ShowLicenseKeyLabelZh));
    }
}
