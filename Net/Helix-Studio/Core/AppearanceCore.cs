using System;
using Helix_Studio.Model;

namespace Helix_Studio.Core;

/// <summary>外觀核心，依 INI 套用主題與語系。</summary>
public sealed class AppearanceCore : IDisposable
{
    private const string SettingsSection = "Settings";
    private readonly IniCore _iniCore;
    private readonly ThemeCore _themeCore;

    /// <summary>建立外觀核心。</summary>
    public AppearanceCore(IniCore iniCore, ThemeCore themeCore)
    {
        _iniCore = iniCore;
        _themeCore = themeCore;

        ApplyAppearance();
        _iniCore.Changed += OnIniChanged;
    }

    /// <summary>依目前設定套用外觀。</summary>
    public void ApplyAppearance()
    {
        var theme = _iniCore.QuerySnapshot<ThemeModel>(SettingsSection);
        _themeCore.ApplyAppearance(theme.ThemeIndex, theme.LanguageIndex);
    }

    /// <summary>釋放外觀監聽資源。</summary>
    public void Dispose() => _iniCore.Changed -= OnIniChanged;

    private void OnIniChanged(object? sender, EventArgs e) => ApplyAppearance();
}
