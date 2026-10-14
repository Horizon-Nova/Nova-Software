using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE004 — 禁用符號。
//
// 收錄判準：這個符號是否在任何情境下都有問題。
// 只在特定情境有問題的不收錄——那需要判斷使用情境,符號名判定不出來,
// 收錄後會把正當用法一併擋掉。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedSymbolAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableDictionary<string, string> Banned =
        ImmutableDictionary<string, string>.Empty
            .Add("System.DateTime.Now", "無時區資訊,請用 DateTimeOffset.UtcNow")
            .Add("System.DateTime.Today", "無時區資訊,請用 DateTimeOffset.UtcNow")
            .Add("System.Environment.Exit", "繞過宿主關閉流程");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.BannedSymbol);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(
            OnAccess,
            OperationKind.PropertyReference,
            OperationKind.Invocation);
    }

    private static void OnAccess(OperationAnalysisContext context)
    {
        ISymbol? symbol = context.Operation switch
        {
            IPropertyReferenceOperation property => property.Property,
            IInvocationOperation invocation => invocation.TargetMethod,
            _ => null
        };

        if (symbol is null || symbol.ContainingType is null)
        {
            return;
        }

        var fullName = $"{symbol.ContainingType.ToDisplayString()}.{symbol.Name}";

        if (!Banned.TryGetValue(fullName, out var reason))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.BannedSymbol,
            context.Operation.Syntax.GetLocation(),
            fullName,
            reason));
    }
}
