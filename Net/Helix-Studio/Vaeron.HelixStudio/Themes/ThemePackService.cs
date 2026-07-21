using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Themes;

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
        => Dictionary?[key] as Brush ?? System.Windows.Application.Current?.TryFindResource(key) as Brush;
}

/// <summary>主題包掃描與套用</summary>
public sealed class ThemePackService(AppDataPath paths, AuditLogging audit)
{
    /// <summary>基礎字典，需與 App.xaml 的 MergedDictionaries 一致</summary>
    private static readonly string[] BaseDictionaries =
        ["Themes/Tokens.xaml", "Themes/Controls.xaml", "Themes/Icons.xaml"];

    public string ThemeDirectory => paths.ThemeDirectory;

    /// <summary>主題套用完成，供 Shell 重新套用背景媒體</summary>
    public event Action? ThemeApplied;

    /// <summary>掃描主題資料夾，一個子資料夾一個主題</summary>
    public IReadOnlyList<ThemePack> LoadAvailablePacks()
    {
        var packs = new List<ThemePack> { new(ThemePack.DefaultId, "預設", null) };
        if (!Directory.Exists(paths.ThemeDirectory)) return packs;

        foreach (var folder in Directory.EnumerateDirectories(paths.ThemeDirectory))
        {
            var manifest = Path.Combine(folder, "theme.xaml");
            if (!File.Exists(manifest)) continue;

            var id = Path.GetFileName(folder);
            var (ok, dictionary, message) = Load(manifest);
            if (!ok || dictionary is null)
            {
                audit.Log(nameof(ThemePackService), $"主題包「{id}」載入失敗：{message}");
                continue;
            }

            ResolveMediaPath(dictionary, folder);
            packs.Add(new ThemePack(id, dictionary["ThemeName"] as string ?? id, dictionary));
        }

        return packs;
    }

    /// <summary>套用主題：重建合併字典，主題包疊在基礎字典之上</summary>
    public (bool success, string message) Apply(ThemePack pack)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return (false, $"{ResultContract.FailPrefix}應用程式尚未初始化");

        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        foreach (var path in BaseDictionaries)
            merged.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        if (pack.Dictionary is not null) merged.Add(pack.Dictionary);

        ThemeApplied?.Invoke();
        return (true, pack.IsBuiltIn ? "已套用預設樣式" : $"已套用主題「{pack.Name}」");
    }

    /// <summary>背景媒體宣告的是相對檔名，轉成絕對路徑供 MediaElement 使用</summary>
    private static void ResolveMediaPath(ResourceDictionary dictionary, string packFolder)
    {
        if (dictionary["BackgroundMedia"] is not string relative || relative.Length == 0) return;
        dictionary["BackgroundMedia"] = Path.Combine(packFolder, relative);
    }

    /// <summary>載入主題包，格式與型別錯誤由 XamlReader 自己回報</summary>
    private static (bool ok, ResourceDictionary? dictionary, string message) Load(string manifestPath)
    {
        try
        {
            var (safe, reason) = ThemePackValidator.CheckForCodeExecution(manifestPath);
            if (!safe) return (false, null, reason);

            using var stream = File.OpenRead(manifestPath);
            var context = new ParserContext
            {
                BaseUri = new Uri(Path.GetDirectoryName(manifestPath)! + Path.DirectorySeparatorChar),
            };
            return (true, (ResourceDictionary)XamlReader.Load(stream, context), "");
        }
        catch (XamlParseException ex) { return (false, null, ex.Message); }
        catch (InvalidCastException) { return (false, null, "最外層必須是 ResourceDictionary"); }
        catch (XmlException ex) { return (false, null, ex.Message); }
        catch (IOException ex) { return (false, null, ex.Message); }
    }
}
