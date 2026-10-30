using System.IO;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using HelixStudio.Contract;
using HelixStudio.Service;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>工具掛載這一頁</summary>
public partial class HostPanel : UserControl
{
    private readonly ShellService service = new();

    private readonly SecretStore secrets = new();

    /// <summary>每一支組件檔建立過的那一份畫面；同一個檔只建立一次，之後開都掛同一份</summary>
    private readonly Dictionary<string, object> screens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>每一支組件檔載不起來的原因</summary>
    private readonly Dictionary<string, List<string>> broken = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>工具要把自己那一頁叫到前面時，平台要做的事：主視窗叫回前面、開這一支（主視窗塞進來）</summary>
    public Action<string> Show { get; set; } = _ => { };

    /// <summary>備好這一頁</summary>
    public HostPanel()
    {
        InitializeComponent();
    }

    /// <summary>啟動一支工具（自動啟動、切成自動啟動）：把它的組件載進來，建立它的畫面在背景跑，不掛上來；建立過就是那一份</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Start(string toolNo)
    {
        // 這一支工具那一列：裝在哪個資料夾、載哪一支組件、哪一個型別
        var row = service.LoadTool(toolNo) ?? new();
        var entry = Path.Combine($"{row.home}", $"{row.entry}");

        // 建立過就不再建立（它開出來的視窗、正在做的事都還在，不會再多一份）
        if (screens.ContainsKey(entry)) return;

        // 載不起來的原因
        var faults = new List<string>();

        broken[entry] = faults;

        // 組件檔要在；不在就說出來
        faults.AddRange(Enumerable
            .Repeat($"找不到 {entry}。這支工具可能沒有裝完整，或是被手動刪掉了。重新匯入一次它的套件。", 1)
            .Where(_ => !File.Exists(entry)));

        // 載進來，找出它宣告的那個型別；同一個檔已經載過就用那一份（同名組件載兩份，WPF 找畫面資源會找錯份）
        var types = Enumerable
            .Repeat(entry, 1)
            .Where(File.Exists)
            .Select(one => AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .Where(loaded => !loaded.IsDynamic)
                .FirstOrDefault(loaded => string.Equals(loaded.Location, one, StringComparison.OrdinalIgnoreCase))
                ?? new AssemblyLoadContext($"{toolNo}", true).LoadFromAssemblyPath(one))
            .Select(one => one.GetType($"{row._class}"))
            .ToList();

        faults.AddRange(types
            .Where(one => one is null)
            .Select(_ => $"{row.entry} 裡找不到 {row._class}。這支工具的套件可能包錯了，回報給資訊部。"));

        // 建立它的畫面
        var made = types.Where(one => one is not null).Select(one => Activator.CreateInstance(one!)!).ToList();

        made.ForEach(one => screens[entry] = one);

        faults.AddRange(made
            .Where(one => one is not UIElement)
            .Select(one => $"{row._class} 不是一個畫面（它是 {one!.GetType().Name}），掛不上來。"));

        // 把保險箱交給它
        made
            .OfType<ITool>()
            .ToList()
            .ForEach(one => one.Secrets = secrets.For(toolNo));

        // 要用資料庫的工具，把平台的連線交給它
        made
            .OfType<IData>()
            .ToList()
            .ForEach(one => one.Connection = secrets.Secret("HelixStudio"));

        // 要把自己叫到前面的工具，交給它平台的那個動作
        made
            .OfType<IShow>()
            .ToList()
            .ForEach(one => one.Show = () => Show(toolNo));
    }

    /// <summary>開起一支工具（側欄、雙擊、開啟鈕）：啟動它（建立過就是那一份），掛在這一區；載不起來就把原因擺上去</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Open(string toolNo)
    {
        // 上一次掛的先拿掉
        Mount.Children.Clear();

        // 啟動它
        Start(toolNo);

        // 這一支的畫面與載不起來的原因
        var row = service.LoadTool(toolNo) ?? new();
        var entry = Path.Combine($"{row.home}", $"{row.entry}");

        DataContext = new { Faults = broken.GetValueOrDefault(entry, []) };

        screens.Where(one => string.Equals(one.Key, entry, StringComparison.OrdinalIgnoreCase))
            .Select(one => one.Value)
            .OfType<UIElement>()
            .ToList()
            .ForEach(one => Mount.Children.Add(one));
    }
}
