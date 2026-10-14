using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE002 — 例外只在能對它做出決定的地方被攔截。
//
// 判定依據是所在方法的回傳型別,不是型別名稱。
// 能回傳 Result 代表有能力把失敗表達給呼叫方;不能回傳 Result 的位置 catch,
// 例外即被吞掉。以型別名後綴判定會把具名特例當成結構軸,新增型別必須同步維護名單。
//
// 三個豁免,互斥且窮盡：
//   1 轉換點——方法回傳 Result / Result<T> / 其 Task 包裝
//   2 安全網——方法參數含 Action<Exception>,即 Guard 本身
//   3 測試——位於測試組件,測試驗證例外行為而非處理它
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatchLocationAnalyzer : DiagnosticAnalyzer
{
    private const string ResultTypeName = "Result";
    private const string ResultNamespace = "Vaeron.Kernel";
    private const string TestProjectOption = "build_property.VaeronIsTestProject";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.CatchLocation);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(OnTry, OperationKind.Try);
    }

    private static void OnTry(OperationAnalysisContext context)
    {
        var operation = (ITryOperation)context.Operation;

        if (operation.Catches.Length == 0)
        {
            return;
        }

        // 豁免 3：測試組件
        var options = context.Options.AnalyzerConfigOptionsProvider
            .GetOptions(operation.Syntax.SyntaxTree);

        if (options.TryGetValue(TestProjectOption, out var isTest)
            && string.Equals(isTest, "true", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.ContainingSymbol is not IMethodSymbol method)
        {
            return;
        }

        // 豁免 1：轉換點
        if (ReturnsResult(method.ReturnType))
        {
            return;
        }

        // 豁免 2：安全網
        if (HasFailureHandlerParameter(method))
        {
            return;
        }

        var location = operation.Catches[0].Syntax.GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.CatchLocation,
            location,
            method.Name));
    }

    // Result / Result<T> / Task<Result> / ValueTask<Result<T>> 皆視為轉換點
    private static bool ReturnsResult(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (IsResultType(named))
        {
            return true;
        }

        if (named.IsGenericType
            && (named.Name == "Task" || named.Name == "ValueTask")
            && named.TypeArguments.Length == 1)
        {
            return ReturnsResult(named.TypeArguments[0]);
        }

        return false;
    }

    private static bool IsResultType(INamedTypeSymbol type)
        => type.Name == ResultTypeName
        && type.ContainingNamespace?.ToDisplayString() == ResultNamespace;

    // 安全網的結構特徵：接收一個失敗處置委派
    private static bool HasFailureHandlerParameter(IMethodSymbol method)
        => method.Parameters.Any(parameter =>
            parameter.Type is INamedTypeSymbol { IsGenericType: true } delegateType
            && delegateType.Name == "Action"
            && delegateType.TypeArguments.Length == 1
            && IsExceptionType(delegateType.TypeArguments[0]));

    private static bool IsExceptionType(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "Exception"
                && current.ContainingNamespace?.ToDisplayString() == "System")
            {
                return true;
            }
        }

        return false;
    }
}
