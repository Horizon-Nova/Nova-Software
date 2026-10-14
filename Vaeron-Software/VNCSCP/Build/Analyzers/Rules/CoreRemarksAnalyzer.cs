using System;
using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE016 — *Core 類別必須有 <remarks>，說明取代了什麼平台預設機制。
//
// Core 的定位：平台本來有預設做法（例如 ASP.NET Core 的 cookie-based session），
// 這個專案刻意不用，自己寫一套取代。CLAUDE.md 已經把 <remarks> 定義為「真正踩過
// 坑、需要交代前因後果的地方」——Core 存在的理由本身就是這種情況，沒有 <remarks>
// 等於沒有交代「為什麼不用平台預設」，讀者看不出這是刻意繞開，還是漏掉平台已經
// 提供的東西。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CoreRemarksAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.CoreRemarks);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(OnClass, SyntaxKind.ClassDeclaration);
    }

    private static void OnClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (!classDecl.Identifier.Text.EndsWith("Core", StringComparison.Ordinal))
        {
            return;
        }

        var hasRemarks = classDecl.GetLeadingTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .SelectMany(doc => doc.Content.OfType<XmlElementSyntax>())
            .Any(element => element.StartTag.Name.LocalName.Text == "remarks"
                && element.Content.ToString().Trim().Length > 0);

        if (hasRemarks)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.CoreRemarks,
            classDecl.Identifier.GetLocation(),
            classDecl.Identifier.Text));
    }
}
