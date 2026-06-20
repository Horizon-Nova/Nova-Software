using System.Windows;
using System.Windows.Media;

namespace UsbPowerGuard;

public partial class MainWindow : Window
{
    private readonly PowerService _power = new();

    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xf5, 0x9e, 0x0b));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x5a, 0x64, 0x78));

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        ApplySleepSummary(_power.IsModernStandbyEnabled());

        var (ac, dc) = _power.GetUsbSelectiveSuspend();
        ApplyState(AcState, ac);
        ApplyState(DcState, dc);
    }

    private void ApplySleepSummary(bool modernStandby)
    {
        if (modernStandby)
        {
            SleepHeadline.Text = "待機方式：現代待機 (S0)";
            SleepDetail.Text = "螢幕關閉後系統仍在背景運作，閒置時 USB 裝置最容易被省電暫停。"
                + "停用上方的「USB 選擇性暫停」即可避免攝影機被關閉。";
        }
        else
        {
            SleepHeadline.Text = "待機方式：傳統睡眠 (S3)";
            SleepDetail.Text = "此電腦未使用現代待機。但閒置時 USB 裝置仍可能被「選擇性暫停」省電，"
                + "停用上方的「USB 選擇性暫停」即可避免攝影機被關閉。";
        }
    }

    private static void ApplyState(System.Windows.Documents.Run run, int value)
    {
        switch (value)
        {
            case 0:
                run.Text = " 已停用（裝置不會被暫停）";
                run.Foreground = Green;
                break;
            case 1:
                run.Text = " 已啟用（閒置時會暫停裝置）";
                run.Foreground = Amber;
                break;
            default:
                run.Text = " 讀取失敗";
                run.Foreground = Muted;
                break;
        }
    }

    private void Disable_Click(object sender, RoutedEventArgs e) => Apply(0, "已停用 USB 選擇性暫停。");

    private void Enable_Click(object sender, RoutedEventArgs e) => Apply(1, "已還原 USB 選擇性暫停為啟用。");

    private void Apply(int value, string successMessage)
    {
        var applied = _power.SetUsbSelectiveSuspend(value);
        StatusText.Text = applied ? successMessage : "已取消（未變更）。";
        if (applied) Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        StatusText.Text = "已重新整理。";
    }
}
