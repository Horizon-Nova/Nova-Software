using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE020 —「專用查詢方法」只能查 view 型別，不得直接查主表型別。
//
// view 是 keyless 型別，EF Core 追蹤不到、寫不回去——查詢方法拿到的型別在型別
// 層級就不具寫入能力，讀寫路徑分離由編譯期保證，不靠人記得遵守。
// 過濾條件（軟刪除、狀態、權限範圍）定義在 view，所有經過它的查詢都套得到，
// 不必逐條查詢檢查有沒有漏加。
// 主表欄位變動時 view 是緩衝層，可維持既有欄位形狀，變動不直接波及查詢。
//
// view 的判定用 [Keyless]，不是型別名稱前綴：view 沒有主鍵，EF Core scaffold
// 產生 view 對應型別時本來就會標這個屬性——平台原生訊號，不是自訂命名慣例，
// 換一套命名前綴規則也不影響判定。
//
// 只管「專用查詢方法」；「基本 CRUD 操作」寫入本來就要對主表做，不在範圍內。
//
// 涵蓋範圍的誠實標注：
//   1. 分析器只看得到 C# 原始碼。view 在資料庫裡實際存不存在、欄位對不對得上、
//      主表結構有沒有漂移，都不在偵測範圍內。
//   2. 完全以 Fluent API 的 .ToView()/.HasNoKey() 設定、原始碼上不帶 [Keyless] 的
//      型別，沒有符號特徵可供識別，偵測不到。這是判定依據的邊界，跟 VAE007 同一種。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ViewOnlyQueryAnalyzer : DiagnosticAnalyzer
{
    private const string QuerySourceRegion = "統一的查詢來源";
    private const string QueryMethodRegion = "專用查詢方法";
    private const string KeylessAttributeName = "KeylessAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.ViewOnlyQuery);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(OnClass, SyntaxKind.ClassDeclaration);
    }

    private static void OnClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (!classDecl.Identifier.Text.EndsWith("Store", StringComparison.Ordinal))
        {
            return;
        }

        var querySourceSpan = RegionHelpers.FindSpan(classDecl, QuerySourceRegion);
        var queryMethodSpan = RegionHelpers.FindSpan(classDecl, QueryMethodRegion);

        if (querySourceSpan is null || queryMethodSpan is null)
        {
            return;
        }

        var semanticModel = context.SemanticModel;

        // 查詢來源屬性 → 它承載的實體型別
        var tableBackedSources = classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => querySourceSpan.Value.Contains(p.Span))
            .Select(p => semanticModel.GetDeclaredSymbol(p))
            .OfType<IPropertySymbol>()
            .Select(p => (Property: (ISymbol)p, Entity: ElementTypeOf(p.Type)))
            .Where(pair => pair.Entity is not null && !IsView(pair.Entity!))
            .ToImmutableDictionary(
                pair => pair.Property,
                pair => pair.Entity!,
                SymbolEqualityComparer.Default);

        if (tableBackedSources.Count == 0)
        {
            return;
        }

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>()
                     .Where(m => queryMethodSpan.Value.Contains(m.Span)))
        {
            foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                if (symbol is null || !tableBackedSources.TryGetValue(symbol, out var entity))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.ViewOnlyQuery,
                    identifier.GetLocation(),
                    method.Identifier.Text,
                    symbol.Name,
                    entity.Name));
            }
        }
    }

    private static ITypeSymbol? ElementTypeOf(ITypeSymbol type)
        => type is INamedTypeSymbol { TypeArguments.Length: 1 } named
            ? named.TypeArguments[0]
            : null;

    private static bool IsView(ITypeSymbol entity)
        => entity.GetAttributes().Any(a =>
            string.Equals(a.AttributeClass?.Name, KeylessAttributeName, StringComparison.Ordinal));
}
