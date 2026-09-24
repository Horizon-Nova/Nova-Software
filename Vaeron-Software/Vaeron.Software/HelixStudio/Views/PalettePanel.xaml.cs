using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>指令面板這一頁</summary>
public partial class PalettePanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private string query = string.Empty;

    /// <summary>有人要關掉面板</summary>
    public event EventHandler? Closed;

    /// <summary>有人選了某一列</summary>
    public event EventHandler<PaletteLogic.PaletteItem>? Picked;

    /// <summary>備好這一頁</summary>
    public PalettePanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();
    }

    /// <summary>打開指令面板：每次都從空的關鍵字開始</summary>
    public void Open()
    {
        query = string.Empty;
        Search.Text = string.Empty;

        Show();

        Search.Focus();
    }

    /// <summary>面板一出現就把游標放進搜尋框</summary>
    private void TakeFocus(object sender, RoutedEventArgs e) =>
        ((TextBox)sender).Focus();

    /// <summary>點面板以外的地方就關掉</summary>
    private void Dismiss(object sender, MouseButtonEventArgs e) =>
        new[] { e.OriginalSource }
            .Where(one => one == sender)
            .ToList()
            .ForEach(_ => Closed?.Invoke(this, EventArgs.Empty));

    /// <summary>按 Esc：關掉面板</summary>
    private void Escape(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Escape)
            .ToList()
            .ForEach(_ =>
            {
                e.Handled = true;

                Closed?.Invoke(this, EventArgs.Empty);
            });

    /// <summary>打字搜尋</summary>
    private void Find(object sender, TextChangedEventArgs e)
    {
        query = Search.Text;

        Show();
    }

    /// <summary>選一列：交給外面去做，面板自己收起來</summary>
    private void Pick(object sender, RoutedEventArgs e)
    {
        var item = (PaletteLogic.PaletteItem)((Button)sender).DataContext;

        Closed?.Invoke(this, EventArgs.Empty);
        Picked?.Invoke(this, item);
    }

    /// <summary>把這一頁重畫一次</summary>
    private void Show() =>
        DataContext = PaletteLogic.Palette(store.QueryShells(), icons, query);
}
