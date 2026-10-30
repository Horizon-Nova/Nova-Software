using System.IO;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HelixStudio.Contract;
using HelixStudio.Logic;
using HelixStudio.Service;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>設定這一頁</summary>
/// <remarks>區段、標籤、說明、選項的字寫在 XAML；這裡把值擺上去、照搜尋字收起不符合的列、把操作交給 SettingsService。</remarks>
public partial class SettingsPanel : UserControl
{
    private readonly SettingsService service = new();

    private readonly SecretStore secrets = new();

    /// <summary>選中的那一節；空字串是還沒選</summary>
    private string section = string.Empty;

    /// <summary>這一次攤出來的這一頁</summary>
    private SettingsLogic.SettingsView view = new();

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
    private void Find(object sender, TextChangedEventArgs e) =>
        Show();

    /// <summary>清除搜尋</summary>
    private void Clear(object sender, RoutedEventArgs e) =>
        Search.Text = string.Empty;

    /// <summary>展開或收起一個分類：分類鈕的 Uid 對到它底下那一疊</summary>
    private void Twist(object sender, RoutedEventArgs e)
    {
        var area = (Button)sender;
        var open = $"{area.Tag}" != "open";

        area.Tag = open ? "open" : "shut";
        Nav.Children.OfType<StackPanel>().Where(one => one.Uid == area.Uid).ToList()
            .ForEach(one => one.Visibility = open ? Visibility.Visible : Visibility.Collapsed);
    }

