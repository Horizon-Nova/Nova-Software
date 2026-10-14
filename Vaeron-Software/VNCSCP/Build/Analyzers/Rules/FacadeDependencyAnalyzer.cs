using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE024 — Facade 只能注入串接層（*Service／*ViewModel），不得注入 *Logic 或 *Store。
//
// 呼叫鏈是 UI → 串接 → Logic → Store，Facade 是這條鏈對外的入口。Facade 直接
// 注入 Logic 或 Store，等於外部呼叫端接上的位置比內部 UI 還深，同一個功能因此
// 有兩條深度不同的進入路徑。
//
// 與 VAE018 對稱：VAE018 擋串接層往下跳過 Logic，本規則擋 Facade 往下跳過串接層。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FacadeDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> BannedSuffixes =
        ImmutableArray.Create("Logic", "Store");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.FacadeDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class
            || !type.Name.EndsWith("Facade", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var constructor in type.Constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                var hit = BannedSuffixes.FirstOrDefault(suffix =>
                    parameter.Type.Name.EndsWith(suffix, StringComparison.Ordinal));

                if (hit is null)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.FacadeDependency,
                    parameter.Locations.FirstOrDefault(),
                    type.Name,
                    parameter.Type.Name));
            }
        }
    }
}
