namespace Helix_Studio.Model.Activation;

/// <summary>啟動／授權畫面顯示用之裝置與版本資訊快照。</summary>
/// <param name="DeviceName">裝置名稱。</param>
/// <param name="ShortFingerprint">裝置指紋簡短顯示字串。</param>
/// <param name="BuildText">組建版本顯示文字。</param>
public sealed record ActivationRuntimeInfo(string DeviceName, string ShortFingerprint, string BuildText);
