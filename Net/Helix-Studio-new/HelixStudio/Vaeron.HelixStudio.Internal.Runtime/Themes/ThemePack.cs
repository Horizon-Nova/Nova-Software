using System.Windows;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Internal.Runtime.Themes;

/// <summary>一個可選用的主題，Dictionary 為 null 代表內建預設樣式庫</summary>
public sealed record ThemePack(string Id, string Name, ResourceDictionary? Dictionary)
{
    public const string DefaultId = "default";
    public bool IsBuiltIn => Dictionary is null;

    /// <summary>色卡預覽色，取自該主題自己的字典——四色同時呈現才看得出主題長怎樣，單一底色+強調色點資訊量不足</summary>
    public Brush? PreviewBackground => Resolve("BgPageBrush");
    public Brush? PreviewSidebar => Resolve("BgSidebarBrush");
    public Brush? PreviewAccent => Resolve("AccentSolidBrush");
    public Brush? PreviewText => Resolve("TextPrimaryBrush");

    private Brush? Resolve(string key)
        => Dictionary?[key] as Brush ?? Application.Current?.TryFindResource(key) as Brush;
}
