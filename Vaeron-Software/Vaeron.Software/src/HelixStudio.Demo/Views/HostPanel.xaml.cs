using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using HelixStudio.Contract;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>工具掛載這一頁</summary>
public partial class HostPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly SecretStore secrets = new();

    /// <summary>每一支組件檔建立過的那一份畫面；同一個檔只建立一次，之後開都掛同一份</summary>
    private readonly Dictionary<string, object> screens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>備好這一頁</summary>
    public HostPanel()
    {
        InitializeComponent();
    }

    /// <summary>開起一支工具：把它的組件載進來，建立它的畫面，掛在這一區</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Open(string toolNo)
    {
        // 這一支工具那一列：裝在哪個資料夾、載哪一支組件、哪一個型別
        var row = store.QueryShellItem(toolNo) ?? new();
        var home = $"{row.home}";
        var entry = Path.Combine(home, $"{row.entry}");

        // 上一次掛的先拿掉
        Mount.Children.Clear();

        // 載不起來的原因
        var faults = new List<string>();

        DataContext = new { Faults = faults };

        // 組件檔要在；不在就說出來，畫面停在這裡
        faults.AddRange(Enumerable
            .Repeat($"找不到 {entry}。這支工具可能沒有裝完整，或是被手動刪掉了。再執行一次 HelixStudio Demo 安裝檔。", 1)
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

        // 那個畫面：這個組件檔建立過就掛回同一份（它開出來的視窗、正在做的事都還在，不會再多一份）；沒建立過才建立
        var kept = screens.Where(one => string.Equals(one.Key, entry, StringComparison.OrdinalIgnoreCase)).Select(one => one.Value).ToList();
        var made = kept.Count > 0
            ? kept
            : types.Where(one => one is not null).Select(one => Activator.CreateInstance(one!)!).ToList();

        made.ForEach(one => screens[entry] = one);

        faults.AddRange(made
            .Where(one => one is not UIElement)
            .Select(one => $"{row._class} 不是一個畫面（它是 {one!.GetType().Name}），掛不上來。"));

        // 是一個畫面才掛上去；掛之前把保險箱交給它
        made
            .OfType<ITool>()
            .ToList()
            .ForEach(one => one.Secrets = secrets.For(toolNo));

        // 要用資料庫的工具，把平台的連線交給它
        made
            .OfType<IData>()
            .ToList()
            .ForEach(one => one.Connection = secrets.Secret("HelixStudio"));

        made
            .OfType<UIElement>()
            .ToList()
            .ForEach(one => Mount.Children.Add(one));
    }
}
