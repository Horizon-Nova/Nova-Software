using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE017 — Service/ViewModel 不得注入自己 Feature 的 Facade。
//
// Facade 是別的 Feature 與殼層存取本 Feature 的入口。同 Feature 的 Service/
// ViewModel 呼叫自己的 Facade 是純委派空層——行為等價：拿掉它，呼叫端收到的
// 東西不變。跨 Feature 注入別人的 Facade 合法（殼層 ViewModel 同時組合多個
// Feature 的 Facade 即是），本規則只擋同 Feature 自己注入自己。
//
// Feature 邊界用命名空間根判定：命名空間如果含 ".Internal"，取它前面那段當
// Feature 根；兩個型別的 Feature 根相同，才算「同一個 Feature」。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FacadeSelfInjectionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.FacadeSelfInjection);

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

        var ownFeatureRoot = FeatureRoot(type.ContainingNamespace);

        foreach (var constructor in type.Constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.Type is not INamedTypeSymbol parameterType
                    || !parameterType.Name.EndsWith("Facade", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameterFeatureRoot = FeatureRoot(parameterType.ContainingNamespace);

                if (!string.Equals(ownFeatureRoot, parameterFeatureRoot, StringComparison.Ordinal))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.FacadeSelfInjection,
                    parameter.Locations.FirstOrDefault(),
                    type.Name,
                    parameterType.Name));
            }
        }
    }

    private static bool IsConnectorType(string name)
        => name.EndsWith("Service", StringComparison.Ordinal)
        || name.EndsWith("ViewModel", StringComparison.Ordinal);

    private static string FeatureRoot(INamespaceSymbol ns)
    {
        var full = ns.ToDisplayString();
        var internalIndex = full.IndexOf(".Internal", StringComparison.Ordinal);
        return internalIndex < 0 ? full : full.Substring(0, internalIndex);
    }
}
