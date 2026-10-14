using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Vaeron.Analyzers.Rules;

// Region trivia 的共用讀取邏輯——VAE009／VAE011 都要抓 Region 名稱或範圍,
// 抽出來避免兩處各自重複同一段 trivia 解析。
//
// 找對應的 #endregion 不用第三方套件（Roslynator 有 GetEndRegionDirective，
// 但骨架沒有引用它），改用深度計數自己配對——#region 進一層、#endregion 出一層,
// 深度歸零那個 #endregion 就是配對對象,天然支援巢狀 Region。
internal static class RegionHelpers
{
    /// <summary>取 #region 後面的說明文字，沒有文字回傳 null。</summary>
    public static string? GetName(SyntaxTrivia regionTrivia)
    {
        if (regionTrivia.GetStructure() is not RegionDirectiveTriviaSyntax directive)
        {
            return null;
        }

        var message = directive.EndOfDirectiveToken.LeadingTrivia
            .FirstOrDefault(t => t.IsKind(SyntaxKind.PreprocessingMessageTrivia));

        var text = message.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>找出容器內指定名稱的 Region 範圍（含 #region 到 #endregion 之間）。找不到回傳 null。</summary>
    public static TextSpan? FindSpan(SyntaxNode container, string regionName)
    {
        var directives = container.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia) || t.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
            .ToList();

        var startIndex = -1;

        for (var i = 0; i < directives.Count; i++)
        {
            if (directives[i].IsKind(SyntaxKind.RegionDirectiveTrivia) && GetName(directives[i]) == regionName)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            return null;
        }

        var depth = 0;

        for (var i = startIndex; i < directives.Count; i++)
        {
            if (directives[i].IsKind(SyntaxKind.RegionDirectiveTrivia))
            {
                depth++;
                continue;
            }

            depth--;

            if (depth == 0)
            {
                return TextSpan.FromBounds(directives[startIndex].SpanStart, directives[i].Span.End);
            }
        }

        return null;
    }
}
