using System.IO;
using System.Xml;

namespace Vaeron.HelixStudio.Themes;

/// <summary>主題包安全檢查：只擋 WPF 自己不會擋的（可執行程式碼的型別、外部素材路徑）</summary>
public static class ThemePackValidator
{
    /// <summary>可執行程式碼的型別</summary>
    private static readonly HashSet<string> ForbiddenElements = new(StringComparer.Ordinal)
    {
        "ObjectDataProvider", "XmlDataProvider", "EventSetter",
    };

    /// <summary>可觸及任意型別或靜態成員的標記延伸</summary>
    private static readonly string[] ForbiddenMarkupExtensions = ["x:Static", "x:Type"];

    /// <summary>指向外部素材的屬性</summary>
    private static readonly string[] SourceAttributes = ["Source", "ImageSource", "UriSource"];

    public static (bool Ok, string Message) CheckForCodeExecution(string xamlPath)
    {
        using var reader = XmlReader.Create(xamlPath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        });

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;

            if (ForbiddenElements.Contains(reader.LocalName))
                return (false, $"主題包不得使用「{reader.LocalName}」，該型別可執行程式碼");

            if (!reader.HasAttributes) continue;
            while (reader.MoveToNextAttribute())
            {
                foreach (var ext in ForbiddenMarkupExtensions)
                {
                    if (reader.Value.Contains(ext, StringComparison.Ordinal))
                        return (false, $"主題包不得使用「{ext}」標記延伸");
                }

                if (!SourceAttributes.Contains(reader.LocalName, StringComparer.Ordinal)) continue;
                var (pathOk, pathReason) = CheckSourcePath(reader.Value);
                if (!pathOk) return (false, pathReason);
            }
            reader.MoveToElement();
        }

        return (true, "");
    }

    /// <summary>素材路徑限主題包資料夾內的相對路徑</summary>
    private static (bool Ok, string Message) CheckSourcePath(string value)
    {
        if (value.Length == 0 || value[0] == '{') return (true, "");

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return (false, $"素材路徑不得使用絕對位址「{absolute.Scheme}:」");

        if (value.Contains("..", StringComparison.Ordinal))
            return (false, "素材路徑不得指向主題包資料夾之外");

        return (true, "");
    }
}
