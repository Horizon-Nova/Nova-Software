using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Internal.Runtime.Themes;

/// <summary>主題包掃描與套用</summary>
public sealed class ThemePackService(AuditLogging audit)
{
    /// <summary>基礎字典，需與 App.xaml 的 MergedDictionaries 一致</summary>
    private static readonly string[] BaseDictionaries =
        ["Themes/Tokens.xaml", "Themes/Controls.xaml", "Themes/Icons.xaml"];

    public string ThemeDirectory => Path.Combine(Paths.Roaming, "Themes");

    /// <summary>主題套用完成，供 Shell 重新套用背景媒體</summary>
    public event Action? ThemeApplied;

    /// <summary>掃描主題資料夾，一個子資料夾一個主題</summary>
    public IReadOnlyList<ThemePack> LoadAvailablePacks()
    {
        var packs = new List<ThemePack> { new(ThemePack.DefaultId, "預設", null) };
        if (!Directory.Exists(ThemeDirectory)) return packs;

        foreach (var folder in Directory.EnumerateDirectories(ThemeDirectory))
        {
            var manifest = Path.Combine(folder, "theme.xaml");
            if (!File.Exists(manifest)) continue;

            var id = Path.GetFileName(folder);
            var loaded = Load(manifest);
            if (!loaded.IsSuccess || loaded.Value is null)
            {
                audit.Log(new AuditEntry(nameof(ThemePackService), $"主題包「{id}」載入失敗：{loaded.Message}"));
                continue;
            }

            var dictionary = loaded.Value;
            ResolveMediaPath(dictionary, folder);
            packs.Add(new ThemePack(id, dictionary["ThemeName"] as string ?? id, dictionary));
        }

        return packs;
    }

    /// <summary>套用主題：重建合併字典，主題包疊在基礎字典之上</summary>
    public Result Apply(ThemePack pack)
    {
        var app = Application.Current;
        if (app is null) return Result.Fail(ResultKind.InfrastructureError, "應用程式尚未初始化");

        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        foreach (var path in BaseDictionaries)
            merged.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        if (pack.Dictionary is not null) merged.Add(pack.Dictionary);

        ThemeApplied?.Invoke();
        return new Result
        {
            Kind = ResultKind.Success,
            Message = pack.IsBuiltIn ? "已套用預設樣式" : $"已套用主題「{pack.Name}」",
        };
    }

    /// <summary>背景媒體宣告的是相對檔名，轉成絕對路徑供 MediaElement 使用</summary>
    private static void ResolveMediaPath(ResourceDictionary dictionary, string packFolder)
    {
        if (dictionary["BackgroundMedia"] is not string relative || relative.Length == 0) return;
        dictionary["BackgroundMedia"] = Path.Combine(packFolder, relative);
    }

    /// <summary>載入主題包，格式與型別錯誤由 XamlReader 自己回報</summary>
    private static Result<ResourceDictionary> Load(string manifestPath)
    {
        try
        {
            var safeResult = ThemePackValidator.CheckForCodeExecution(manifestPath);
            if (!safeResult.IsSuccess)
                return Result<ResourceDictionary>.Fail(ResultKind.ValidationFailed, safeResult.Message);

            using var stream = File.OpenRead(manifestPath);
            var context = new ParserContext
            {
                BaseUri = new Uri(Path.GetDirectoryName(manifestPath)! + Path.DirectorySeparatorChar),
            };
            return Result<ResourceDictionary>.Ok((ResourceDictionary)XamlReader.Load(stream, context));
        }
        catch (XamlParseException ex) { return Result<ResourceDictionary>.Fail(ResultKind.InfrastructureError, ex.Message); }
        catch (InvalidCastException) { return Result<ResourceDictionary>.Fail(ResultKind.InfrastructureError, "最外層必須是 ResourceDictionary"); }
        catch (XmlException ex) { return Result<ResourceDictionary>.Fail(ResultKind.InfrastructureError, ex.Message); }
        catch (IOException ex) { return Result<ResourceDictionary>.Fail(ResultKind.InfrastructureError, ex.Message); }
    }
}
