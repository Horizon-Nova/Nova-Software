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

    private readonly Dictionary<string, List<string>> icons;

    private string key = string.Empty;

    /// <summary>有人要看這一支工具的資訊</summary>
    public event EventHandler<string>? Detailed;

    /// <summary>備好這一頁</summary>
    public HostPanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();
    }

    /// <summary>開起一支工具：把它的組件載進來，建立它的畫面，掛在這一區</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Open(string toolNo)
    {
        key = toolNo;

        // 這一支工具那一列：裝在哪個資料夾、載哪一支組件、哪一個型別
        var row = store.QueryShell(toolNo);
        var home = $"{row.home}";
        var entry = Path.Combine(home, $"{row.entry}");

        // 上一次掛的先拿掉
        Mount.Children.Clear();

        // 標頭這幾格
        var faults = new List<string>();

        DataContext = new
        {
            Name = $"{row.name}",
            Line = $"v{row.installed_version} · {row.vendor} · 掛載點 {home}",
            Paths = icons[$"{row.icon}"],
            Faults = faults,
        };

        // 組件檔要在；不在就說出來，畫面停在這裡
        faults.AddRange(Enumerable
            .Repeat($"找不到 {entry}。這支工具可能沒有裝完整，或是被手動刪掉了。重新匯入一次它的套件。", 1)
            .Where(_ => !File.Exists(entry)));

        // 載進來，建立它宣告的那個畫面
        var made = Enumerable
            .Repeat(entry, 1)
            .Where(File.Exists)
            .Select(one => Try(() => new AssemblyLoadContext($"{toolNo}", true).LoadFromAssemblyPath(one)))
            .Select(one => Try(() => one.Value?.GetType($"{row._class}")))
            .Select(one => Try(() => Activator.CreateInstance(one.Value!)))
            .ToList();

        faults.AddRange(made
            .Where(one => one.Value is null)
            .Select(one => $"{row.entry} 載入失敗：{one.Fault}"));

        faults.AddRange(made
            .Select(one => one.Value)
            .Where(one => one is not null)
            .Where(one => one is not UIElement)
            .Select(one => $"{row._class} 不是一個畫面（它是 {one!.GetType().Name}），掛不上來。"));

        // 是一個畫面才掛上去；掛之前把保險箱交給它
        made
            .Select(one => one.Value)
            .OfType<ITool>()
            .ToList()
            .ForEach(one => one.Secrets = secrets.For(toolNo));

        made
            .Select(one => one.Value)
            .OfType<UIElement>()
            .ToList()
            .ForEach(one => Mount.Children.Add(one));
    }

    /// <summary>重新載入這一支工具</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void Again(object sender, RoutedEventArgs e) =>
        Open(key);

    /// <summary>看這一支工具的資訊</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void Detail(object sender, RoutedEventArgs e) =>
        Detailed?.Invoke(this, key);

    #region 對照

    /// <summary>做一件可能失敗的事，把結果與失敗的原因一起帶回來</summary>
    /// <param name="work">要做的那一件事</param>
    private static Tried<T> Try<T>(Func<T> work)
    {
        try
        {
            return new Tried<T> { Value = work() };
        }
        catch (Exception fault)
        {
            return new Tried<T> { Fault = $"{fault.GetType().Name}：{fault.Message}" };
        }
    }

    #endregion

    #region 模型

    /// <summary>一件可能失敗的事做完之後</summary>
    private class Tried<T>
    {
        /// <summary>做成了的話拿到什麼</summary>
        public T? Value { get; set; }

        /// <summary>沒做成的話是什麼原因</summary>
        public string Fault { get; set; } = string.Empty;
    }

    #endregion
}
