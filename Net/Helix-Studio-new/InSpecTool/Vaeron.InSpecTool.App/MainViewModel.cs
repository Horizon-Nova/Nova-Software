using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.InSpecTool.Interop;
using Vaeron.Kernel.Guarding;

namespace Vaeron.InSpecTool;

public enum ConnectionStatus { Idle, Scanning, Connected, LaunchFailed }

public sealed record IwpFile(string Name, string FullPath)
{
    public override string ToString() => Name;
}

public sealed record MeasurementResultRow(string Column1, string Column2, string Column3, string Column4);

/// <summary>核心量測流程：掃描/連接 InSpec、選 .iwp 透過官方 iscmd + AutoExport 無畫面執行並讀回結果、
/// 顯示/隱藏主視窗。不含 GDI 即時讀畫面、InSpec 前端樣式注入開關（v1 範圍之外）。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly InSpecController _controller = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private ConnectionStatus _status = ConnectionStatus.Idle;

    [ObservableProperty]
    private string _statusDetail = "尚未連接";

    [ObservableProperty]
    private string _searchDirectory = "";

    [ObservableProperty]
    private string _searchTerm = "";

    [ObservableProperty]
    private IwpFile? _selectedIwp;

    [ObservableProperty]
    private bool _isMainWindowVisible = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _runFeedback = "";

    public ObservableCollection<IwpFile> IwpFiles { get; } = [];
    public ObservableCollection<MeasurementResultRow> Results { get; } = [];

    public bool IsConnected => Status == ConnectionStatus.Connected;

    [RelayCommand]
    private async Task ScanAsync()
    {
        Status = ConnectionStatus.Scanning;
        StatusDetail = "掃描中…";
        await Guard.RunAsync(async () =>
        {
            var ok = await Task.Run(() => _controller.ScanAndAttach());
            ApplyAfterConnectAttempt(ok, "掃描");
        }, ApplyConnectFailure);
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        Status = ConnectionStatus.Scanning;
        StatusDetail = "啟動中…";
        await Guard.RunAsync(async () =>
        {
            var ok = await Task.Run(() => _controller.LaunchInSpecVisible(InSpecController.InSpecExeDefaultPath));
            ApplyAfterConnectAttempt(ok, "啟動");
        }, ApplyConnectFailure);
    }

    private void ApplyAfterConnectAttempt(bool ok, string action)
    {
        if (ok)
        {
            Status = ConnectionStatus.Connected;
            StatusDetail = $"PID {_controller.ProcessId}" + (_controller.IsInjected ? "・已注入" : "");
            IsMainWindowVisible = _controller.IsMainWindowVisible() ?? true;
        }
        else
        {
            Status = ConnectionStatus.LaunchFailed;
            StatusDetail = $"{action}失敗，請確認 InSpec 是否已安裝／執行中";
        }
    }

    private void ApplyConnectFailure(Exception ex)
    {
        Status = ConnectionStatus.LaunchFailed;
        StatusDetail = $"發生例外：{ex.Message}";
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "選擇搜尋目錄" };
        if (dialog.ShowDialog() == true) SearchDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void Search()
    {
        IwpFiles.Clear();
        var dir = SearchDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

        var term = SearchTerm;
        Guard.Run(() =>
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.iwp", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(term) || name.Contains(term, StringComparison.OrdinalIgnoreCase))
                    IwpFiles.Add(new IwpFile(Path.GetFileName(path), path));
            }
        }, ex => RunFeedback = $"[失敗] 搜尋失敗：{ex.Message}");
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (SelectedIwp is not { } iwp) return;
        if (!IsConnected) { RunFeedback = "[失敗] 尚未連接 InSpec"; return; }

        IsBusy = true;
        RunFeedback = "準備 AutoExport 工作副本並以 iscmd 執行…";
        Results.Clear();

        await Guard.RunAsync(async () =>
        {
            var paths = await Task.Run(() => _controller.PrepareAutoExportCopy(iwp.FullPath));
            var runResult = await _controller.RunViaIscmdAsync(new RunMeasurementRequest(paths.WorkIwp, NoWait: true));
            if (!runResult.IsSuccess || runResult.Value is not true)
            {
                RunFeedback = "[失敗] iscmd 執行失敗（InSpec 沒在跑或參數不認得）";
                return;
            }

            await Task.Delay(1000); // 等 AutoExport 寫檔
            var readResult = _controller.ReadExportFile(paths.ExportTxt);
            if (!readResult.IsSuccess || readResult.Value is null)
            {
                RunFeedback = $"[失敗] {readResult.Message}";
                return;
            }

            foreach (var line in readResult.Value)
            {
                var c = line.Split('\t');
                Results.Add(new MeasurementResultRow(
                    c.ElementAtOrDefault(0) ?? "", c.ElementAtOrDefault(1) ?? "",
                    c.ElementAtOrDefault(2) ?? "", c.ElementAtOrDefault(3) ?? ""));
            }
            RunFeedback = Results.Count == 0
                ? "iscmd 執行成功。匯出檔為空（此檔無量測元素/無真機台時屬正常）。"
                : $"iscmd 執行成功，讀到 {Results.Count} 行。";
        }, ex => RunFeedback = $"[失敗] 發生例外：{ex.Message}");

        IsBusy = false;
    }

    private bool CanRun() => SelectedIwp is not null && !IsBusy;

    partial void OnSelectedIwpChanged(IwpFile? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => RunCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task ToggleMainWindowAsync()
    {
        await Guard.RunAsync(async () =>
        {
            var ok = IsMainWindowVisible
                ? await Task.Run(() => _controller.HideMainWindow())
                : await Task.Run(() => _controller.ShowMainWindow());
            if (ok) IsMainWindowVisible = !IsMainWindowVisible;
        }, ex => RunFeedback = $"[失敗] 切換視窗顯示失敗：{ex.Message}");
    }
}
