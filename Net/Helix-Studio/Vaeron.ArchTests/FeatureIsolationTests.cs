using NetArchTest.Rules;
using Xunit;

namespace Vaeron.ArchTests;

public sealed class FeatureIsolationTests
{
    private static readonly System.Reflection.Assembly AppAssembly = typeof(Vaeron.HelixStudio.App).Assembly;
    private const string FeaturesRoot = "Vaeron.HelixStudio.Features";

    /// <summary>solution-structure.md 給的斷言模板，套用到實際 7 個 Feature，並修正模板的一個真實缺口：
    ///
    /// 模板原文只排除「類別名稱結尾 Facade」，但 contracts.md 明文規定「Facade 方法只回傳 Contracts
    /// 型別（Snapshot）」——任何合法呼叫其他 Feature Facade 的地方，必然也會參照到對方 Contracts
    /// 命名空間下的型別（例如 AppLibrary 呼叫 AppsManagementFacade.LoadInstalledToolListAsync 時，
    /// 回傳型別 InstalledToolSnapshot 本身就在 Vaeron.HelixStudio.Features.AppsManagement.Contracts）。
    ///
    /// 若直接把整個功能命名空間（含 .Contracts）當一個字串丟給 NetArchTest 的 HaveDependencyOnAny
    /// 做前綴比對，Contracts 型別會被誤判成「跨功能引用內部實作」而擋下——這是合法、且被 contracts.md
    /// 要求的用法。修法：不用命名空間前綴字串當禁止清單，改成逐一列舉每個 Feature「真正內部」的型別
    /// 全名（排除 Facade 類別與 .Contracts 命名空間下的型別）當禁止清單，用型別全名比對，不會誤傷
    /// 命名空間結構不同的 Contracts 型別。</summary>
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

    /// <summary>solution-structure.md 依賴法第 3 條：AI.Data 只能被 AI.Clients 引用。
    /// AI.Clients／AI.Data 目前都是空殼（沒有 Feature 需要 AI 能力，見計畫書假設 2），
    /// 這裡先確保現況成立（App 主程式不得繞過 AI.Clients 直接碰 AI.Data），
    /// 之後兩個專案真的有內容時，這條斷言會繼續有效，不需要另外補。</summary>
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
