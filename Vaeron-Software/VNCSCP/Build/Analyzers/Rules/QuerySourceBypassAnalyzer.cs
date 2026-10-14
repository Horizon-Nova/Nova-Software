using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE011 — 「專用查詢方法」不得繞過「統一的查詢來源」直接摸查詢來源包住的根物件。
//
// 查詢來源屬性集中套一次基礎過濾條件（軟刪除、啟用狀態等）；個別查詢方法如果繞過它、
// 直接對根物件（例如 dbContext）發查詢，基礎過濾條件對那條查詢就不生效——這正是
// 查詢來源這個 Region 存在的理由，不是命名習慣。
//
// 判定用符號解析找查詢來源屬性的「根物件」（表達式鏈最左邊的識別字解析出的符號），
// 再檢查「專用查詢方法」內有沒有直接引用同一個符號。同一根物件、不同過濾條件的
// 兩個查詢來源屬性都算數，只要方法繞過其中任一個都算違規。
//
// 只管「專用查詢方法」——「基本 CRUD 操作」依角色定義本來就允許直接碰根物件（寫入
// 不套用查詢過濾條件），不在此規則範圍內。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuerySourceBypassAnalyzer : DiagnosticAnalyzer
{
    private const string QuerySourceRegion = "統一的查詢來源";
    private const string QueryMethodRegion = "專用查詢方法";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.QuerySourceBypass);

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

        // 兩個 Region 都不存在時交給 VAE009 報「缺少 Region」，這裡不重複判斷。
        var querySourceSpan = RegionHelpers.FindSpan(classDecl, QuerySourceRegion);
        var queryMethodSpan = RegionHelpers.FindSpan(classDecl, QueryMethodRegion);

        if (querySourceSpan is null || queryMethodSpan is null)
        {
            return;
        }

        var semanticModel = context.SemanticModel;

        var bannedRoots = classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => querySourceSpan.Value.Contains(p.Span))
            .Select(p => p.ExpressionBody?.Expression)
            .Where(expr => expr is not null)
            .Select(expr => FindRootIdentifier(expr!))
            .Where(root => root is not null)
            .Select(root => semanticModel.GetSymbolInfo(root!).Symbol)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .ToImmutableHashSet<ISymbol>(SymbolEqualityComparer.Default);

        if (bannedRoots.Count == 0)
        {
            return;
        }

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>()
                     .Where(m => queryMethodSpan.Value.Contains(m.Span)))
        {
            foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                if (symbol is null || !bannedRoots.Contains(symbol))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.QuerySourceBypass,
                    identifier.GetLocation(),
                    method.Identifier.Text,
                    symbol.Name));
            }
        }
    }

    // 表達式鏈（a.b.Where(...).Select(...) 這種）最左邊的識別字——查詢來源屬性真正
    // 包住的根物件。
    private static IdentifierNameSyntax? FindRootIdentifier(ExpressionSyntax expr)
    {
        while (true)
        {
            switch (expr)
            {
                case InvocationExpressionSyntax invocation:
                    expr = invocation.Expression;
                    continue;
                case MemberAccessExpressionSyntax memberAccess:
                    expr = memberAccess.Expression;
                    continue;
                case IdentifierNameSyntax identifier:
                    return identifier;
                default:
                    return null;
            }
        }
    }
}
