using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE007 — 帶資料表映射屬性的型別須位於 Internal\Models\ 之下。
//
// 分開存放的理由是變化源不同,不是分類美觀：
//   Internal\Models\ — 形狀跟著自己的資料庫 schema 變,自己可控
//   Internal\Wire\   — 形狀跟著外部系統的 API 變,完全不可控
// 兩者機械上同形(皆為 internal、可變、無行為),混放後無法區分哪一個變動
// 是自己造成的、哪一個是外部強加的。
//
// 以屬性「名稱」比對而非符號解析：不同 ORM 各有自己的映射屬性,
// 綁死單一套件會讓規則只對一種 ORM 成立,違反「規則要能推導未列舉的情況」。
//
// 涵蓋範圍的誠實標注：完全以 Fluent API 設定、原始碼上不帶任何映射屬性的實體,
// 沒有任何符號特徵可供識別,本規則偵測不到。這是判定依據的邊界,不是實作缺口。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityLocationAnalyzer : DiagnosticAnalyzer
{
    private const string WindowsSegment = @"\Internal\Models\";
    private const string PosixSegment = "/Internal/Models/";

    private static readonly ImmutableHashSet<string> TypeLevelAttributes =
        ImmutableHashSet.Create(StringComparer.Ordinal, "TableAttribute");

    private static readonly ImmutableHashSet<string> MemberLevelAttributes =
        ImmutableHashSet.Create(StringComparer.Ordinal, "KeyAttribute", "ColumnAttribute");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.EntityLocation);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
        {
            return;
        }

        if (!HasMappingAttribute(type))
        {
            return;
        }

        foreach (var location in type.Locations)
        {
            var path = location.SourceTree?.FilePath;

            if (string.IsNullOrEmpty(path) || IsUnderModelsFolder(path!))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.EntityLocation,
                location,
                type.Name));

            return;
        }
    }

    private static bool IsUnderModelsFolder(string path)
        => path.IndexOf(WindowsSegment, StringComparison.Ordinal) >= 0
        || path.IndexOf(PosixSegment, StringComparison.Ordinal) >= 0;

    private static bool HasMappingAttribute(INamedTypeSymbol type)
    {
        if (type.GetAttributes().Any(attribute =>
                attribute.AttributeClass is { } attributeClass
                && TypeLevelAttributes.Contains(attributeClass.Name)))
        {
            return true;
        }

        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .SelectMany(property => property.GetAttributes())
            .Any(attribute =>
                attribute.AttributeClass is { } attributeClass
                && MemberLevelAttributes.Contains(attributeClass.Name));
    }
}