    /// <summary>點左邊的一節：工具的那一節把那支工具自己的設定畫面掛上來；其他節整頁捲到它那一段</summary>
    private void PickSection(object sender, RoutedEventArgs e)
    {
        section = $"{((Button)sender).Tag}";

        // 換節先把上一支工具的設定畫面拿掉
        ToolPane.Content = null;

        // 工具的那一節：載入它的組件，建立它宣告的設定畫面，交給它保險箱與資料庫連線
        new[] { section }
            .Where(one => one.StartsWith($"{SettingsLogic.Tools}:"))
            .Select(one => service.LoadToolSettings(one[(SettingsLogic.Tools.Length + 1)..]))
            .Where(row => row is not null && File.Exists(Path.Combine($"{row.home}", $"{row.entry}")))
            .ToList()
            .ForEach(row =>
            {
                // 同一個檔已經載過（開過這支工具）就用那一份；同名組件載兩份，WPF 找畫面資源會找錯份
                var entry = Path.Combine($"{row!.home}", $"{row.entry}");
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
        Page.UpdateLayout();
        Page.ScrollToVerticalOffset(Sections.Children
            .OfType<FrameworkElement>()
            .Where(one => $"{one.Tag}" == section && one.Visibility == Visibility.Visible)
            .Select(one => one.TranslatePoint(new Point(0, 0), Sections).Y)
            .DefaultIfEmpty(0)
            .First());
    }

    /// <summary>輸入工具金鑰：配得上這支工具就解開它的設定，照設定的分鐘數倒數</summary>
    /// <param name="sender">解鎖（Tag 是金鑰框）</param>
    /// <param name="e">這一次點擊</param>
    private void Unlock(object sender, RoutedEventArgs e)
    {
        var code = (PasswordBox)((FrameworkElement)sender).Tag;
        var toolNo = section[(SettingsLogic.Tools.Length + 1)..];
        var (fault, minutes) = service.LoadUnlocked(toolNo, code.Password);

        // 配不上：原因寫在框下面
        ((TextBlock)code.Tag).Text = fault;
        code.Clear();

        new[] { fault }.Where(one => one.Length == 0).ToList().ForEach(_ => until[toolNo] = DateTime.Now.AddMinutes(minutes));

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

    /// <summary>切一個開關（Tag 是設定代碼）：寫回去，當場照新的值套用</summary>
    private void Flip(object sender, RoutedEventArgs e)
    {
        var box = (CheckBox)sender;

        service.LoadChanged($"{box.Tag}", box.IsChecked == true, null);

        ((App)Application.Current).Apply();
        Show();
    }

    /// <summary>選一個選項（Tag 是設定代碼，選項的 Tag 是值）：跟現在的值不同才寫回去，重畫時不會又彈回來</summary>
    private void Choose(object sender, SelectionChangedEventArgs e)
    {
        var box = (ComboBox)sender;
        var key = $"{box.Tag}";

        new[] { box.SelectedValue }
            .OfType<int>()
            .Where(one => view.Values.TryGetValue(key, out var now) && now.Number != one)
            .ToList()
            .ForEach(one =>
            {
                service.LoadChanged(key, null, one);

                ((App)Application.Current).Apply();
                Show();
            });
    }

    /// <summary>把一項設定改回資訊部建議的值（Tag 是設定代碼）</summary>
    private void Reset(object sender, RoutedEventArgs e)
    {
        service.LoadReverted([$"{((Button)sender).Tag}"]);

        ((App)Application.Current).Apply();
        Show();
    }

    /// <summary>回復成資訊部建議設定：每一項都寫回建議值，這一頁跟著重畫</summary>
    private void Revert(object sender, RoutedEventArgs e)
    {
        service.LoadReverted([]);

        ((App)Application.Current).Apply();
        Show();

        ((MainWindow)Window.GetWindow(this)!).Note("「回復成資訊部建議設定」已送出", "info");
    }

    /// <summary>把這一頁重畫一次：值擺上去；照搜尋字收起不符合的列、節與左邊的節；工具設定照鎖著或解開決定能不能改；有工具解開就每秒再畫一次</summary>
    private void Show()
    {
        var now = DateTime.Now;
        var query = Search.Text.Trim();

        view = service.LoadSettings(section, until);
        DataContext = view;

        // 看的是一支工具：只出那支工具的設定畫面；其餘節收起
        Sections.Visibility = view.Here ? Visibility.Collapsed : Visibility.Visible;
        ToolPane.IsEnabled = view.Opens.Count > 0;
        tick.IsEnabled = until.Values.Any(one => one > now);

        // 每一列（Uid 是 row）：搜尋比對的字（Tag）裡有打的字才出現；一節裡一列都沒有就收起那一節
        var shown = 0;
        foreach (var block in Sections.Children.OfType<StackPanel>())
        {
            var rows = LogicalTreeHelper.GetChildren(block).OfType<FrameworkElement>().Where(one => one.Uid == "row").ToList();

            rows.ForEach(one => one.Visibility = $"{one.Tag}".Contains(query) ? Visibility.Visible : Visibility.Collapsed);
            block.Visibility = rows.Any(one => one.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            shown += rows.Count(one => one.Visibility == Visibility.Visible);
        }

        // 左邊：節跟著右邊那一段出現與否；選中的那一節（Uid 是 on）有淺底；分類底下一節都沒有就收起分類
        var visible = Sections.Children.OfType<StackPanel>().Where(one => one.Visibility == Visibility.Visible).Select(one => $"{one.Tag}").ToList();

        foreach (var stack in Nav.Children.OfType<StackPanel>().Where(one => one.Uid != "tools"))
        {
            stack.Children.OfType<Button>().ToList().ForEach(one =>
            {
                one.Visibility = visible.Contains($"{one.Tag}") ? Visibility.Visible : Visibility.Collapsed;
                one.Uid = $"{one.Tag}" == section ? "on" : string.Empty;
            });

            Nav.Children.OfType<Button>().Where(one => one.Uid == stack.Uid).ToList()
                .ForEach(one => one.Visibility = stack.Children.OfType<Button>().Any(two => two.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed);
        }

        // 「工具」底下：名稱有打的字的那幾支；一支都沒有就收起「工具」
        ToolNav.ItemsSource = view.Tools.Where(one => one.Text.Contains(query)).ToList();
        ToolsArea.Visibility = view.Tools.Any(one => one.Text.Contains(query)) ? Visibility.Visible : Visibility.Collapsed;

        ClearButton.Visibility = query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Count.Text = $"{shown} 項設定";
    }
}
