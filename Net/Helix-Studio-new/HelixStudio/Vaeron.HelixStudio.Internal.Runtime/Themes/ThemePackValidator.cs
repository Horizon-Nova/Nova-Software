using System.IO;
using System.Xaml;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Internal.Runtime.Themes;

/// <summary>主題包安全檢查：只擋 WPF 自己不會擋的（可執行程式碼的型別、外部素材路徑）。
/// 用 XamlXmlReader 的型別節點流比對，不對原始 XML 字串做子字串比對——
/// 命名空間前綴可任意取別名，字串比對會被 {別名:Static ...} 這類寫法繞過，型別比對不會。</summary>
public static class ThemePackValidator
{
    /// <summary>可執行程式碼、或可觸及任意型別/靜態成員的型別——
    /// StaticExtension／TypeExtension 涵蓋 {x:Static}／{x:Type} 不論以哪個前綴別名出現</summary>
    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.Windows.Data.ObjectDataProvider",
        "System.Windows.Data.XmlDataProvider",
        "System.Windows.EventSetter",
        "System.Windows.Markup.StaticExtension",
        "System.Windows.Markup.TypeExtension",
    };

    /// <summary>指向外部素材的屬性</summary>
    private static readonly string[] SourceAttributes = ["Source", "ImageSource", "UriSource"];

    public static Result CheckForCodeExecution(string xamlPath)
    {
        try
        {
            using var reader = new XamlXmlReader(xamlPath);
            while (reader.Read())
            {
                if (reader.NodeType == XamlNodeType.StartObject)
                {
                    var typeName = reader.Type?.UnderlyingType?.FullName;
                    if (typeName is not null && ForbiddenTypes.Contains(typeName))
                    {
                        return Result.Fail(ResultKind.ValidationFailed,
                            $"主題包不得使用「{reader.Type!.Name}」，該型別可執行程式碼或觸及任意成員");
                    }
                    continue;
                }

                if (reader.NodeType != XamlNodeType.StartMember) continue;
                if (reader.Member is null || !SourceAttributes.Contains(reader.Member.Name, StringComparer.Ordinal)) continue;

                if (reader.Read() && reader.NodeType == XamlNodeType.Value && reader.Value is string path)
                {
                    var pathResult = CheckSourcePath(path);
                    if (!pathResult.IsSuccess) return pathResult;
                }
            }
        }
        catch (XamlParseException ex) { return Result.Fail(ResultKind.InfrastructureError, ex.Message); }
        catch (IOException ex) { return Result.Fail(ResultKind.InfrastructureError, ex.Message); }

        return Result.Ok();
    }

    /// <summary>素材路徑限主題包資料夾內的相對路徑</summary>
    private static Result CheckSourcePath(string value)
    {
        if (value.Length == 0 || value[0] == '{') return Result.Ok();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return Result.Fail(ResultKind.ValidationFailed, $"素材路徑不得使用絕對位址「{absolute.Scheme}:」");

        if (value.Contains("..", StringComparison.Ordinal))
            return Result.Fail(ResultKind.ValidationFailed, "素材路徑不得指向主題包資料夾之外");

        return Result.Ok();
    }
}
