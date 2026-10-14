using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE018 — Service/ViewModel 不得注入 *Store，只能經由 *Logic 存取倉儲。
//
// Logic 存在的理由是集中業務規則；Service/ViewModel 如果還能直接注入 Store，
// 規則判斷就會有兩條路徑可以碰到資料——一條經過 Logic（走規則），一條直接繞過去
// （不走規則）。兩條路徑並存等於 Logic 集中規則這件事形同虛設,跟 VAE011（查詢
// 來源不可繞過）、VAE017（不得自我注入 Facade）同一種「不准繞過」判準，只是換了
// 兩端的角色。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StoreBypassAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.StoreBypass);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || !IsConnectorType(type.Name))
        {
            return;
        }

        foreach (var constructor in type.Constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (!parameter.Type.Name.EndsWith("Store", StringComparison.Ordinal))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.StoreBypass,
                    parameter.Locations.FirstOrDefault(),
                    type.Name,
                    parameter.Type.Name));
            }
        }
    }

    private static bool IsConnectorType(string name)
        => name.EndsWith("Service", StringComparison.Ordinal)
        || name.EndsWith("ViewModel", StringComparison.Ordinal);
}
