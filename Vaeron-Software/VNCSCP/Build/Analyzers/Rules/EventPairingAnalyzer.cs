using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vaeron.Analyzers.Rules;

// VAE001 — 型別內每個 += 訂閱,須有對應的 -= 解除。
// 平台做不到：需語意模型比對事件符號,且橫跨同型別的多個方法。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventPairingAnalyzer : DiagnosticAnalyzer
{
    // ConcurrentBag 的元素型別必須具名,不可用 tuple——tuple 在此處無法推斷
    private readonly struct Subscription
    {
        public Subscription(ISymbol eventSymbol, Location location)
        {
            EventSymbol = eventSymbol;
            Location = location;
        }

        public ISymbol EventSymbol { get; }
        public Location Location { get; }
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Descriptors.EventPairing);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolStartAction(OnTypeStart, SymbolKind.NamedType);
    }

    private static void OnTypeStart(SymbolStartAnalysisContext context)
    {
        var subscribed = new ConcurrentBag<Subscription>();
        var unsubscribed = new ConcurrentBag<ISymbol>();

        context.RegisterOperationAction(
            operationContext =>
            {
                var assignment = (IEventAssignmentOperation)operationContext.Operation;

                if (assignment.EventReference is not IEventReferenceOperation reference)
                {
                    return;
                }

                if (assignment.Adds)
                {
                    subscribed.Add(new Subscription(
                        reference.Event,
                        operationContext.Operation.Syntax.GetLocation()));
                }
                else
                {
                    unsubscribed.Add(reference.Event);
                }
            },
            OperationKind.EventAssignment);

        context.RegisterSymbolEndAction(endContext =>
        {
            var removed = ImmutableHashSet.CreateRange(
                SymbolEqualityComparer.Default,
                unsubscribed);

            foreach (var subscription in subscribed)
            {
                if (removed.Contains(subscription.EventSymbol))
                {
                    continue;
                }

                endContext.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.EventPairing,
                    subscription.Location,
                    subscription.EventSymbol.Name));
            }
        });
    }
}
