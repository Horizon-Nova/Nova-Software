using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE006 — 位於 Internal 資料夾的型別不得對外可見。
//
// 這條把「位置」變成可執行的宣告：把型別放進 Internal\ 等同宣告它不對外,
// 分析器驗證的是「宣告與可見性一致」,不是「你選對了位置沒有」——後者是語意判斷,
// 機械上做不到。
//
// 判定用檔案路徑而非命名空間：命名空間對 CLR 無意義,可任意宣告;
// 路徑則對應實際的資料夾結構,骨架其餘檢查(S001-S013)也一律以路徑判定。
//
// 路徑分隔符同時比對兩種形式——同一份原始碼在不同作業系統下建置時,
// SyntaxTree.FilePath 的分隔符不同。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InternalVisibilityAnalyzer : DiagnosticAnalyzer
{
    private const string WindowsSegment = @"\Internal\";
    private const string PosixSegment = "/Internal/";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.InternalVisibility);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(OnType, SymbolKind.NamedType);
    }

    private static void OnType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!IsExternallyVisible(type))
        {
            return;
        }

        foreach (var location in type.Locations)
        {
            var path = location.SourceTree?.FilePath;

            if (string.IsNullOrEmpty(path) || !IsUnderInternalFolder(path!))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.InternalVisibility,
                location,
                type.Name));

            return;
        }
    }

    private static bool IsUnderInternalFolder(string path)
        => path.IndexOf(WindowsSegment, System.StringComparison.Ordinal) >= 0
        || path.IndexOf(PosixSegment, System.StringComparison.Ordinal) >= 0;

    // 巢狀於 internal 型別內的 public 成員實際上不對外可見——
    // 逐層往上檢查,任一層不是 public 即不構成外洩。
    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public
                && current.DeclaredAccessibility != Accessibility.Protected
                && current.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
            {
                return false;
            }
        }

        return true;
    }
}
