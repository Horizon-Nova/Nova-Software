using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace InSpecHookDLLHost;

public partial class MainWindow : Window
{
    private readonly InSpecController _controller = new();
    private readonly DispatcherTimer _liveReadTimer;   // GDI 即時讀畫面(進階,opt-in)
    private bool _isMainWindowHidden;
    private bool _headlessRunning;

    public MainWindow()
    {
        InitializeComponent();

        _liveReadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveReadTimer.Tick += LiveReadTimer_Tick;

        // Log 面板即時綁定(FR-8):AppLog 每寫一行就附到畫面。
        LogPathText.Text = AppLog.LogFilePath;
        AppLog.LineAppended += line => Dispatcher.BeginInvoke(() =>
        {
            LogTextBox.AppendText(line + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        });
    }

    // ── 狀態列(FR-1) ─────────────────────────────────────────
    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        StatusText.Text = "InSpec：掃描中…";
        var ok = await Task.Run(() => _controller.ScanAndAttach());
        ScanButton.IsEnabled = true;
        RefreshStatus(ok ? "已掃描" : "未找到（請先開啟 InSpec 或用「無畫面啟動」）");
    }

    private const string InSpecExePath = InSpecController.InSpecExeDefaultPath;

    private async void LaunchHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchHiddenButton.IsEnabled = false;
        StatusText.Text = "InSpec：無畫面啟動中…";
        var ok = await Task.Run(() => _controller.LaunchInSpecHidden(InSpecExePath));
        LaunchHiddenButton.IsEnabled = true;
        if (ok)
        {
            _isMainWindowHidden = true;
            HideWindowCheckBox.IsChecked = true;
        }
        RefreshStatus(ok ? "無畫面啟動" : "啟動或注入失敗");
    }

    private void RefreshStatus(string note)
    {
        var pid = _controller.ProcessId is int p ? $"PID {p}" : "未連線";
        var inj = _controller.IsInjected ? " · 已注入" : "";
        var hid = _isMainWindowHidden ? " · 隱藏中" : "";
        StatusText.Text = $"InSpec：{pid}{inj}{hid} · {note}";
        UpdateMutualExclusion();
    }

    // ── 主流程 ①選程式(FR-2) ────────────────────────────────
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "選擇搜尋目錄" };
        if (dialog.ShowDialog() == true) SearchDirTextBox.Text = dialog.FolderName;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchResultsListBox.Items.Clear();
        var dir = SearchDirTextBox.Text;
        var term = SearchTermTextBox.Text;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            AppLog.Warn("Search", $"目錄無效：{dir}");
            return;
        }
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(dir, "*.iwp", SearchOption.AllDirectories); }
        catch (IOException ex) { AppLog.Error("Search", ex); return; }
        catch (UnauthorizedAccessException ex) { AppLog.Error("Search", ex); return; }

        var n = 0;
        foreach (var path in files)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(term) || name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                SearchResultsListBox.Items.Add(new SearchResultItem(Path.GetFileName(path), path));
                n++;
            }
        }
        AppLog.Info("Search", $"dir={dir}, term=\"{term}\", 命中 {n} 筆");
    }

    // ── 主流程 ②執行 + ③結果(FR-3/4/5) ─────────────────────
    private async void RunHeadlessButton_Click(object sender, RoutedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not SearchResultItem item)
        {
            HeadlessNotice.Text = "請先在①選一個 .iwp。";
            return;
        }
        if (_controller.ProcessId is null)
        {
            HeadlessNotice.Text = "InSpec 尚未連線。請先「掃描」或「無畫面啟動」。";
            return;
        }

        _headlessRunning = true;
        UpdateMutualExclusion();
        HeadlessNotice.Text = "準備 AutoExport 工作副本並以 iscmd 執行…";
        var promptValue = PromptValueTextBox.Text;
        var wasHidden = _isMainWindowHidden;

        var result = await Task.Run(() =>
        {
            var (workIwp, exportTxt) = _controller.PrepareAutoExportCopy(item.FullPath);
            var ran = _controller.RunViaIscmdAsync(workIwp, noWait: true).GetAwaiter().GetResult();
            if (!ran) return (ran, exportTxt, (IReadOnlyList<string>)Array.Empty<string>(), false);

            bool filled = false;
            if (_controller.IsInjected && !string.IsNullOrWhiteSpace(promptValue))
            {
                Thread.Sleep(800);
                filled = _controller.FillAndConfirm(promptValue);
            }
            Thread.Sleep(1000); // 等 AutoExport 寫檔
            var lines = _controller.ReadExportFile(exportTxt);
            // iscmd /run 開檔會讓 InSpec self-activate 顯示;不靠讀畫面,讀完若原本要隱藏就補隱藏。
            if (wasHidden && _controller.IsInjected) _controller.HideMainWindow();
            return (ran, exportTxt, lines, filled);
        });

        _headlessRunning = false;
        UpdateMutualExclusion();

        var (okRun, path, lines2, promptFilled) = result;
        if (!okRun) { HeadlessNotice.Text = "iscmd 回傳失敗（InSpec 沒在跑或參數不認得）。見 Log。"; return; }

        PopulateResults(lines2);
        HeadlessNotice.Text = lines2.Count == 0
            ? $"iscmd /run 成功{(promptFilled ? "、提示已填值" : "")}。匯出檔為空（此檔無量測元素/無真機台時屬正常,機制已運作）。檔：{path}"
            : $"iscmd /run 成功{(promptFilled ? "、提示已填值" : "")}。讀到 {lines2.Count} 行。檔：{path}";
    }

    private void PopulateResults(IReadOnlyList<string> lines)
    {
        ResultsDataGrid.Items.Clear();
        foreach (var line in lines)
        {
            var c = line.Split('\t');
            ResultsDataGrid.Items.Add(new ExportRow(
                c.ElementAtOrDefault(0) ?? "", c.ElementAtOrDefault(1) ?? "",
                c.ElementAtOrDefault(2) ?? "", c.ElementAtOrDefault(3) ?? ""));
        }
    }

    // ── 進階：隱藏 / 即時讀畫面 / 前端注入 / legacy(FR-6/7；C 約束) ──
    private async void HideWindowCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        var ok = await Task.Run(() => _controller.HideMainWindow());
        _isMainWindowHidden = ok;
        RefreshStatus(_isMainWindowHidden ? "隱藏" : "隱藏失敗");
    }

    private async void HideWindowCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => _controller.ShowMainWindow());
        _isMainWindowHidden = false;
        RefreshStatus("顯示");
    }

    private void LiveReadCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_isMainWindowHidden)
        {
            LiveReadCheckBox.IsChecked = false; // C-4：隱藏時不能讀畫面
            LiveSummaryText.Text = "即時摘要：無法啟用——視窗隱藏中（GDI 讀畫面需視窗可見）。";
            return;
        }
        _liveReadTimer.Start();
        LiveSummaryText.Text = "即時摘要：讀取中…";
        UpdateMutualExclusion();
    }

    private void LiveReadCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        _liveReadTimer.Stop();
        LiveSummaryText.Text = "即時摘要：（未啟用）";
        UpdateMutualExclusion();
    }

    private async void LiveReadTimer_Tick(object? sender, EventArgs e)
    {
        if (_headlessRunning || _isMainWindowHidden) return; // C-3/C-4
        var lines = await Task.Run(() => _controller.CaptureSummary(out var l) ? l : null);
        if (lines is null || lines.Count == 0) return;
        var prompt = lines.Count > 1 ? lines[1].Text : "";
        var value = lines.Count > 3 ? lines[3].Text : lines[^1].Text;
        LiveSummaryText.Text = $"即時摘要：提示 {prompt} · 輸入 {value}";
    }

    private async void LiveEditButtonCheckBox_Checked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.EnableLiveEditButton());

    private async void LiveEditButtonCheckBox_Unchecked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.DisableLiveEditButton());

    private async void XyzEditCheckBox_Checked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.EnableXyzEdit());

    private async void XyzEditCheckBox_Unchecked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.DisableXyzEdit());

    private async void ReloadButtonCheckBox_Checked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.EnableReloadButton());

    private async void ReloadButtonCheckBox_Unchecked(object sender, RoutedEventArgs e)
        => await Task.Run(() => _controller.DisableReloadButton());

    private async void TriggerReloadButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.IsEnabled = false;
        await Task.Run(() => _controller.TriggerReload());
        button.IsEnabled = true;
    }

    // ── InSpec 摘要面板前端改造（主流程，必要能力，非進階選配）───────
    // 改的是 InSpec 自己顯示的畫面：完成小圖示換樣式／顏色，輸入值變成可以直接點進去編輯的
    // 真正欄位——不是我方軟體另開一個控制台代管 InSpec 的 UI，這裡只是開關與樣式設定入口。
    private async void OverlayEnableCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_controller.ProcessId is null)
        {
            OverlayNotice.Text = "InSpec 尚未連線。請先「掃描」或「無畫面啟動」。";
            OverlayEnableCheckBox.IsChecked = false;
            return;
        }
        OverlayEnableCheckBox.IsEnabled = false;
        var ok = await Task.Run(() => _controller.EnableOverlay());
        OverlayEnableCheckBox.IsEnabled = true;
        OverlayNotice.Text = ok
            ? "已啟用。InSpec 摘要面板上：完成小圖示換成下方選定樣式，輸入值那一行變成可直接點選編輯的欄位。"
            : "啟用失敗，見 Log。";
        if (!ok) OverlayEnableCheckBox.IsChecked = false;
    }

    private async void OverlayEnableCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        OverlayEnableCheckBox.IsEnabled = false;
        await Task.Run(() => _controller.DisableOverlay());
        OverlayEnableCheckBox.IsEnabled = true;
        OverlayNotice.Text = "已停用，InSpec 摘要面板恢復原樣。";
    }

    private async void OverlaySymbolCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var mode = (InSpecController.OverlaySymbol)OverlaySymbolCombo.SelectedIndex;
        OverlayCustomTextBox.Visibility = mode == InSpecController.OverlaySymbol.CustomText
            ? Visibility.Visible : Visibility.Collapsed;
        if (_controller.ProcessId is null) return;
        await Task.Run(() => _controller.SetOverlaySymbol(mode));
    }

    private async void OverlayCustomTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_controller.ProcessId is null) return;
        var text = OverlayCustomTextBox.Text;
        await Task.Run(() => _controller.SetOverlayCustomText(text));
    }

    private async void ColorGreen_Click(object sender, RoutedEventArgs e) => await ApplyOverlayColorAsync(22, 163, 74);
    private async void ColorRed_Click(object sender, RoutedEventArgs e) => await ApplyOverlayColorAsync(220, 38, 38);
    private async void ColorYellow_Click(object sender, RoutedEventArgs e) => await ApplyOverlayColorAsync(217, 160, 10);
    private async void ColorBlue_Click(object sender, RoutedEventArgs e) => await ApplyOverlayColorAsync(37, 99, 235);

    private async Task ApplyOverlayColorAsync(byte r, byte g, byte b)
    {
        if (_controller.ProcessId is null) return;
        await Task.Run(() => _controller.SetOverlayColor(r, g, b));
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteButton.IsEnabled = false;
        await Task.Run(() => _controller.TriggerExecute());
        ExecuteButton.IsEnabled = true;
    }

    private async void OpenInInSpecButton_Click(object sender, RoutedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not SearchResultItem item) return;
        OpenInInSpecButton.IsEnabled = false;
        await _controller.OpenFileInInSpecAsync(item.FullPath);
        OpenInInSpecButton.IsEnabled = true;
    }

    // ── Log(FR-8) ───────────────────────────────────────────
    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{AppLog.LogFilePath}\""); }
        catch (Exception ex) { AppLog.Error("OpenLog", ex); }
    }

    // ── C 干擾約束 → UI 強制(不只文件) ───────────────────────
    private void UpdateMutualExclusion()
    {
        // C-1：iscmd 執行中鎖 legacy 手動驅動;反之
        var legacyOk = !_headlessRunning;
        ExecuteButton.IsEnabled = legacyOk;
        OpenInInSpecButton.IsEnabled = legacyOk;
        RunHeadlessButton.IsEnabled = !_headlessRunning;

        // C-2/C-4：隱藏中不能即時讀畫面;即時讀畫面開啟中不能隱藏
        var liveReadOn = LiveReadCheckBox.IsChecked == true;
        LiveReadCheckBox.IsEnabled = !_isMainWindowHidden && !_headlessRunning;
        HideWindowCheckBox.IsEnabled = !liveReadOn && !_headlessRunning;
    }

    private sealed record SearchResultItem(string DisplayName, string FullPath)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record ExportRow(string C1, string C2, string C3, string C4);
}
