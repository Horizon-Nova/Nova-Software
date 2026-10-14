using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE005 — 禁止以字串串接或內插組成 SQL。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlConcatAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] Keywords =
    {
        "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "FROM ", "WHERE "
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.SqlConcat);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(
            OnOperation,
            OperationKind.Binary,
            OperationKind.InterpolatedString);
    }

    private static void OnOperation(OperationAnalysisContext context)
    {
        var operation = context.Operation;

        if (operation is IBinaryOperation binary)
        {
            if (binary.OperatorKind != BinaryOperatorKind.Add)
            {
                return;
            }

            if (binary.Type is null || binary.Type.SpecialType != SpecialType.System_String)
            {
                return;
            }

            if (!ContainsSql(binary.Syntax.ToString()))
            {
                return;
            }
        }
        else if (operation is IInterpolatedStringOperation interpolated)
        {
            // 全為常值者不構成注入風險
            if (interpolated.Parts.All(part => part is IInterpolatedStringTextOperation))
            {
                return;
            }

            if (!ContainsSql(interpolated.Syntax.ToString()))
            {
                return;
            }
        }
        else
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.SqlConcat,
            operation.Syntax.GetLocation()));
    }

    private static bool ContainsSql(string text)
    {
        var upper = text.ToUpperInvariant();
        return Keywords.Any(keyword => upper.IndexOf(keyword, StringComparison.Ordinal) >= 0);
    }
}
