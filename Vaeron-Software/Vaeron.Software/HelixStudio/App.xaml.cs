using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HelixStudio.Logic;
using HelixStudio.Store;
using HelixStudio.Views;
using Microsoft.Win32;

namespace HelixStudio;

/// <summary>這個應用程式</summary>
public partial class App : Application
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, double> baseline = [];

    /// <summary>開起來：把顏色放進資源，照設定套用一次，再看這台電腦有沒有授權</summary>
    /// <param name="e">啟動參數</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        Apply();

        base.OnStartup(e);

        // 簽章驗得過、兩邊都有的機器識別全部對得上，就直接進平台；否則先開授權畫面
        var paper = new LicenceStore().Licence();
        var pass = new[] { paper.Sealed }.Where(_ => paper.Both > 0 && paper.Same == paper.Both).DefaultIfEmpty(0).First();

        Enumerable.Repeat(0, 1).Where(_ => pass > 0).ToList().ForEach(_ => new MainWindow().Show());
        Enumerable.Repeat(0, 1).Where(_ => pass == 0).ToList().ForEach(_ => new LicenceWindow().Show());
    }

    /// <summary>照設定把畫面與這台裝置擺成該有的樣子</summary>
    /// <remarks>啟動時做一次；使用者改了設定也做一次。不輪詢，只在真的變了的那一刻做。</remarks>
    public void Apply()
    {
        var ink = new BrushConverter();

        // 這一次的設定：改過之後再叫一次就會拿到新的值
        var setup = SettingsLogic.Applied(store.QuerySettings());

        ShellLogic.Adopt(setup);

        // 主色九階與這一套顏色，畫面上每一處都從這裡取
        ShellLogic.Brand.ToList().ForEach(one => Resources[$"Brand{one.Key}"] = ink.ConvertFromString(one.Value));
        ShellLogic.Ink.ToList().ForEach(one => Resources[one.Key] = ink.ConvertFromString(one.Value));

        // 高對比：文字與框線換成對比更高的那一組；關掉時上面那一行已經寫回原本的值
        Enumerable.Repeat(Contrast, 1).Where(_ => setup.Contrast).ToList()
            .ForEach(one => one.ToList().ForEach(two => Resources[two.Key] = ink.ConvertFromString(two.Value)));

        // 骨架條的光澤跟著這一套顏色走
        Resources["Shine"] = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(Sweep, 0),
            MappingMode = BrushMappingMode.Absolute,
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            [
                new GradientStop((Color)ColorConverter.ConvertFromString(ShellLogic.Ink["ShineDim"]), 0.25),
                new GradientStop((Color)ColorConverter.ConvertFromString(ShellLogic.Ink["ShineLit"]), 0.5),
                new GradientStop((Color)ColorConverter.ConvertFromString(ShellLogic.Ink["ShineDim"]), 0.75),
            ],
        };

        // 字級四階原本多大，第一次套用時記下來，之後每次都從原本的大小乘倍率，不會愈套愈大
        Sized.Where(one => !baseline.ContainsKey(one)).ToList()
            .ForEach(one => baseline[one] = (double)Resources[one]);

        Sized.ToList().ForEach(one => Resources[one] = baseline[one] * setup.Scale);

        // 大按鈕模式：按鈕與輸入框的高度
        Resources["ControlHeight"] = setup.Height;
        Resources["SmallHeight"] = setup.Height - Small;

        // 使用中保持螢幕不休眠；關掉就把那個要求收回來
        SetThreadExecutionState(Continuous | Awake[setup.Awake]);

        // 開機時自動啟動：登錄檔那一項跟著設定走
        var key = Registry.CurrentUser.OpenSubKey(Run, true);

        new[] { setup.Boot }.Where(one => one).ToList()
            .ForEach(_ => key?.SetValue(Me, Environment.ProcessPath ?? string.Empty));
        new[] { setup.Boot }.Where(one => !one).ToList()
            .ForEach(_ => key?.DeleteValue(Me, false));
    }

    /// <summary>接住沒人處理的失誤：程式不終止，把發生的事寫在畫面上並給一條出路</summary>
    /// <param name="sender">送出這件事的來源</param>
    /// <param name="e">這一次失誤</param>
    private void Fail(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 視窗還在就把這一條掛上去；視窗還沒開起來就沒有地方掛
        Windows.OfType<MainWindow>().ToList().ForEach(one => one.Fail(e.Exception.Message));

        e.Handled = true;
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    #region 對照

    /// <summary>骨架光澤掃過的寬度</summary>
    private const double Sweep = 240;

    /// <summary>小一號的控制項比一般矮多少：原本是 32 與 28</summary>
    private const double Small = 4;

    /// <summary>會跟著文字大小一起放大的每一個代幣</summary>
    private static IReadOnlyCollection<string> Sized { get; } =
        ["Display", "Heading", "Body", "Caption", "DisplayLine", "HeadingLine", "BodyLine", "CaptionLine"];

    /// <summary>高對比時換掉的那幾個顏色</summary>
    private static IReadOnlyDictionary<string, string> Contrast { get; } = new Dictionary<string, string>
    {
        ["TextSecondary"] = "#1b1b1b",
        ["TextMuted"] = "#2b2b2b",
        ["TextFaint"] = "#3b3b3b",
        ["HairLine"] = "#66000000",
        ["Divider"] = "#80000000",
    };

    /// <summary>開機自動啟動寫在登錄檔的哪裡</summary>
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>登錄檔裡那一項叫什麼</summary>
    private const string Me = "HelixStudio";

    /// <summary>一直維持這個狀態</summary>
    private const uint Continuous = 0x80000000;

    /// <summary>要不要留著螢幕：不要時就沒有額外的旗標</summary>
    private static IReadOnlyDictionary<bool, uint> Awake { get; } = new Dictionary<bool, uint>
    {
        [true] = 0x00000002,
        [false] = 0x00000000,
    };

    #endregion
}
