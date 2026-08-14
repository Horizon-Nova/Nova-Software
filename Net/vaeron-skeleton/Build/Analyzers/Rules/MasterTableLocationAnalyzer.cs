using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE021 — 主表映射型別須宣告於共用層，不得宣告在功能組件內。
//
// 主表是跨功能共用的資產：新功能需要欄位時加進既有主表，不新開一張功能專屬表。
// 型別宣告的位置就是這件事的機械保證——主表型別放在共用層，功能組件無法在自己
// 的範圍內生出一張只有自己用的表。
//
// view 不受本規則限制：view 是功能專屬、隨功能一起建立與刪除的東西，留在功能
// 組件內，與主表分屬不同生命週期。
//
// 實體的判定涵蓋兩種設定方式：
//   屬性設定 — 型別帶 [Table]/[Key]/[Column]
//   Fluent  — 型別出現在 DbContext 衍生類別的 DbSet<T> 屬性
// view 的判定同樣涵蓋兩種：
//   屬性設定 — [Keyless]
//   Fluent  — EntityTypeBuilder<T> 上呼叫 ToView/HasNoKey/ToSqlQuery/ToFunction
//
// 共用層名稱取自 build_property.VaeronSharedFolderName（來源為 Workspace.props），
// 不在分析器內寫死；屬性未傳入時本規則不動作。
//
// 涵蓋範圍的誠實標注：
//   1. 判定依據是型別宣告位置，不是資料庫實際結構。一張表有幾個欄位、被幾個功能
//      共用、某個欄位該加進哪一張表，都不在偵測範圍內。
//   2. DbSet<T> 與 T 的宣告分屬不同組件時，T 在本次編譯中只有中繼資料位置、沒有
//      原始碼位置，不觸發。功能組件內自建主表（DbContext 與實體同組件）是本規則
//      的主要偵測對象，這種跨組件情況不在其中。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MasterTableLocationAnalyzer : DiagnosticAnalyzer
{
    private const string SharedFolderOption = "build_property.VaeronSharedFolderName";

    private static readonly ImmutableHashSet<string> TypeLevelAttributes =
        ImmutableHashSet.Create(StringComparer.Ordinal, "TableAttribute");

    private static readonly ImmutableHashSet<string> MemberLevelAttributes =
        ImmutableHashSet.Create(StringComparer.Ordinal, "KeyAttribute", "ColumnAttribute");

    private static readonly ImmutableHashSet<string> ViewConfiguringMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "ToView", "HasNoKey", "ToSqlQuery", "ToFunction");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.MasterTableLocation);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext startContext)
    {
        var entities = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
        var views = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);

        startContext.RegisterSymbolAction(symbolContext =>
        {
            var type = (INamedTypeSymbol)symbolContext.Symbol;

            CollectAttributeDeclared(type, entities, views);
            CollectDbSetDeclared(type, entities);
        }, SymbolKind.NamedType);

        startContext.RegisterOperationAction(operationContext =>
        {
            var invocation = (IInvocationOperation)operationContext.Operation;

            if (!ViewConfiguringMethods.Contains(invocation.TargetMethod.Name))
            {
                return;
            }

            var builder = invocation.TargetMethod.ContainingType is { TypeArguments.Length: 1 } declaring
                ? declaring
                : invocation.Instance?.Type as INamedTypeSymbol;

            if (builder is { TypeArguments.Length: 1 })
            {
                views[builder.TypeArguments[0]] = 0;
            }
        }, OperationKind.Invocation);

        startContext.RegisterCompilationEndAction(endContext =>
        {
            foreach (var entity in entities.Keys)
            {
                if (views.ContainsKey(entity))
                {
                    continue;
                }

                Report(endContext, entity);
            }
        });
    }

    private static void CollectAttributeDeclared(
        INamedTypeSymbol type,
        ConcurrentDictionary<ISymbol, byte> entities,
        ConcurrentDictionary<ISymbol, byte> views)
    {
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
        {
            return;
        }

        if (type.GetAttributes().Any(a => string.Equals(a.AttributeClass?.Name, "KeylessAttribute", StringComparison.Ordinal)))
        {
            views[type] = 0;
            return;
        }

        var typeLevel = type.GetAttributes()
            .Any(a => a.AttributeClass is { } attribute && TypeLevelAttributes.Contains(attribute.Name));

        var memberLevel = type.GetMembers()
            .OfType<IPropertySymbol>()
            .SelectMany(member => member.GetAttributes())
            .Any(a => a.AttributeClass is { } attribute && MemberLevelAttributes.Contains(attribute.Name));

        if (typeLevel || memberLevel)
        {
            entities[type] = 0;
        }
    }

    private static void CollectDbSetDeclared(INamedTypeSymbol type, ConcurrentDictionary<ISymbol, byte> entities)
    {
        if (!DerivesFromDbContext(type))
        {
            return;
        }

        var declared = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => property.Type)
            .OfType<INamedTypeSymbol>()
            .Where(propertyType => propertyType.Name == "DbSet" && propertyType.TypeArguments.Length == 1)
            .Select(propertyType => propertyType.TypeArguments[0]);

        foreach (var entity in declared)
        {
            entities[entity] = 0;
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

    private static void Report(CompilationAnalysisContext context, ISymbol entity)
    {
        foreach (var location in entity.Locations)
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
                Descriptors.MasterTableLocation,
                location,
                entity.Name,
                sharedFolder));
        }
    }

    private static bool IsUnderSharedFolder(string path, string sharedFolder)
        => path.IndexOf($@"\{sharedFolder}\", StringComparison.OrdinalIgnoreCase) >= 0
        || path.IndexOf($"/{sharedFolder}/", StringComparison.OrdinalIgnoreCase) >= 0;
}
