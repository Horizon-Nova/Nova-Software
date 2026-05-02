using System.Diagnostics;
using System.IO;
using System.Reflection;
using Helix_Studio.Model.Activation;

namespace Helix_Studio.Services;

/// <summary>提供啟用流程、執行時期資訊與法務相關檔案開啟的薄層服務。</summary>
public sealed class ActivationServices
{
    private readonly LicenseServices _licenseService;

    /// <summary>以授權服務建立 <see cref="ActivationServices"/> 實例。</summary>
    /// <param name="licenseService">授權狀態讀寫服務。</param>
    public ActivationServices(LicenseServices licenseService)
    {
        _licenseService = licenseService;
    }

    /// <summary>以授權金鑰完成本機啟用並寫入事件日誌。</summary>
    public void Activate(string licenseKey)
    {
        _licenseService.Activate(licenseKey);
        EventLogServices.Info("已於本機模式完成授權啟用。", "License");
    }

    /// <summary>查詢啟用視窗顯示用的裝置名稱、指紋摘要與組建字串。</summary>
    public ActivationRuntimeInfo QueryRuntimeInfo()
    {
        var deviceName = Environment.MachineName;
        var fingerprint = _licenseService.QueryDeviceFingerprintSha256();
        var shortFingerprint = fingerprint.Length > 12
            ? $"{fingerprint[..6]}...{fingerprint[^4..]}"
            : fingerprint;

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var buildText = version is null
            ? "Build —"
            : $"Build {version.Major}.{version.Minor}.{version.Build}";

        return new ActivationRuntimeInfo(deviceName, shortFingerprint, buildText);
    }

    /// <summary>以預設關聯程式開啟 <c>Assets/Legal</c> 目錄內的指定檔案。</summary>
    /// <returns>若檔案存在並已嘗試啟動則為 <c>true</c>，否則為 <c>false</c>。</returns>
    public bool OpenLegalFile(string fileName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Legal", fileName);
        if (!File.Exists(path))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
        return true;
    }
}


