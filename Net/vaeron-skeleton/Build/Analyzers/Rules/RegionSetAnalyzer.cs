using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE009 — Facade/Service/Store 依角色要求固定 Region 集合。
//
// Region 名稱是 preprocessor trivia，編譯器語意層看不到，須用語法樹 trivia 掃描，
// 跟其餘用符號解析的規則不同層級——這是本規則能檢查到什麼、檢查不到什麼的邊界。
//
// 案例集合（哪個角色要哪些 Region）放在下方唯讀表格，不展開成 if/switch 分支；
// 新增角色只需在表格加一行。
//
// 已知涵蓋範圍限制：巢狀類別的 Region 會被外層一併掃到（DescendantTrivia 會遞迴進
// 巢狀節點）。骨架目前的 Facade/Service/Store 都是頂層類別，暫不處理巢狀情況。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegionSetAnalyzer : DiagnosticAnalyzer
{
    private readonly struct RoleRegions
    {
        public RoleRegions(ImmutableArray<string> required, ImmutableArray<string> optional)
        {
            Required = required;
            Optional = optional;
        }

        public ImmutableArray<string> Required { get; }
        public ImmutableArray<string> Optional { get; }
    }

    // Service 的 Region 集合。驗證邏輯屬於 Logic，Service 沒有驗證方法可放。
    // 兩個都必存在，可以是空的，比照 Store。
    private static readonly RoleRegions ServiceRegions = new(
        ImmutableArray.Create("統一的查詢方法", "基本 CRUD 操作"),
        ImmutableArray<string>.Empty);

    private static readonly RoleRegions StoreRegions = new(
        ImmutableArray.Create("統一的查詢來源", "專用查詢方法", "基本 CRUD 操作"),
        ImmutableArray.Create("特殊查詢"));

    // ViewModel（WPF）的 7 類契約全部列為 optional，不是隨便放寬：不是每個 ViewModel
    // 都會有全部 7 種東西（例如沒有訂閱任何事件的 ViewModel 不需要「生命週期」），
    // 但只要出現 Region，名稱必須是這 7 個之一（exact_set）——防止用其他名字亂分類。
    private static readonly RoleRegions ViewModelRegions = new(
        ImmutableArray<string>.Empty,
        ImmutableArray.Create("控制項", "命令", "生命週期", "事件回呼", "外部操作方法", "屬性變更通知", "內部方法"));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.RegionSet);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(OnClass, SyntaxKind.ClassDeclaration);
    }

    private static void OnClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var name = classDecl.Identifier.Text;
        var roleRegions = RegionsFor(name);

        if (roleRegions is null)
        {
            return;
        }

        var required = roleRegions.Value.Required;
        var optional = roleRegions.Value.Optional;

        var regionNames = classDecl.DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
            .Select(RegionHelpers.GetName)
            .Where(regionName => regionName is not null)
            .Select(regionName => regionName!)
            .ToImmutableArray();

        foreach (var missing in required.Except(regionNames))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.RegionSet,
                classDecl.Identifier.GetLocation(),
                name,
                $"缺少必存在 Region：{missing}"));
        }

        var allowed = required.Concat(optional).ToImmutableHashSet();

        foreach (var extra in regionNames.Where(regionName => !allowed.Contains(regionName)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.RegionSet,
                classDecl.Identifier.GetLocation(),
                name,
                $"不在允許清單內的 Region：{extra}"));
        }
    }

    // 角色與 Region 集合的對照表——新增角色只需在此加一行。
    private static RoleRegions? RegionsFor(string typeName)
    {
        if (typeName.EndsWith("Service", StringComparison.Ordinal))
        {
            return ServiceRegions;
        }

        if (typeName.EndsWith("Store", StringComparison.Ordinal))
        {
            return StoreRegions;
        }

        if (typeName.EndsWith("ViewModel", StringComparison.Ordinal))
        {
            return ViewModelRegions;
        }

        // Facade 刻意不在此表——骨架定義 Facade 只委派不含邏輯（單一 => 委派運算式），
        // 強制切 Region 對一行委派方法沒有意義，屬於行為等價判準下的贅字包裝。
        return null;
    }
}
