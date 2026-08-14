using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE014 — Middleware／Filter 建構子不得注入業務層型別（*Service/*Store/*Facade）。
//
// Middleware/Filter 是跨橫切關注點的管線基礎設施，不是業務入口——直接依賴業務層
// 型別，代表業務邏輯正在往基礎設施層滲透。這是「不必要的設計」在這個角色最容易
// 出現的具體形狀：一開始只是記錄 log 的 middleware，某天被塞進一個 Service 依賴，
// 從此開始夾帶業務判斷，範圍長歪了卻沒有任何機械訊號能抓到——型別系統允許注入
// 任何東西，需要額外這條規則把「不該依賴誰」講清楚。
//
// 跟 repositories.md「禁止呼叫 Service」同一種精神，方向相反：那條管下層不得依賴
// 上層，這條管基礎設施不得依賴業務層。
//
// 角色判定用型別名稱後綴／實作介面，不用資料夾——跟 VAE008 同一個理由，這幾種
// 角色在骨架裡沒有固定要放在哪個資料夾。
//
// Controller 不在本規則的判定對象內：ASP.NET Core 的 Controller 基底本身實作
// IActionFilter/IAsyncActionFilter，僅以介面判定會把每個 Controller 一併算成
// Filter。Controller 注入 Service/Facade 是它的既定職責，繼承 ControllerBase
// 的型別在此排除。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PipelineDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> FilterInterfaces = ImmutableArray.Create(
        "IActionFilter", "IAsyncActionFilter",
        "IExceptionFilter", "IAsyncExceptionFilter",
        "IAuthorizationFilter", "IAsyncAuthorizationFilter",
        "IResultFilter", "IAsyncResultFilter",
        "IResourceFilter", "IAsyncResourceFilter");

    private static readonly ImmutableArray<string> BannedDependencySuffixes = ImmutableArray.Create(
        "Service", "Store", "Facade");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.PipelineDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || !IsPipelineType(type))
        {
            return;
        }

        foreach (var constructor in type.Constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                var parameterTypeName = parameter.Type.Name;

                if (!BannedDependencySuffixes.Any(suffix =>
                        parameterTypeName.EndsWith(suffix, StringComparison.Ordinal)))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.PipelineDependency,
                    parameter.Locations.FirstOrDefault(),
                    type.Name,
                    parameter.Name,
                    parameterTypeName));
            }
        }
    }

    // 角色與判定方式的對照表——新增角色在此加一行。
    private static bool IsPipelineType(INamedTypeSymbol type)
    {
        if (DerivesFromControllerBase(type))
        {
            return false;
        }

        if (type.Name.EndsWith("Middleware", StringComparison.Ordinal)
            || type.Name.EndsWith("Filter", StringComparison.Ordinal))
        {
            return true;
        }

        return type.AllInterfaces.Any(i =>
            i.Name == "IMiddleware" || FilterInterfaces.Contains(i.Name));
    }

    private static bool DerivesFromControllerBase(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "ControllerBase")
            {
                return true;
            }
        }

        return false;
    }
}
