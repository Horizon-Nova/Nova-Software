using Vaeron.HelixStudio.Contract;

namespace SampleTool;

/// <summary>最小的工具。交付畫面描述，不建立任何畫面元件。</summary>
public sealed class Tool : ITool
{
    private static readonly IReadOnlyDictionary<string, Func<Screen>> Screens =
        new Dictionary<string, Func<Screen>>(StringComparer.Ordinal)
        {
            ["start"] = Start,
            ["again"] = Again
        };

    public string Name => "示範工具";

    public string About => "最小的工具，只有一個畫面。";

    public string ModuleId => "sample-tool";

    public Screen Show(string action) => Screens.GetValueOrDefault(action, Start)();

    private static Screen Start() => new("示範工具", [
        new Element("text", "這一頁由載體畫，工具只給描述。", ""),
        new Element("text", "工具的程式碼跑在自己的行程裡。", ""),
        new Element("button", "換一句話", "again")
    ]);

    private static Screen Again() => new("示範工具", [
        new Element("text", "這是換過的那一句。", ""),
        new Element("button", "換回去", "start")
    ]);
}
