using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HelixStudio.Logic;
using HelixStudio.Service;
using HelixStudio.Store;
using HelixStudio.Views;
using Microsoft.Win32;

namespace HelixStudio;

/// <summary>這個應用程式</summary>
public partial class App : Application
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, double> baseline = [];

    /// <summary>這台電腦這個帳號只開一個平台：兩個同時開，工具會各做一次同一件事（例如同一筆 InSpec 結果記兩次）</summary>
    private static readonly Mutex Only = new(true, @"Local\HelixStudio", out First);

    /// <summary>這一個是不是第一個開起來的</summary>
    private static readonly bool First;

    /// <summary>開起來：把顏色放進資源，照設定套用一次，再看這台電腦有沒有授權</summary>
    /// <param name="e">啟動參數</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 換一個權限重開（例如程式監控的「以系統管理員重新開啟」）：命令列帶著舊的那一份的 PID，等它關掉、接手同一把鎖
        var after = e.Args.SkipWhile(one => one != "--after").Skip(1).Select(one => int.TryParse(one, out var pid) ? pid : 0).FirstOrDefault();
        var first = First;

        // 等舊的那一份關掉（最多 15 秒），接手「只開一個」的那把鎖；舊的沒放開就結束，鎖一樣交到這一份手上
        if (!first && after > 0)
        {
            try
            {
                Process.GetProcessById(after).WaitForExit(15000);
            }
            catch (ArgumentException)
            {
                // 已經關掉了
            }

            try
            {
                first = Only.WaitOne(5000);
            }
            catch (AbandonedMutexException)
            {
                first = true;
            }
        }

        // 已經開著一個：把那一個叫到前面，這一個不開
        if (!first)
        {
            Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .Where(one => one.Id != Environment.ProcessId && one.MainWindowHandle != IntPtr.Zero)
                .ToList()
                .ForEach(one =>
                {
                    ShowWindow(one.MainWindowHandle, Restore);
                    SetForegroundWindow(one.MainWindowHandle);
                });

            Shutdown();
            return;
        }

        // 這台電腦的資料庫連不上：照實說、說怎麼修，不往下開（錯誤也記不進 event_log）
        if (!store.QueryReachable())
        {
            MessageBox.Show(
                "連不上這台電腦的 HelixStudio 資料庫：資料庫服務沒有在執行，或被移除了。\n\n再執行一次 HelixStudio 安裝檔，它會把資料庫服務修好；資料都還在。",
                "HelixStudio", MessageBoxButton.OK, MessageBoxImage.Warning);

            Shutdown();
            return;
        }

        // 沒人接住的錯誤：畫面執行緒的由 Fail 接；背景執行緒與沒人等的工作也一樣記進 event_log
        AppDomain.CurrentDomain.UnhandledException += (_, one) => Logged((Exception)one.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, one) =>
        {
            Logged(one.Exception);
            one.SetObserved();
        };

        // 套件庫資料夾還沒有值：照這台電腦這個帳號的位置填上，種子資料不寫任何一台電腦的路徑
        new SettingsService().LoadFilled("source", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HelixStudio", "library"));

        Apply();

        base.OnStartup(e);

        // 簽章驗得過、兩邊都有的機器識別全部對得上、還沒過到期日（當天仍可用），就直接進平台；否則先開授權畫面
        var pass = new LicenceService().LoadHeld();

        Enumerable.Repeat(0, 1).Where(_ => pass).ToList().ForEach(_ => new MainWindow().Show());
        Enumerable.Repeat(0, 1).Where(_ => !pass).ToList().ForEach(_ => new LicenceWindow().Show());
    }

    /// <summary>照設定把畫面與這台裝置擺成該有的樣子</summary>
    /// <remarks>啟動時做一次；使用者改了設定也做一次。不輪詢，只在真的變了的那一刻做。</remarks>
    public void Apply()
    {
        var ink = new BrushConverter();

        // 這一次的設定：改過之後再叫一次就會拿到新的值
        var setup = new SettingsService().LoadApplied();

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

        // 按鈕與輸入框的高度：比框裡那一行字大一階（內文行高 × 1.25），文字放大時跟著放大；大按鈕模式再墊到它的高度
        Resources["ControlHeight"] = Math.Max(setup.Height, Math.Round((double)Resources["TextLine"] * Step));

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

        // 記進 event_log
        Logged(e.Exception);

        e.Handled = true;

        // 一個視窗都還沒開起來（開到一半就出錯）：沒有地方掛，照實說出來、關掉，不留一個看不到的程式在背景
        if (Windows.Count == 0)
        {
            MessageBox.Show($"HelixStudio 開到一半出錯，沒有開起來：\n\n{e.Exception.Message}", "HelixStudio", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
        }
    }

    /// <summary>把一次錯誤記進 event_log：哪一支方法出錯、錯誤訊息、呼叫堆疊、誰</summary>
    /// <param name="error">這一次錯誤</param>
    private void Logged(Exception error)
    {
        // 資料庫連不上時記不進去：畫面上已經掛了那一條，不再讓記錄本身把 HelixStudio 關掉
        try
        {
            new LogService().LoadFailed(error);
        }
        catch (Exception unsaved) when (unsaved is System.Data.Common.DbException or InvalidOperationException)
        {
            Dispatcher.BeginInvoke(() => Windows.OfType<MainWindow>().ToList().ForEach(one => one.Fail(unsaved.Message)));
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    #region 對照

    /// <summary>ShowWindow 的還原：最小化的叫回原本大小</summary>
    private const int Restore = 9;

    /// <summary>骨架光澤掃過的寬度</summary>
    private const double Sweep = 240;

    /// <summary>一階：框比裡面那一行字大多少</summary>
    private const double Step = 1.25;

    /// <summary>會跟著文字大小一起放大的每一個代幣</summary>
    private static IReadOnlyCollection<string> Sized { get; } =
        ["Text", "Title", "Special", "TextLine", "TitleLine", "SpecialLine"];

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
