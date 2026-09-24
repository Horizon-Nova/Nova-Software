using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using HelixStudio.Contract;

namespace ChecksumTool;

/// <summary>檔案校驗碼這一頁</summary>
public partial class Board : UserControl, ITool
{
    /// <summary>平台載入後塞進來的秘密存取；這支工具用不到，只為符合合約</summary>
    public ISecrets Secrets { get; set; } = null!;

    private readonly List<Line> lines = [];

    /// <summary>備好這一頁</summary>
    public Board()
    {
        InitializeComponent();

        Lines.ItemsSource = lines;
    }

    /// <summary>選一個檔案，算出它的校驗碼</summary>
    /// <param name="sender">按下去的那一塊</param>
    /// <param name="e">這一次點擊</param>
    private void Pick(object sender, MouseButtonEventArgs e)
    {
        // 讓使用者挑一個檔案；沒挑就是零筆
        var box = new OpenFileDialog { Title = "選一個檔案算校驗碼", Filter = "所有檔案|*.*" };

        new[] { box.ShowDialog() == true }.Where(one => one).ToList().ForEach(_ => Read(box.FileName));
    }

    /// <summary>檔案拖到這一塊上面</summary>
    /// <param name="sender">被拖過的那一塊</param>
    /// <param name="e">拖著什麼</param>
    private void DraggedOver(object sender, DragEventArgs e)
    {
        e.Effects = Effect[e.Data.GetDataPresent(DataFormats.FileDrop)];
        e.Handled = true;
    }

    /// <summary>檔案放下來，一個一個算</summary>
    /// <param name="sender">被放的那一塊</param>
    /// <param name="e">放下來的是什麼</param>
    private void Dropped(object sender, DragEventArgs e) =>
        new[] { e.Data.GetData(DataFormats.FileDrop) }
            .OfType<string[]>()
            .SelectMany(one => one)
            .Where(File.Exists)
            .ToList()
            .ForEach(Read);

    /// <summary>算一個檔案的校驗碼，擺到清單最上面</summary>
    /// <param name="path">那個檔案在哪裡</param>
    private void Read(string path)
    {
        // 讀這個檔案，算 SHA-256
        using var file = File.OpenRead(path);
        using var sha = SHA256.Create();

        var hash = Convert.ToHexString(sha.ComputeHash(file));

        // 檔案多大，照它的大小選單位
        var size = new FileInfo(path).Length;
        var unit = Units.Last(one => size >= one.Key);

        // 擺到最上面，剛算出來的在眼前
        lines.Insert(0, new Line
        {
            Name = Path.GetFileName(path),
            Size = $"{size / (double)unit.Key:0.#} {unit.Value} · {size:N0} 位元組",
            Hash = hash,
        });

        Lines.Items.Refresh();
        ZoneTitle.Text = "再拖一個檔案進來，或按一下選檔";
    }

    /// <summary>把校驗碼複製起來</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void Copy(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        Clipboard.SetText($"{button.Tag}");

        button.Content = "已複製";
    }

    #region 對照

    /// <summary>拖著的是不是檔案，決定游標長什麼樣</summary>
    private static IReadOnlyDictionary<bool, DragDropEffects> Effect { get; } = new Dictionary<bool, DragDropEffects>
    {
        [true] = DragDropEffects.Copy,
        [false] = DragDropEffects.None,
    };

    /// <summary>幾位元組起算哪一個單位</summary>
    private static IReadOnlyList<KeyValuePair<long, string>> Units { get; } =
    [
        new(1, "位元組"),
        new(1024, "KB"),
        new(1024 * 1024, "MB"),
        new(1024L * 1024 * 1024, "GB"),
    ];

    #endregion

    #region 模型

    /// <summary>算好的一筆</summary>
    public class Line
    {
        /// <summary>檔名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>檔案多大</summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>SHA-256</summary>
        public string Hash { get; set; } = string.Empty;
    }

    #endregion
}
