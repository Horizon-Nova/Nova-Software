using Vaeron.HelixStudio.Contract;

namespace AngryTool;

/// <summary>按鈕會擲例外的工具。用來看載體會不會跟著死。</summary>
public sealed class Tool : ITool
{
    private static readonly IReadOnlyDictionary<string, Func<Screen>> Screens =
        new Dictionary<string, Func<Screen>>(StringComparer.Ordinal)
        {
            ["start"] = Start,
            ["boom"] = Explode
        };

    public string Name => "會爆的工具";

    public string About => "按下按鈕就擲例外，用來看載體會不會跟著死。";

    public string ModuleId => "angry-tool";

    public Screen Show(string action) => Screens.GetValueOrDefault(action, Start)();

    private static Screen Start() => new("會爆的工具", [
        new Element("text", "按下去之後這支工具的行程會死。", ""),
        new Element("button", "按了會擲例外", "boom")
    ]);

    private static Screen Explode() => throw new InvalidOperationException("工具擲的例外");
}
