using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE013 — 同一資料夾內，共用同一個非 object 基底型別／介面的類別，名稱後綴必須彼此一致。
//
// 判準跟資料夾叫什麼名字無關：兩個以上類別放在同一個資料夾、共用某個非 object 的
// 基底型別或介面，代表這個資料夾裝的是同一角色的重複實例，後綴要一致——後綴文字
// 本身不規定，只規定「這一組裡面要一樣」。單一檔案、或彼此沒有共用基底的資料夾
// （主題分組，例如 Identity/、Lifecycle/ 底下放不同種類的型別合作完成一件事），
// 不落在本規則範圍內，不需要黑白名單排除。
//
// 後綴定義：識別字用 PascalCase 詞界拆開後的最後一個詞（BannedSymbolAnalyzer →
// Banned／Symbol／Analyzer，取 Analyzer）。連續大寫視為一個詞的一部分，直到遇到
// 後面接小寫才斷詞（處理 XMLParser 這類縮寫開頭的情況）。
//
// 用 RegisterCompilationStartAction + RegisterSyntaxNodeAction + RegisterCompilationEndAction
// 收集全編譯單元的類別再統一比對——不手動呼叫 Compilation.GetSemanticModel()
// （RS1030 禁止），改用每個語法節點回呼自帶的 SemanticModel。
//
// 已知涵蓋範圍限制：門檻只有「非 object」，不排除通用框架介面（IDisposable、
// IEquatable<T> 等）。兩個彼此無關的類別各自實作同一個通用介面時，會被分進
// 同一組，屬於誤判。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FolderSuffixConsistencyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.FolderSuffixConsistency);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext startContext)
    {
        var members = new ConcurrentBag<(INamedTypeSymbol Symbol, ClassDeclarationSyntax Syntax, string Folder)>();

        startContext.RegisterSyntaxNodeAction(nodeContext =>
        {
            var classDecl = (ClassDeclarationSyntax)nodeContext.Node;
            var filePath = classDecl.SyntaxTree.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var folder = Path.GetDirectoryName(filePath);

            if (folder is null)
            {
                return;
            }

            var symbol = nodeContext.SemanticModel.GetDeclaredSymbol(classDecl, nodeContext.CancellationToken);

            // 巢狀型別不是資料夾裡的「同層角色」，是外層類別自己的實作細節——
            // 只比對頂層類別，ContainingType 非 null 代表這是巢狀在別的型別裡面。
            if (symbol is not null && symbol.ContainingType is null)
            {
                members.Add((symbol, classDecl, folder));
            }
        }, SyntaxKind.ClassDeclaration);

        startContext.RegisterCompilationEndAction(endContext =>
        {
            foreach (var folderGroup in members.GroupBy(m => m.Folder))
            {
                CheckFolder(endContext, folderGroup.ToList());
            }
        });
    }

    private static void CheckFolder(
        CompilationAnalysisContext context,
        List<(INamedTypeSymbol Symbol, ClassDeclarationSyntax Syntax, string Folder)> folderMembers)
    {
        var baseGroups = new Dictionary<ISymbol, List<(INamedTypeSymbol Symbol, ClassDeclarationSyntax Syntax)>>(
            SymbolEqualityComparer.Default);

        foreach (var (symbol, syntax, _) in folderMembers)
        {
            foreach (var baseType in RelevantBases(symbol))
            {
                if (!baseGroups.TryGetValue(baseType, out var list))
                {
                    list = new List<(INamedTypeSymbol, ClassDeclarationSyntax)>();
                    baseGroups[baseType] = list;
                }

                list.Add((symbol, syntax));
            }
        }

        foreach (var entry in baseGroups)
        {
            var baseType = entry.Key;
            var group = entry.Value;

            if (group.Count < 2)
            {
                continue;
            }

            var withSuffix = group
                .Select(g => (g.Symbol, g.Syntax, Suffix: LastPascalWord(g.Symbol.Name)))
                .ToList();

            var majoritySuffix = withSuffix
                .GroupBy(g => g.Suffix)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First()
                .Key;

            foreach (var item in withSuffix)
            {
                if (item.Suffix == majoritySuffix)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.FolderSuffixConsistency,
                    item.Syntax.Identifier.GetLocation(),
                    item.Symbol.Name,
                    baseType.Name,
                    majoritySuffix));
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> RelevantBases(INamedTypeSymbol symbol)
    {
        for (var current = symbol.BaseType;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            yield return current;
        }

        foreach (var iface in symbol.Interfaces)
        {
            yield return iface;
        }
    }

    private static string LastPascalWord(string name)
    {
        var words = SplitPascalCase(name);
        return words.Count == 0 ? name : words[words.Count - 1];
    }

    private static List<string> SplitPascalCase(string name)
    {
        var words = new List<string>();
        var start = 0;

        for (var i = 1; i < name.Length; i++)
        {
            var isBoundary = char.IsUpper(name[i])
                && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));

            if (isBoundary)
            {
                words.Add(name.Substring(start, i - start));
                start = i;
            }
        }

        words.Add(name.Substring(start));
        return words;
    }
}
