using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vaeron.Analyzers.Rules;

// VAE019 — ViewModel 的「控制項」「命令」Region 內容必須符合宣告的契約形狀。
//
// Region 存在的理由是「這裡的東西有一個可反查的外部契約」——控制項對應 XAML
// 繫結，命令對應 XAML Command 繫結，兩者在 CommunityToolkit.Mvvm 底下都是靠
// 屬性標注（source generator）產生實際的繫結目標。本規則檢查「宣告放這裡的欄位，
// 有沒有真的具備那個契約」——沒有標對應屬性，代表它其實不是控制項/命令，只是
// 名字放對了位置，內容沒對上。
//
// 只檢查欄位（field）：手寫的完整屬性（例如自己實作 get/set 邏輯的繫結目標）
// 本身就是完整契約，不需要額外標注，不在本規則檢查範圍內，不算誤判排除，是
// 這條規則的檢查對象本來就只鎖定「宣告了卻沒有产生器接住」的欄位。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ViewModelRegionShapeAnalyzer : DiagnosticAnalyzer
{
    private const string ControlRegion = "控制項";
    private const string CommandRegion = "命令";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.ViewModelRegionShape);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(OnClass, SyntaxKind.ClassDeclaration);
    }

    private static void OnClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (!classDecl.Identifier.Text.EndsWith("ViewModel", StringComparison.Ordinal))
        {
            return;
        }

        CheckControls(context, classDecl);
        CheckCommands(context, classDecl);
    }

    private static void CheckControls(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDecl)
    {
        var span = RegionHelpers.FindSpan(classDecl, ControlRegion);

        if (span is null)
        {
            return;
        }

        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>()
                     .Where(f => span.Value.Contains(f.Span)))
        {
            if (HasAttribute(field.AttributeLists, "ObservableProperty"))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.ViewModelRegionShape,
                    variable.GetLocation(),
                    variable.Identifier.Text,
                    ControlRegion,
                    "[ObservableProperty]"));
            }
        }
    }

    private static void CheckCommands(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDecl)
    {
        var span = RegionHelpers.FindSpan(classDecl, CommandRegion);

        if (span is null)
        {
            return;
        }

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>()
                     .Where(m => span.Value.Contains(m.Span)))
        {
            if (HasAttribute(method.AttributeLists, "RelayCommand"))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.ViewModelRegionShape,
                method.Identifier.GetLocation(),
                method.Identifier.Text,
                CommandRegion,
                "[RelayCommand]"));
        }
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string shortName)
        => attributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute =>
            {
                var name = attribute.Name.ToString();
                return name == shortName || name == shortName + "Attribute";
            });
}
