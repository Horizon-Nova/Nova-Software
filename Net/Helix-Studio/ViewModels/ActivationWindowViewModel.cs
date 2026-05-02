using Helix_Studio.Model.Activation;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>授權啟用視窗的 UI 狀態、雙語字串、命令與啟用流程。</summary>
public sealed class ActivationWindowViewModel : ViewModelBase
{
    private readonly ActivationServices _service;

    private int _languageIndex;
    private string _licenseKey = string.Empty;
    private bool _agreeToTerms;

    private string _boundDeviceName = "—";
    private string _shortFingerprint = "—";
    private string _statusDeviceText = "—";
    private string _statusBuildText = "—";

    /// <summary>使用 <see cref="AppServices.Activation"/> 的預設建構子。</summary>
    public ActivationWindowViewModel()
        : this(AppServices.Activation) { }

    /// <summary>以注入的啟用服務建構，並註冊各命令與初始語系。</summary>
    public ActivationWindowViewModel(ActivationServices service)
    {
        _service = service;

        LoadedCommand = new RelayCommand(LoadRuntimeInfo);
        ActivateCommand = new RelayCommand(Activate);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
        OpenEnterpriseAgreementCommand = new RelayCommand(() => OpenLegalFileByName(QueryTermsFileName()));
        OpenDataProcessingTermsCommand = new RelayCommand(() => OpenLegalFileByName(QueryTermsFileName()));
        ContactAdministratorCommand = new RelayCommand(ContactAdministrator);

        ApplyLanguage();
    }

    /// <summary>要求關閉啟用視窗（例如使用者取消）。</summary>
    public event Action? CancelRequested;

    /// <summary>啟用成功時引發，供關閉視窗或進入下一段流程。</summary>
    public event Action? ActivationSucceeded;

    /// <summary>要顯示的短暫訊息（標題、內文）。</summary>
    public event Action<string, string>? ToastRequested;

    /// <summary>載入執行時期裝置與指紋資訊。</summary>
    public RelayCommand LoadedCommand { get; }

    /// <summary>送出授權金鑰進行本機啟用。</summary>
    public RelayCommand ActivateCommand { get; }

    /// <summary>取消啟用流程。</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>開啟企業授權條款（依目前語系選擇法律檔）。</summary>
    public RelayCommand OpenEnterpriseAgreementCommand { get; }

    /// <summary>開啟資料處理條款（依目前語系選擇法律檔）。</summary>
    public RelayCommand OpenDataProcessingTermsCommand { get; }

    /// <summary>顯示聯絡系統管理員的提示訊息。</summary>
    public RelayCommand ContactAdministratorCommand { get; }

    /// <summary>介面語言選項索引；變更時重算雙語字串。</summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            if (!SetProperty(ref _languageIndex, value))
                return;

