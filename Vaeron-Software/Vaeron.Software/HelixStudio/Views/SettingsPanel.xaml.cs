using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>設定這一頁</summary>
public partial class SettingsPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private readonly List<string> opened = ["top", "device", "work", "support"];

    private string section = "common";

    private string query = string.Empty;

    /// <summary>有人要關掉設定</summary>
    public event EventHandler? Closed;

    /// <summary>備好這一頁</summary>
    public SettingsPanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();
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

    /// <summary>點左邊的一節</summary>
    private void PickSection(object sender, RoutedEventArgs e)
    {
        section = ((SettingsLogic.NavItem)((Button)sender).DataContext).Key;

        Show();
    }

    /// <summary>切一個開關：畫面上的值和資料裡的值不同才寫回去，重畫時不會又彈回來</summary>
    private void Flip(object sender, RoutedEventArgs e) =>
        new[] { ((CheckBox)sender).IsChecked == true }
            .Where(one => one != ((SettingsLogic.ToggleItem)((CheckBox)sender).DataContext).On)
            .ToList()
            .ForEach(one =>
            {
                store.SaveSetting(((SettingsLogic.ToggleItem)((CheckBox)sender).DataContext).Key, Truth[one]);

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
                store.SaveSetting(((SettingsLogic.ChoiceItem)((ComboBox)sender).DataContext).Key, one);

                Applied();
            });

    /// <summary>把一項設定改回資訊部建議的值</summary>
    private void Reset(object sender, RoutedEventArgs e)
    {
        var back = (SettingsLogic.ResetItem)((Button)sender).DataContext;

        store.SaveSetting(back.Key, back.Fallback);

        Applied();
    }

    /// <summary>按一顆設定裡的按鈕</summary>
    private void Act(object sender, RoutedEventArgs e)
    {
        var act = (SettingsLogic.ActionItem)((Button)sender).DataContext;

        // 檢查更新：把工具清單重讀一次
        new[] { act.Label }.Where(one => one == Check).ToList()
            .ForEach(_ => ((MainWindow)Window.GetWindow(this)!).Reread());

        // 清除本機問答紀錄
        new[] { act.Label }.Where(one => one == Wipe).ToList().ForEach(_ => store.DropMessages());

        // 回復成資訊部建議設定：每一項都寫回建議值，這一頁跟著重畫
        new[] { act.Label }.Where(one => one == Revert).ToList()
            .ForEach(_ =>
            {
                store.ResetSettings();

                Applied();
            });

        ((MainWindow)Window.GetWindow(this)!).Note(act.Said, "info", []);
    }

    /// <summary>設定改過了：當場照新的值套用一次，再把這一頁重畫</summary>
    private void Applied()
    {
        ((App)Application.Current).Apply();

        Show();
    }

    /// <summary>把這一頁重畫一次</summary>
    private void Show() =>
        DataContext = SettingsLogic.Settings(store.QuerySettings(), icons, section, opened, query);

    #region 對照

    /// <summary>開關寫回去的時候是哪兩個字</summary>
    private static IReadOnlyDictionary<bool, string> Truth { get; } = new Dictionary<bool, string>
    {
        [true] = "true",
        [false] = "false",
    };

    /// <summary>檢查更新那顆按鈕上的字</summary>
    private const string Check = "檢查更新";

    /// <summary>清問答紀錄那顆按鈕上的字</summary>
    private const string Wipe = "清除";

    /// <summary>回復預設那顆按鈕上的字</summary>
    private const string Revert = "回復預設";

    #endregion
}
