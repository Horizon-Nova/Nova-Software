using System.Linq;
using Vaeron.Analyzers.Rules;
using Xunit;

namespace Vaeron.Analyzers.Tests;

// 每條規則兩組：應觸發、以及形似但合法不應觸發。
// 誤殺測試比違規測試重要——誤殺會擋掉正確的程式碼並誤導修改方向。

public class EventPairingTests
{
    [Fact]
    public void 訂閱未解除_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<EventPairingAnalyzer>("""
            using System;
            interface IObservableThing { event EventHandler Changed; }
            class Consumer
            {
                void Attach(IObservableThing thing) { thing.Changed += OnChanged; }
                void OnChanged(object? sender, EventArgs e) { }
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "VAE001");
    }

    [Fact]
    public void 訂閱有解除_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<EventPairingAnalyzer>("""
            using System;
            interface IObservableThing { event EventHandler Changed; }
            class Consumer
            {
                void Attach(IObservableThing thing) { thing.Changed += OnChanged; }
                void Detach(IObservableThing thing) { thing.Changed -= OnChanged; }
                void OnChanged(object? sender, EventArgs e) { }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE001");
    }
}

public class CatchLocationTests
{
    // 判定依據是回傳型別,不是型別名稱。以下案例的型別名刻意不帶任何既有白名單後綴,
    // 證明判定與命名無關。

    private const string ResultStub = """
        namespace Vaeron.Kernel
        {
            public readonly struct Result
            {
                public static Result Ok() => default;
            }
        }
        """;

    [Fact]
    public void 回傳void的方法內catch_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<CatchLocationAnalyzer>(ResultStub + """
            using System;
            class OrderProcessor
            {
                public void Load() { try { } catch (Exception) { } }
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "VAE002");
    }

    [Fact]
    public void 回傳Result的方法內catch_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<CatchLocationAnalyzer>(ResultStub + """
            using System;
            using Vaeron.Kernel;
            class OrderProcessor
            {
                public Result Load() { try { return Result.Ok(); } catch (Exception) { return Result.Ok(); } }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE002");
    }

    [Fact]
    public void 參數含失敗處置委派的方法內catch_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<CatchLocationAnalyzer>("""
            using System;
            static class Guard
            {
                public static void Run(Action work, Action<Exception> onFailure)
                {
                    try { work(); } catch (Exception ex) { onFailure(ex); }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE002");
    }

    [Fact]
    public void 名稱含Service但回傳void_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<CatchLocationAnalyzer>("""
            using System;
            class SampleService
            {
                public void Load() { try { } catch (Exception) { } }
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "VAE002");
    }
}

public class PrimitiveSignatureTests
{
    [Fact]
    public void 多個原始型別參數並排_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "public class Service { public void Update(int id, string name, bool active) { } }");

        Assert.Contains(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void 單一原始型別參數_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "public class Service { public void Initialize(string appName) { } }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void record主建構式_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "public sealed record SampleRequest(string Key, string Value, int Count);");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void 靜態工廠回傳所屬型別_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>("""
            public enum Kind { Ok, Bad }
            public readonly record struct Outcome
            {
                public static Outcome Fail(Kind kind, string message) => default;
                public static Outcome Both(string a, string b) => default;
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void 可能失敗的操作回傳裸字串_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "public class Service { public string Load() => \"\"; }");

        Assert.Contains(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void 純判定bool回傳_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "public class Service { public bool Exists() => true; }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE003");
    }

    [Fact]
    public void internal方法_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<PrimitiveSignatureAnalyzer>(
            "class Service { internal void Update(int id, string name, bool active) { } }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE003");
    }
}

public class BannedSymbolTests
{
    [Fact]
    public void DateTimeNow_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<BannedSymbolAnalyzer>(
            "using System; class Clock { void Read() { var now = DateTime.Now; } }");

        Assert.Contains(diagnostics, d => d.Id == "VAE004");
    }

    [Fact]
    public void GuidNewGuid_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<BannedSymbolAnalyzer>(
            "using System; class Ids { void Make() { var id = Guid.NewGuid(); } }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE004");
    }

    [Fact]
    public void DateTimeOffsetUtcNow_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<BannedSymbolAnalyzer>(
            "using System; class Clock { void Read() { var now = DateTimeOffset.UtcNow; } }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE004");
    }
}