            ApplyLanguage();
        }
    }

    /// <summary>是否顯示英文介面（相對於主要中文選項）。</summary>
    public bool IsEnglish => LanguageIndex != 1;

    /// <summary>使用者輸入的授權金鑰。</summary>
    public string LicenseKey
    {
        get => _licenseKey;
        set => SetProperty(ref _licenseKey, value ?? string.Empty);
    }

    /// <summary>是否已勾選同意授權與條款。</summary>
    public bool AgreeToTerms
    {
        get => _agreeToTerms;
        set => SetProperty(ref _agreeToTerms, value);
    }

    /// <summary>授權將綁定的裝置名稱顯示文字。</summary>
    public string BoundDeviceName
    {
        get => _boundDeviceName;
        private set => SetProperty(ref _boundDeviceName, value);
    }

    /// <summary>裝置指紋的縮寫顯示（中間以省略顯示）。</summary>
    public string ShortFingerprint
    {
        get => _shortFingerprint;
        private set => SetProperty(ref _shortFingerprint, value);
    }

    /// <summary>狀態列顯示的裝置敘述。</summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetProperty(ref _statusDeviceText, value);
    }

    /// <summary>狀態列顯示的組建或版本敘述。</summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetProperty(ref _statusBuildText, value);
    }

    /// <summary>視窗主標題（雙語）。</summary>
    public string ActivationTitleText { get; private set; } = string.Empty;

    /// <summary>啟用說明內文（雙語）。</summary>
    public string ActivationDescriptionText { get; private set; } = string.Empty;

    /// <summary>授權金鑰欄位標籤（雙語）。</summary>
    public string LicenseKeyLabelText { get; private set; } = string.Empty;

    /// <summary>是否以中文顯示金鑰標籤樣式（與英文版面搭配）。</summary>
    public bool ShowLicenseKeyLabelZh => IsEnglish;

    /// <summary>金鑰格式前綴說明（雙語）。</summary>
    public string LicenseFormatPrefixText { get; private set; } = string.Empty;

    /// <summary>金鑰長度或格式提示（雙語）。</summary>
    public string LicenseFormatHintText { get; private set; } = string.Empty;

    /// <summary>裝置綁定說明前綴（雙語）。</summary>
    public string DeviceBindingPrefixText { get; private set; } = string.Empty;

    /// <summary>指紋欄位標籤前綴（雙語）。</summary>
    public string FingerprintPrefixText { get; private set; } = string.Empty;

    /// <summary>重新綁定提示（雙語）。</summary>
    public string RebindingHintText { get; private set; } = string.Empty;

    /// <summary>複製裝置資訊按鈕文字（雙語）。</summary>
    public string CopyDeviceInfoText { get; private set; } = string.Empty;

    /// <summary>同意條款敘述前綴（雙語）。</summary>
    public string AgreementPrefixText { get; private set; } = string.Empty;

    /// <summary>企業授權條款連結文字（雙語）。</summary>
    public string EnterpriseAgreementText { get; private set; } = string.Empty;

    /// <summary>條款連結之間的「與」字（雙語）。</summary>
    public string AgreementAndText { get; private set; } = string.Empty;

    /// <summary>資料處理條款連結文字（雙語）。</summary>
    public string DataProcessingTermsText { get; private set; } = string.Empty;

    /// <summary>「需要協助」提示前綴（雙語）。</summary>
    public string NeedHelpText { get; private set; } = string.Empty;

    /// <summary>聯絡管理員連結文字（雙語）。</summary>
    public string ContactAdministratorText { get; private set; } = string.Empty;

    /// <summary>取消按鈕文字（雙語）。</summary>
    public string CancelButtonText { get; private set; } = string.Empty;

    /// <summary>啟用按鈕文字（雙語）。</summary>
    public string ActivateButtonText { get; private set; } = string.Empty;

    /// <summary>狀態列模式說明（雙語）。</summary>
    public string StatusModeText { get; private set; } = string.Empty;

    /// <summary>狀態列安全性說明（雙語）。</summary>
    public string StatusSecurityText { get; private set; } = string.Empty;

    /// <summary>完整指紋顯示字串（含 SHA-256 前綴）。</summary>
    public string FingerprintText => $" SHA-256: {ShortFingerprint}";

    private void LoadRuntimeInfo()
    {
        var runtimeInfo = _service.QueryRuntimeInfo();
        BoundDeviceName = runtimeInfo.DeviceName;
        ShortFingerprint = runtimeInfo.ShortFingerprint;
        StatusDeviceText = $"Device: {runtimeInfo.DeviceName}";
        StatusBuildText = runtimeInfo.BuildText;

        RaisePropertyChanged(nameof(FingerprintText));
    }

    private void Activate()
    {
        if (!AgreeToTerms)
        {
            ToastRequested?.Invoke(
                IsEnglish ? "Activation failed" : "啟用失敗",
                IsEnglish ? "Please agree to the license terms before activation." : "請先同意授權條款後再進行啟用。");
            return;
        }

        try
        {
            _service.Activate(LicenseKey);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ToastRequested?.Invoke(
                IsEnglish ? "Activation failed" : "啟用失敗",
                ex.Message);
            return;
        }

        ActivationSucceeded?.Invoke();
    }

    private void ContactAdministrator()
    {
        ToastRequested?.Invoke(
            IsEnglish ? "Support" : "支援資訊",
            IsEnglish
                ? "Please contact your system administrator for license support."
                : "請聯絡系統管理員取得授權協助。");
    }

    private void OpenLegalFileByName(string fileName)
    {
        if (_service.OpenLegalFile(fileName)) return;

        ToastRequested?.Invoke(
            IsEnglish ? "Open file failed" : "開啟失敗",
            IsEnglish ? "Legal document not found." : "找不到條款文件。");
    }

    private string QueryTermsFileName()
        => IsEnglish ? "Helix-Studio-Terms.en.txt" : "Helix-Studio-Terms.zh-Hant.txt";

    private void ApplyLanguage()
    {
        ActivationTitleText = IsEnglish ? "Activate your license" : "啟用授權";
        ActivationDescriptionText = IsEnglish
            ? "Enter the license key provided by your organization to begin. Your activation will be bound to this device for security and compliance."
            : "請輸入由組織提供的授權金鑰。啟用後會綁定本機裝置，以符合安全與合規需求。";
        LicenseKeyLabelText = IsEnglish ? "License key" : "授權金鑰";
        LicenseFormatPrefixText = IsEnglish
            ? "Format: HN-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX"
            : "格式：HN-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX";
        LicenseFormatHintText = IsEnglish
            ? "25 characters including prefix"
            : "含前綴共 25 個字元";
        DeviceBindingPrefixText = IsEnglish ? "This license will be bound to" : "此授權將綁定至";
        FingerprintPrefixText = IsEnglish ? "Fingerprint" : "裝置指紋";
        RebindingHintText = IsEnglish
            ? "Rebinding requires organization admin approval"
            : "重新綁定需由組織管理員核准";
        CopyDeviceInfoText = IsEnglish ? "Copy" : "複製";
        AgreementPrefixText = IsEnglish ? "I have read and agree to the " : "我已閱讀並同意";
        EnterpriseAgreementText = IsEnglish ? "Enterprise License Agreement" : "企業授權條款";
        AgreementAndText = IsEnglish ? " and " : "與";
        DataProcessingTermsText = IsEnglish ? "Data Processing Terms" : "資料處理條款";
        NeedHelpText = IsEnglish ? "Need help? " : "需要協助？";
        ContactAdministratorText = IsEnglish ? "Contact your administrator" : "聯絡系統管理員";
        CancelButtonText = IsEnglish ? "Cancel" : "取消";
        ActivateButtonText = IsEnglish ? "Activate" : "啟用";
        StatusModeText = IsEnglish
            ? "Local license mode · no server connection required"
            : "本機授權模式 · 不需連線伺服器";
        StatusSecurityText = IsEnglish
            ? "Offline activation · device-bound license"
            : "離線啟用 · 授權綁定裝置";

        RaisePropertyChanged(nameof(IsEnglish));
        RaisePropertyChanged(nameof(ShowLicenseKeyLabelZh));
        RaisePropertyChanged(nameof(ActivationTitleText));
        RaisePropertyChanged(nameof(ActivationDescriptionText));
        RaisePropertyChanged(nameof(LicenseKeyLabelText));
        RaisePropertyChanged(nameof(LicenseFormatPrefixText));
        RaisePropertyChanged(nameof(LicenseFormatHintText));
        RaisePropertyChanged(nameof(DeviceBindingPrefixText));
        RaisePropertyChanged(nameof(FingerprintPrefixText));
        RaisePropertyChanged(nameof(RebindingHintText));
        RaisePropertyChanged(nameof(CopyDeviceInfoText));
        RaisePropertyChanged(nameof(AgreementPrefixText));
        RaisePropertyChanged(nameof(EnterpriseAgreementText));
        RaisePropertyChanged(nameof(AgreementAndText));
        RaisePropertyChanged(nameof(DataProcessingTermsText));
        RaisePropertyChanged(nameof(NeedHelpText));
        RaisePropertyChanged(nameof(ContactAdministratorText));
        RaisePropertyChanged(nameof(CancelButtonText));
        RaisePropertyChanged(nameof(ActivateButtonText));
        RaisePropertyChanged(nameof(StatusModeText));
        RaisePropertyChanged(nameof(StatusSecurityText));
    }
}
