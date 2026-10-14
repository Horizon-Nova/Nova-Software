using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE015 — *Utility/*Utilities 類別必須真的跨出 C# 生態，不然不該用這個角色名。
//
// Utility 的定位：包住 C#/.NET 生態原生沒有、需要外部程序或非受管 API 才能完成的
// 功能（例如透過 PowerShell 取得 CPU 資訊）。這條規則檢查的是「這個角色的定義有
// 沒有被兌現」——沒有任何一處真的跨出受管環境（呼叫 Process、或宣告 DllImport／
// LibraryImport 的 P/Invoke），就只是普通邏輯借了這個名字，變回「拿來裝邏輯」的
// 舊用法，規則存在的目的就是擋住這種名不符實。
//
// 用 IOperation（RegisterOperationAction）偵測 Process/ProcessStartInfo 使用，
// 不手動呼叫 Compilation.GetSemanticModel()（RS1030 禁止）——Operation 本身已經
// 帶著解析好的型別資訊，不需要另外查語意模型。
//
// 已知涵蓋範圍限制：邊界訊號認 Process/ProcessStartInfo 與 DllImport/
// LibraryImport 兩類。其他跨出受管環境的手段（具名管道、COM 互操作等）
// 偵測不到；擴充方式是加進清單，不是改寫規則。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UtilityBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> InteropAttributeNames = ImmutableArray.Create(
        "DllImportAttribute", "LibraryImportAttribute");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.UtilityBoundary);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext startContext)
    {
        var processType = startContext.Compilation.GetTypeByMetadataName("System.Diagnostics.Process");
        var processStartInfoType = startContext.Compilation.GetTypeByMetadataName("System.Diagnostics.ProcessStartInfo");

        var utilityTypes = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
        var boundaryFound = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);

        startContext.RegisterSymbolAction(symbolContext =>
        {
            var type = (INamedTypeSymbol)symbolContext.Symbol;

            if (type.TypeKind != TypeKind.Class || !IsUtilityType(type.Name))
            {
                return;
            }

            utilityTypes[type] = 0;

            var hasInteropAttribute = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(member => member.GetAttributes().Any(a =>
                    a.AttributeClass is { } attributeClass
                    && InteropAttributeNames.Contains(attributeClass.Name)));

            if (hasInteropAttribute)
            {
                boundaryFound[type] = 0;
            }
        }, SymbolKind.NamedType);

        startContext.RegisterOperationAction(operationContext =>
        {
            var containingType = operationContext.ContainingSymbol.ContainingType;

            if (containingType is null || !IsUtilityType(containingType.Name))
            {
                return;
            }

            ITypeSymbol? involvedType = operationContext.Operation switch
            {
                IObjectCreationOperation creation => creation.Type,
                IInvocationOperation invocation => invocation.TargetMethod.ContainingType,
                _ => null
            };

            if (involvedType is null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(involvedType, processType)
                || SymbolEqualityComparer.Default.Equals(involvedType, processStartInfoType))
            {
                boundaryFound[containingType] = 0;
            }
        }, OperationKind.ObjectCreation, OperationKind.Invocation);

        startContext.RegisterCompilationEndAction(endContext =>
        {
            foreach (var type in utilityTypes.Keys)
            {
                if (boundaryFound.ContainsKey(type))
                {
                    continue;
                }

                endContext.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.UtilityBoundary,
                    type.Locations.FirstOrDefault(),
                    type.Name));
            }
        });
    }

    private static bool IsUtilityType(string name)
        => name.EndsWith("Utility", StringComparison.Ordinal)
        || name.EndsWith("Utilities", StringComparison.Ordinal);
}
