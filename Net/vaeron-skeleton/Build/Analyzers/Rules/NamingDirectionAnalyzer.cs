using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE008 — 方法命名前綴宣告服務方向：對呼叫方（Facade/Service/ViewModel/Logic）或對資源（Store）。
// 借用另一方向的前綴，等於謊報這個方法在哪一層。
//
// 判準來自方向，不是層級名稱本身：
//   對呼叫方（Facade/Service/ViewModel/Logic）— 完成一個呼叫方看得懂的動作，不得用 Query*（資源索取語意）
//   對資源（Store）                          — 向資料源要什麼／寫什麼，不得用 Load*/Save*（對呼叫方語意）
// Get* 兩個方向都不宣告，全角色永久禁用。
//
// Service／ViewModel 是同一個角色在不同平台的稱呼（WPF 用 ViewModel、Web 用
// Service），不是兩個獨立角色，方向判準當然相同。
//
// Logic 這條是低把握度分類：現在只知道它「不對外面的呼叫方發生資料索取語意」，
// 沒有像 Service/Store 那樣有具體範例佐證專屬詞庫，先歸進跟 Service 同一組，
// 有更具體案例時應該重新檢視，不是定案。
//
// 角色判定用型別名稱後綴，不用資料夾路徑——Facade/Service/Store 在骨架裡同層並存
// （Templates/App/*/Internal/），路徑分不出角色，後綴才是骨架實際採用的區分依據。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamingDirectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> OutwardBanned =
        ImmutableArray.Create("Query", "Get");

    private static readonly ImmutableArray<string> InwardBanned =
        ImmutableArray.Create("Load", "Save", "Get");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.NamingDirection);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnMethod, SymbolKind.Method);
    }

    private static void OnMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.DeclaredAccessibility != Accessibility.Public
            || method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        var banned = BannedPrefixesFor(method.ContainingType.Name);

        if (banned.IsDefault)
        {
            return;
        }

        var hit = banned.FirstOrDefault(prefix =>
            method.Name.StartsWith(prefix, StringComparison.Ordinal));

        if (hit is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.NamingDirection,
            method.Locations.FirstOrDefault(),
            method.Name,
            hit));
    }

    // 角色與方向的對照表——新增角色只需在此加一行，不需要新增分支結構。
    private static ImmutableArray<string> BannedPrefixesFor(string typeName)
    {
        if (typeName.EndsWith("Service", StringComparison.Ordinal)
            || typeName.EndsWith("Facade", StringComparison.Ordinal)
            || typeName.EndsWith("ViewModel", StringComparison.Ordinal)
            || typeName.EndsWith("Logic", StringComparison.Ordinal))
        {
            return OutwardBanned;
        }

        if (typeName.EndsWith("Store", StringComparison.Ordinal))
        {
            return InwardBanned;
        }

        return default;
    }
}
