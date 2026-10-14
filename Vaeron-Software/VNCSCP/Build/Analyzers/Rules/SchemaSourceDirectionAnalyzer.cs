using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE023 — 資料庫結構的真相來源是資料庫，不是 C#。
//
// 型別由映射工具從資料庫產生；反方向（C# 定義結構、由程式推導出結構變更並套用到
// 資料庫）在此禁止。兩個方向同時存在時，同一份結構有兩個互相衝突的來源。
//
// 桌面軟體的資料庫檔第一次啟動時不存在，仍不構成例外：隨程式打包結構定義檔或
// 空白範本檔、啟動時套用，一樣能自建資料庫，且真相來源仍在資料庫端。
//
// 判定對象：
//   型別 — 繼承 Migration／ModelSnapshot，或帶 [Migration] 屬性
//   呼叫 — DatabaseFacade 上的 Migrate/MigrateAsync
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SchemaSourceDirectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BannedBaseTypes =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Migration", "ModelSnapshot");

    private static readonly ImmutableHashSet<string> BannedMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Migrate", "MigrateAsync");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.SchemaSourceDirection);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
        context.RegisterOperationAction(OnInvocation, OperationKind.Invocation);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var hasAttribute = type.GetAttributes()
            .Any(a => string.Equals(a.AttributeClass?.Name, "MigrationAttribute", StringComparison.Ordinal));

        if (!hasAttribute && !DerivesFromBanned(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.SchemaSourceDirection,
            type.Locations.FirstOrDefault(),
            type.Name));
    }

    private static void OnInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!BannedMethods.Contains(invocation.TargetMethod.Name))
        {
            return;
        }

        if (invocation.TargetMethod.ContainingType?.Name is not "DatabaseFacade" and not "RelationalDatabaseFacadeExtensions")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.SchemaSourceDirection,
            invocation.Syntax.GetLocation(),
            invocation.TargetMethod.Name));
    }

    private static bool DerivesFromBanned(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (BannedBaseTypes.Contains(current.Name))
            {
                return true;
            }
        }

        return false;
    }
}
