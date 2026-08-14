using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE003 — public 方法的簽名要表達語意,不只表達型別。
//
// 擋的是「多個原始型別參數並排」——呼叫端看不出哪個是什麼,順序寫錯編譯器也不擋：
//     UpdateAsync(5, "abc", true)
// 不擋單一原始型別參數：語意由方法名承載,包一層具名型別只增加負擔。
//
// 兩項結構性豁免,兩者都是「建立具名型別」這件事本身：
//   record / record struct 的主建構式——把原始型別包成具名型別的動作
//   靜態工廠方法(回傳所屬型別)——具名型別自己的建構入口
// 擋這兩者等於擋掉本規則要求的補救方式。
//
// 回傳值另有判準：可能失敗的操作必須回傳 Result;純判定 bool 與數值語意 int 除外。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimitiveSignatureAnalyzer : DiagnosticAnalyzer
{
    // 兩個以上並排才構成「分不出誰是誰」
    private const int BareParameterThreshold = 2;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.PrimitiveSignature);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnMethod, SymbolKind.Method);
    }

    private static void OnMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            return;
        }

        if (method.MethodKind != MethodKind.Ordinary && method.MethodKind != MethodKind.Constructor)
        {
            return;
        }

        if (method.IsOverride || method.IsImplicitlyDeclared)
        {
            return;
        }

        CheckParameters(context, method);

        if (method.MethodKind == MethodKind.Constructor)
        {
            return;
        }

        CheckReturnType(context, method);
    }

    private static void CheckParameters(SymbolAnalysisContext context, IMethodSymbol method)
    {
        if (IsRecordPrimaryConstructor(method) || IsStaticFactory(method))
        {
            return;
        }

        var bareParameters = method.Parameters
            .Where(parameter => IsBare(parameter.Type))
            .ToArray();

        if (bareParameters.Length < BareParameterThreshold)
        {
            return;
        }

        var names = string.Join(", ", bareParameters.Select(parameter => parameter.Name));

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.PrimitiveSignature,
            method.Locations.FirstOrDefault(),
            method.Name,
            $"並排的原始型別參數 ({names})"));
    }

    private static void CheckReturnType(SymbolAnalysisContext context, IMethodSymbol method)
    {
        var returnType = Unwrap(method.ReturnType);

        if (!IsBare(returnType) || IsAllowedReturn(returnType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.PrimitiveSignature,
            method.Locations.FirstOrDefault(),
            method.Name,
            "回傳值"));
    }

    // record / record struct 的主建構式：這正是把原始型別包成具名型別的動作
    private static bool IsRecordPrimaryConstructor(IMethodSymbol method)
        => method.MethodKind == MethodKind.Constructor
        && method.ContainingType.IsRecord;

    // 靜態工廠：回傳所屬型別本身,是該具名型別的建構入口
    private static bool IsStaticFactory(IMethodSymbol method)
    {
        if (!method.IsStatic || method.MethodKind != MethodKind.Ordinary)
        {
            return false;
        }

        var returnType = Unwrap(method.ReturnType);
        var containingType = method.ContainingType;

        if (SymbolEqualityComparer.Default.Equals(returnType, containingType))
        {
            return true;
        }

        // Result.Fail 回傳 Result,Result<T>.Fail 回傳 Result<T>——比對原始定義
        return returnType is INamedTypeSymbol named
            && SymbolEqualityComparer.Default.Equals(
                named.OriginalDefinition,
                containingType.OriginalDefinition);
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.IsGenericType
            && (named.Name == "Task" || named.Name == "ValueTask")
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    private static bool IsBare(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsTupleType: true })
        {
            return true;
        }

        return type.SpecialType == SpecialType.System_Int32
            || type.SpecialType == SpecialType.System_Int64
            || type.SpecialType == SpecialType.System_String
            || type.SpecialType == SpecialType.System_Double
            || type.SpecialType == SpecialType.System_Single
            || type.SpecialType == SpecialType.System_Decimal
            || type.SpecialType == SpecialType.System_Boolean;
    }

    // 純判定 bool(不可能失敗)、數值語意 int(計數/索引)
    private static bool IsAllowedReturn(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Boolean
        || type.SpecialType == SpecialType.System_Int32;
}
