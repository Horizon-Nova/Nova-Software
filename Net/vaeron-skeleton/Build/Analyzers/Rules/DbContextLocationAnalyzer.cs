using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE022 — DbContext 衍生類別須宣告於共用層，不得宣告在功能組件內。
//
// 一個 DbContext 就是一個主題。主題底下的多個功能共用同一組表，功能本身不是
// 開表的單位——功能組件內若能宣告 DbContext，等於功能可以自己生出一個主題與
// 一組專屬的表，主題層級的歸屬就不存在了。
//
// 與 VAE021 同一個位置要求：主題（DbContext）與該主題的表（實體型別）同屬共用
// 層，功能組件只放 view 與查詢。
//
// 共用層名稱取自 build_property.VaeronSharedFolderName（來源為 Workspace.props），
// 不在分析器內寫死；屬性未傳入時本規則不動作。
//
// 涵蓋範圍的誠實標注：主題該怎麼切、哪些功能屬於同一個主題、某張表該不該併進
// 既有寬表，都不在偵測範圍內。本規則只保證 DbContext 的宣告位置不落在功能組件。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbContextLocationAnalyzer : DiagnosticAnalyzer
{
    private const string SharedFolderOption = "build_property.VaeronSharedFolderName";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.DbContextLocation);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || !DerivesFromDbContext(type))
        {
            return;
        }

        foreach (var location in type.Locations)
        {
            var tree = location.SourceTree;

            if (tree is null)
            {
                continue;
            }

            var sharedFolder = context.Options.AnalyzerConfigOptionsProvider
                .GetOptions(tree)
                .TryGetValue(SharedFolderOption, out var configured)
                    ? configured
                    : null;

            if (string.IsNullOrWhiteSpace(sharedFolder) || IsUnderSharedFolder(tree.FilePath, sharedFolder!))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.DbContextLocation,
                location,
                type.Name,
                sharedFolder));
        }
    }

    private static bool DerivesFromDbContext(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name == "DbContext")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderSharedFolder(string path, string sharedFolder)
        => path.IndexOf($@"\{sharedFolder}\", StringComparison.OrdinalIgnoreCase) >= 0
        || path.IndexOf($"/{sharedFolder}/", StringComparison.OrdinalIgnoreCase) >= 0;
}
