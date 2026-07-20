using NetArchTest.Rules;
using Xunit;

namespace Vaeron.ArchTests;

public sealed class FeatureIsolationTests
{
    private static readonly System.Reflection.Assembly AppAssembly = typeof(Vaeron.HelixStudio.App).Assembly;
    private const string FeaturesRoot = "Vaeron.HelixStudio.Features";

    /// <summary>Feature 不得引用其他 Feature 的內部型別（Facade／Contracts 除外）</summary>
    [Fact]
    public void Feature不得引用其他Feature的內部型別()
    {
        var allFeatureTypes = Types.InAssembly(AppAssembly)
            .That().ResideInNamespaceStartingWith(FeaturesRoot)
            .GetTypes()
            .Select(t => new { Type = t, Feature = RootFeatureNamespace(t.Namespace) })
            .Where(x => x.Feature is not null)
            .ToList();

        var featureNames = allFeatureTypes.Select(x => x.Feature!).Distinct().ToArray();

        foreach (var feature in featureNames)
        {
            var ownTypes = allFeatureTypes.Where(x => x.Feature == feature).Select(x => x.Type).ToArray();

            var otherInternalTypeNames = allFeatureTypes
                .Where(x => x.Feature != feature)
                .Where(x => !x.Type.Name.EndsWith("Facade"))
                .Where(x => x.Type.Namespace is null || !x.Type.Namespace.StartsWith($"{x.Feature}.Contracts"))
                .Select(x => x.Type.FullName!)
                .Distinct()
                .ToArray();

            if (otherInternalTypeNames.Length == 0) continue;

            var result = Types.InAssembly(AppAssembly)
                .That().ResideInNamespace(feature)
                .And().DoNotHaveNameEndingWith("Facade")
                .And().DoNotResideInNamespace($"{feature}.Contracts")
                .ShouldNot().HaveDependencyOnAny(otherInternalTypeNames)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{feature} 跨功能引用其他功能內部型別：{string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
        }
    }

    /// <summary>App 不得繞過 AI.Clients 直接引用 AI.Data</summary>
    [Fact]
    public void App不得繞過AIClients直接引用AIData()
    {
        var result = Types.InAssembly(AppAssembly)
            .ShouldNot().HaveDependencyOn("Vaeron.AI.Data")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Platform不得引用Feature()
    {
        var platformAssembly = typeof(Vaeron.Platform.PlatformModule).Assembly;
        var result = Types.InAssembly(platformAssembly)
            .ShouldNot().HaveDependencyOn(FeaturesRoot)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void SharedKernel不得引用Platform()
    {
        var sharedKernelAssembly = typeof(Vaeron.SharedKernel.AppDataPath).Assembly;
        var result = Types.InAssembly(sharedKernelAssembly)
            .ShouldNot().HaveDependencyOn("Vaeron.Platform")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    private static string? RootFeatureNamespace(string? typeNamespace)
    {
        if (typeNamespace is null || !typeNamespace.StartsWith(FeaturesRoot)) return null;
        if (typeNamespace.Length == FeaturesRoot.Length) return null;

        var remainder = typeNamespace[(FeaturesRoot.Length + 1)..];
        var firstSegment = remainder.Split('.')[0];
        return $"{FeaturesRoot}.{firstSegment}";
    }
}