public class SqlConcatTests
{
    [Fact]
    public void SQL字串串接_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<SqlConcatAnalyzer>(
            """class Repo { void Query(string id) { var sql = "SELECT * FROM T WHERE Id=" + id; } }""");

        Assert.Contains(diagnostics, d => d.Id == "VAE005");
    }

    [Fact]
    public void 非SQL字串串接_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<SqlConcatAnalyzer>(
            """class Greeter { void Greet(string name) { var text = "Hello, " + name; } }""");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE005");
    }

    [Fact]
    public void 全常值SQL內插_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<SqlConcatAnalyzer>(
            """class Repo { void Query() { var sql = $"SELECT * FROM T"; } }""");

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE005");
    }
}

public class InternalVisibilityTests
{
    private const string InternalPath = @"C:\Ws\App\Vaeron.App.Feature\Internal\Models\LogModel.cs";
    private const string PublicPath = @"C:\Ws\App\Vaeron.App.Feature\FeatureContracts.cs";

    [Fact]
    public void Internal資料夾下的public型別_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<InternalVisibilityAnalyzer>("""
            namespace Vaeron.App.Feature.Internal.Models;
            public sealed class LogModel { }
            """, InternalPath);

        Assert.Contains(diagnostics, d => d.Id == "VAE006");
    }

    [Fact]
    public void Internal資料夾下的internal型別_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<InternalVisibilityAnalyzer>("""
            namespace Vaeron.App.Feature.Internal.Models;
            internal sealed class LogModel { }
            """, InternalPath);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE006");
    }

    // 誤殺校驗一：組件根目錄的 public 契約是本來就該對外的東西,不得被擋。
    [Fact]
    public void 根目錄的public契約_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<InternalVisibilityAnalyzer>("""
            namespace Vaeron.App.Feature;
            public sealed record LogEntry(string Message);
            """, PublicPath);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE006");
    }

    // 誤殺校驗二：巢狀於 internal 型別內的 public 成員實際上不對外可見。
    [Fact]
    public void 巢狀於internal型別內的public型別_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<InternalVisibilityAnalyzer>("""
            namespace Vaeron.App.Feature.Internal.Models;
            internal sealed class Outer { public sealed class Inner { } }
            """, InternalPath);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE006");
    }
}

public class EntityLocationTests
{
    private const string AttributeStub = """
        namespace System.ComponentModel.DataAnnotations.Schema
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class TableAttribute : System.Attribute
            {
                public TableAttribute(string name) { }
            }
        }
        namespace System.ComponentModel.DataAnnotations
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class KeyAttribute : System.Attribute { }
        }
        """;

    private const string ModelsPath = @"C:\Ws\App\Vaeron.App.Feature\Internal\Models\LogModel.cs";
    private const string WirePath = @"C:\Ws\App\Vaeron.App.Feature\Internal\Wire\LogResponse.cs";

    [Fact]
    public void 帶Table屬性但不在Models底下_應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<EntityLocationAnalyzer>(AttributeStub + """

            using System.ComponentModel.DataAnnotations.Schema;
            [Table("log")]
            internal sealed class LogModel { }
            """, WirePath);

        Assert.Contains(diagnostics, d => d.Id == "VAE007");
    }

    [Fact]
    public void 帶Table屬性且位於Models底下_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<EntityLocationAnalyzer>(AttributeStub + """

            using System.ComponentModel.DataAnnotations.Schema;
            [Table("log")]
            internal sealed class LogModel { }
            """, ModelsPath);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE007");
    }

    // 誤殺校驗：Wire Model 形狀與實體幾乎相同(internal、可變、無行為),
    // 差別只在有沒有資料表映射屬性。沒有屬性的不得被當成實體擋下。
    [Fact]
    public void 無映射屬性的Wire型別_不應觸發()
    {
        var diagnostics = AnalyzerHarness.Run<EntityLocationAnalyzer>("""
            namespace Vaeron.App.Feature.Internal.Wire;
            internal sealed class LogResponse
            {
                public string? message { get; set; }
                public string? code { get; set; }
            }
            """, WirePath);

        Assert.DoesNotContain(diagnostics, d => d.Id == "VAE007");
    }
}
