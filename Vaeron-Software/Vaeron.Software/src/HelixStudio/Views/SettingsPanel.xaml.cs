using System.IO;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HelixStudio.Contract;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>設定這一頁</summary>
public partial class SettingsPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly SecretStore secrets = new();

    private readonly Dictionary<string, List<string>> icons;

    private readonly List<string> opened = ["top", "device", "work", SettingsLogic.Tools, "support"];

    /// <summary>選中的那一節；空字串就是第一節</summary>
    private string section = string.Empty;

    private string query = string.Empty;

    /// <summary>輸入工具金鑰解開的工具，各自到什麼時刻鎖回；只在記憶體裡，金鑰不留在這台電腦</summary>
    private readonly Dictionary<string, DateTime> until = [];

    /// <summary>有工具解開時每秒走一次：倒數，到了就鎖回</summary>
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>有人要關掉設定</summary>
    public event EventHandler? Closed;

    /// <summary>備好這一頁</summary>
    public SettingsPanel()
    {
        InitializeComponent();

        icons = store.QueryIconList();
        tick.Tick += (_, _) => Show();
    }

    /// <summary>打開設定</summary>
    public void Open() =>
        Show();

    /// <summary>關掉設定</summary>
    private void Close(object sender, RoutedEventArgs e) =>
        Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>點對話框以外的地方就關掉</summary>
    private void Dismiss(object sender, MouseButtonEventArgs e) =>
        new[] { e.OriginalSource }
            .Where(one => one == sender)
            .ToList()
            .ForEach(_ => Closed?.Invoke(this, EventArgs.Empty));

    /// <summary>按 Esc：關掉設定</summary>
    private void Escape(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Escape)
            .ToList()
            .ForEach(_ =>
            {
                e.Handled = true;

                Closed?.Invoke(this, EventArgs.Empty);
            });

    /// <summary>搜尋設定</summary>
    private void Find(object sender, TextChangedEventArgs e)
    {
        query = Search.Text;

        Show();
    }

    /// <summary>清除搜尋</summary>
    private void Clear(object sender, RoutedEventArgs e) =>
        Search.Text = string.Empty;

    /// <summary>展開或收起一個分類</summary>
    private void Twist(object sender, RoutedEventArgs e)
    {
        var key = ((SettingsLogic.NavItem)((Button)sender).DataContext).Key;

        // 本來展開的就收起來，本來收起來的就展開
        opened.Where(one => one == key).ToList().ForEach(one => opened.Remove(one));
        opened.AddRange(new[] { key }.Where(_ => !opened.Contains(key)));

        Show();
    }

    /// <summary>點左邊的一節：工具的那一節就把那支工具自己的設定畫面掛上來</summary>
    private void PickSection(object sender, RoutedEventArgs e)
    {
        section = ((SettingsLogic.NavItem)((Button)sender).DataContext).Key;

        // 換節先把上一支工具的設定畫面拿掉
        ToolPane.Content = null;

        // 工具的那一節：載入它的組件，建立它宣告的設定畫面，交給它保險箱與資料庫連線
        new[] { section }
            .Where(one => one.StartsWith($"{SettingsLogic.Tools}:"))
            .Select(one => store.QuerySettingItem(one[(SettingsLogic.Tools.Length + 1)..]) ?? new())
            .Where(row => File.Exists(Path.Combine($"{row.home}", $"{row.entry}")))
            .ToList()
            .ForEach(row =>
            {
                // 同一個檔已經載過（開過這支工具）就用那一份；同名組件載兩份，WPF 找畫面資源會找錯份
                var entry = Path.Combine($"{row.home}", $"{row.entry}");
                var assembly = AssemblyLoadContext.All
                    .SelectMany(context => context.Assemblies)
                    .Where(loaded => !loaded.IsDynamic)
                    .FirstOrDefault(loaded => string.Equals(loaded.Location, entry, StringComparison.OrdinalIgnoreCase))
                    ?? new AssemblyLoadContext($"{row.tool_no}", true).LoadFromAssemblyPath(entry);
                var made = Activator.CreateInstance(assembly.GetType($"{row.settings}")!);

                new[] { made }.OfType<ITool>().ToList().ForEach(one => one.Secrets = secrets.For($"{row.tool_no}"));
                new[] { made }.OfType<IData>().ToList().ForEach(one => one.Connection = secrets.Secret("HelixStudio"));

                ToolPane.Content = made;
            });

        Show();

        // 不是工具的那一節：整頁捲到它那一段的標題
        var at = ((SettingsLogic.SettingsView)DataContext).Sections.FindIndex(one => one.Key == section);

        Page.UpdateLayout();
        Page.ScrollToVerticalOffset(new[] { at }
            .Where(one => one >= 0)
            .Select(one => SectionList.ItemContainerGenerator.ContainerFromIndex(one) as FrameworkElement)
            .Where(one => one is not null)
            .Select(one => one!.TranslatePoint(new Point(0, 0), SectionList).Y)
            .DefaultIfEmpty(0)
            .First());
    }

    /// <summary>輸入工具金鑰：配得上這支工具就解開它的設定，照設定的分鐘數倒數，記一筆解鎖</summary>
    /// <param name="sender">解鎖（Tag 是金鑰框）</param>
    /// <param name="e">這一次點擊</param>
    private void Unlock(object sender, RoutedEventArgs e)
    {
        var code = (PasswordBox)((FrameworkElement)sender).Tag;
        var toolNo = section[(SettingsLogic.Tools.Length + 1)..];
        var fault = new LicenceStore().Fits(code.Password, toolNo);

        // 配不上：原因寫在框下面
        ((TextBlock)code.Tag).Text = fault;
        code.Clear();
        if (fault.Length > 0) return;

        // 配得上：解開幾分鐘照 vw_settings 的 unlock_minutes
        until[toolNo] = DateTime.Now.AddMinutes(store.QuerySettingItem(toolNo)?.unlock_minutes ?? 0);
        store.InsertEventLog("unlock", toolNo, $"{nameof(SettingsPanel)}.{nameof(Unlock)}", null, null);

        Show();
    }

    /// <summary>在金鑰框按 Enter 就是按解鎖</summary>
    /// <param name="sender">金鑰框</param>
    /// <param name="e">按下的鍵</param>
    private void UnlockKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var code = (PasswordBox)sender;
        var button = ((Grid)((Border)code.Parent).Parent).Children.OfType<Button>().First();

        Unlock(button, e);
    }

    /// <summary>鎖卡出來時游標放在金鑰框</summary>
    /// <param name="sender">金鑰框</param>
    /// <param name="e">出現這一次</param>
    private void FocusCode(object sender, RoutedEventArgs e) =>
        ((PasswordBox)sender).Focus();

    /// <summary>不等倒數，現在就鎖回</summary>
    /// <param name="sender">鎖回</param>
    /// <param name="e">這一次點擊</param>
    private void Relock(object sender, RoutedEventArgs e)
    {
        until.Remove(section[(SettingsLogic.Tools.Length + 1)..]);

        Show();
    }

    /// <summary>切一個開關：畫面上的值和資料裡的值不同才寫回去，重畫時不會又彈回來</summary>
    private void Flip(object sender, RoutedEventArgs e) =>
        new[] { ((CheckBox)sender).IsChecked == true }
            .Where(one => one != ((SettingsLogic.ToggleItem)((CheckBox)sender).DataContext).On)
            .ToList()
            .ForEach(one =>
            {
                store.UpdateSetting(((SettingsLogic.ToggleItem)((CheckBox)sender).DataContext).Key, Truth[one]);

                Applied();
            });

    /// <summary>選一個選項</summary>
    private void Choose(object sender, SelectionChangedEventArgs e) =>
        e.AddedItems
            .OfType<string>()
            .Where(one => one != ((SettingsLogic.ChoiceItem)((ComboBox)sender).DataContext).Value)
            .ToList()
            .ForEach(one =>
            {
                store.UpdateSetting(((SettingsLogic.ChoiceItem)((ComboBox)sender).DataContext).Key, one);

                Applied();
            });

    /// <summary>把一項設定改回資訊部建議的值</summary>
    private void Reset(object sender, RoutedEventArgs e)
    {
        var back = (SettingsLogic.ResetItem)((Button)sender).DataContext;

        store.UpdateSetting(back.Key, back.Fallback);

        Applied();
    }

    /// <summary>按一顆設定裡的按鈕</summary>
    private void Act(object sender, RoutedEventArgs e)
    {
        var act = (SettingsLogic.ActionItem)((Button)sender).DataContext;

        // 回復成資訊部建議設定：每一項都寫回建議值，這一頁跟著重畫
        new[] { act.Label }.Where(one => one == Revert).ToList()
            .ForEach(_ =>
            {
                store.UpdateSettingList();

                Applied();
            });

        ((MainWindow)Window.GetWindow(this)!).Note(act.Said, "info");
    }

    /// <summary>設定改過了：當場照新的值套用一次，再把這一頁重畫</summary>
    private void Applied()
    {
        ((App)Application.Current).Apply();

        Show();
    }

    /// <summary>把這一頁重畫一次：工具設定照鎖著或解開決定能不能改；有工具解開就每秒再畫一次</summary>
    private void Show()
    {
        var now = DateTime.Now;
        var view = SettingsLogic.Settings(store.QuerySettingList(), icons, section, opened, query, until, now);

        DataContext = view;
        section = view.Section;
        ToolPane.IsEnabled = view.Opens.Count > 0;
        tick.IsEnabled = until.Values.Any(one => one > now);
    }

    #region 對照

    /// <summary>開關寫回去的時候是哪兩個字</summary>
    private static IReadOnlyDictionary<bool, string> Truth { get; } = new Dictionary<bool, string>
    {
        [true] = "true",
        [false] = "false",
    };

    /// <summary>回復預設那顆按鈕上的字</summary>
    private const string Revert = "回復預設";

    #endregion
}
