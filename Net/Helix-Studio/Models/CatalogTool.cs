using Helix_Studio.Mvvm;

namespace Helix_Studio.Models;

/// <summary>
/// 應用程式庫中的一個目錄工具：manifest + 是否已安裝。
/// 安裝狀態可變（安裝/解除安裝即時反映），故繼承 ObservableObject。
/// </summary>
public sealed class CatalogTool : ObservableObject
{
    private bool _isInstalled;

    public required ToolManifest Manifest { get; init; }

    public string Id => Manifest.Id;
    public string Name => Manifest.Name;

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }
}
