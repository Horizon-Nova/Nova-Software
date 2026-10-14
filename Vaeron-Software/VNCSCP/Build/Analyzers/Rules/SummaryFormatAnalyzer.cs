using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE010 — <summary> 必須單行。
//
// 「doc comment 沒放在合法宣告上」（例如寫進函式主體）已經是編譯器的 CS1587，
// 骨架 TreatWarningsAsErrors=true 且 NoWarn 沒排除 CS1587，平台原生就是錯誤，
// 不需要另外寫規則檢查這件事。
//
// 本規則只補平台沒有的部分：單行是這個工作區的慣例，不是語言規則，
// C# 編譯器不管 <summary> 內容佔幾行。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SummaryFormatAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.SummaryFormat);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(OnSyntaxTree);
    }

    private static void OnSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);

        var docComments = root.DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>();

        foreach (var doc in docComments)
        {
            var summary = doc.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(element => element.StartTag.Name.LocalName.Text == "summary");

            if (summary is null)
            {
                continue;
            }

            var span = summary.GetLocation().GetLineSpan();

            if (span.StartLinePosition.Line != span.EndLinePosition.Line)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.SummaryFormat,
                    summary.GetLocation()));
            }
        }
    }
}
