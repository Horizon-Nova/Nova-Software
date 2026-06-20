using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using HorizonNova.AITrainingModule.Models;
using HorizonNova.HelixStudio.ModuleSdk;
using WpfApp = System.Windows.Application;

namespace HorizonNova.AITrainingModule.ViewModels;

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();
    public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class AITrainingViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly IShellContext _shell;
    private Process? _process;
    private System.Timers.Timer? _sysTimer;
    private DateTime _epochStart;
    private EpochMetric? _pendingMetric; // accumulates train then val data per epoch

    // ── Training config ──────────────────────────────────────────────
    private string _projectPath = string.Empty;
    public string ProjectPath { get => _projectPath; set => Set(ref _projectPath, value); }

    private string _selectedModel = "yolo11n.pt";
    public string SelectedModel { get => _selectedModel; set => Set(ref _selectedModel, value); }

    private int _totalEpochs = 50;
    public int TotalEpochs { get => _totalEpochs; set => Set(ref _totalEpochs, value); }

    private int _batchSize = 16;
    public int BatchSize { get => _batchSize; set => Set(ref _batchSize, value); }

    private int _imgSize = 640;
    public int ImgSize { get => _imgSize; set => Set(ref _imgSize, value); }

    private bool _enableAmp = true;
    public bool EnableAmp { get => _enableAmp; set => Set(ref _enableAmp, value); }

    // ── Augmentation ─────────────────────────────────────────────────
    private double _augFliplr = 0.5;
    public double AugFliplr { get => _augFliplr; set => Set(ref _augFliplr, value); }

    private double _augDegrees = 0.0;
    public double AugDegrees { get => _augDegrees; set => Set(ref _augDegrees, value); }

    private double _augScale = 0.5;
    public double AugScale { get => _augScale; set => Set(ref _augScale, value); }

    private double _augMosaic = 1.0;
    public double AugMosaic { get => _augMosaic; set => Set(ref _augMosaic, value); }

    private double _augBlur = 0.0;
    public double AugBlur { get => _augBlur; set => Set(ref _augBlur, value); }

    private double _augHsvS = 0.7;
    public double AugHsvS { get => _augHsvS; set => Set(ref _augHsvS, value); }

    // ── State ─────────────────────────────────────────────────────────
    private bool _isTraining;
    public bool IsTraining
    {
        get => _isTraining;
        set
        {
            Set(ref _isTraining, value);
            StartCommand.Raise();
            StopCommand.Raise();
            ResumeCommand.Raise();
        }
    }

    private string _statusCode = "idle"; // idle | training | done | error
    public string StatusCode { get => _statusCode; set => Set(ref _statusCode, value); }

    private int _currentEpoch;
    public int CurrentEpoch { get => _currentEpoch; set => Set(ref _currentEpoch, value); }

    private double _epochProgress;
    public double EpochProgress { get => _epochProgress; set => Set(ref _epochProgress, value); }

    private string _eta = "—";
    public string Eta { get => _eta; set => Set(ref _eta, value); }

    private bool _isOverfitting;
    public bool IsOverfitting { get => _isOverfitting; set => Set(ref _isOverfitting, value); }

    // ── Latest metrics ────────────────────────────────────────────────
    private double _latestMap50;
    public double LatestMap50 { get => _latestMap50; set => Set(ref _latestMap50, value); }

    private double _latestMap5095;
    public double LatestMap5095 { get => _latestMap5095; set => Set(ref _latestMap5095, value); }

    private double _latestPrecision;
    public double LatestPrecision { get => _latestPrecision; set => Set(ref _latestPrecision, value); }

    private double _latestRecall;
    public double LatestRecall { get => _latestRecall; set => Set(ref _latestRecall, value); }

    // ── System ────────────────────────────────────────────────────────
    private double _gpuUsage;
    public double GpuUsage { get => _gpuUsage; set => Set(ref _gpuUsage, value); }

    private double _gpuMemProgress;
    public double GpuMemProgress { get => _gpuMemProgress; set => Set(ref _gpuMemProgress, value); }

    private string _gpuMemText = "— / —";
    public string GpuMemText { get => _gpuMemText; set => Set(ref _gpuMemText, value); }

    private double _cpuUsage;
    public double CpuUsage { get => _cpuUsage; set => Set(ref _cpuUsage, value); }

    // ── Collections ───────────────────────────────────────────────────
    public ObservableCollection<EpochMetric> Metrics { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────
    public RelayCommand StartCommand  { get; }
    public RelayCommand StopCommand   { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand BrowseCommand { get; }

    // ── Regex patterns ────────────────────────────────────────────────
    private static readonly Regex RxEpoch = new(
        @"(\d+)/(\d+)\s+([\d.]+)G\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)",
        RegexOptions.Compiled);

    private static readonly Regex RxVal = new(
        @"^\s+all\s+\d+\s+\d+\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public AITrainingViewModel(IShellContext shell)
    {
        _shell = shell;

        // default project path = NewProjectV2 next to Nova-Software
        var moduleDir = AppDomain.CurrentDomain.BaseDirectory;
        var repoRoot  = Path.GetFullPath(Path.Combine(moduleDir, @"..\..\..\..\.."));
        ProjectPath   = Path.Combine(repoRoot, "NewProjectV2");

        StartCommand  = new RelayCommand(() => StartTraining(),  () => !IsTraining);
        StopCommand   = new RelayCommand(StopTraining,   () =>  IsTraining);
        ResumeCommand = new RelayCommand(() => StartTraining(resume: true), () => !IsTraining);
        BrowseCommand = new RelayCommand(BrowseProject);

        StartSysMonitor();
    }

    // ── Training control ─────────────────────────────────────────────
    private void StartTraining(bool resume = false)
    {
        if (IsTraining) return;

        var python = FindPython();
        if (python is null)
        {
            AppendLog("[錯誤] 找不到 Python 解譯器。請確認 .venv 已建立。");
            StatusCode = "error";
            return;
        }

        var script = Path.Combine(ProjectPath, "modules", "segment", "train_yolo.py");
        if (!File.Exists(script))
        {
            AppendLog($"[錯誤] 找不到訓練腳本：{script}");
            StatusCode = "error";
            return;
        }

        var args = BuildArgs(resume);

        Metrics.Clear();
        LogLines.Clear();
        CurrentEpoch  = 0;
        EpochProgress = 0;
        Eta           = "—";
        IsOverfitting = false;
        _pendingMetric = null;
        _epochStart   = DateTime.Now;

        var psi = new ProcessStartInfo
        {
            FileName               = python,
            Arguments              = $"\"{script}\" {args}",
            WorkingDirectory       = ProjectPath,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnOutput;
        _process.ErrorDataReceived  += OnOutput;
        _process.Exited += OnProcessExited;
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        IsTraining = true;
        StatusCode = "training";
        AppendLog($"[開始] {python} {script}");
        AppendLog($"[設定] epochs={TotalEpochs} batch={BatchSize} model={SelectedModel}");
    }

    private void StopTraining()
    {
        try { _process?.Kill(entireProcessTree: true); } catch { }
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        var line = e.Data;

        Dispatch(() => AppendLog(line));

        // parse training epoch line
        var mEpoch = RxEpoch.Match(line);
        if (mEpoch.Success)
        {
            int epoch  = int.Parse(mEpoch.Groups[1].Value);
            int total  = int.Parse(mEpoch.Groups[2].Value);
            double gpu = double.Parse(mEpoch.Groups[3].Value);
            double box = double.Parse(mEpoch.Groups[4].Value);
            double cls = double.Parse(mEpoch.Groups[5].Value);
            double dfl = double.Parse(mEpoch.Groups[6].Value);

            _pendingMetric = new EpochMetric(epoch, total, box, cls, dfl, 0, 0, 0, 0, 0, 0, gpu);

            var elapsed = DateTime.Now - _epochStart;
            var perEpoch = epoch > 0 ? elapsed.TotalSeconds / epoch : 0;
            var remaining = perEpoch * (total - epoch);
            var etaStr = remaining > 0 ? FormatEta(remaining) : "—";

            Dispatch(() =>
            {
                CurrentEpoch  = epoch;
                TotalEpochs   = total;
                EpochProgress = (double)epoch / total;
                Eta           = etaStr;
            });
            return;
        }

        // parse val metrics line
        var mVal = RxVal.Match(line);
        if (mVal.Success && _pendingMetric is not null)
        {
            double P       = double.Parse(mVal.Groups[1].Value);
            double R       = double.Parse(mVal.Groups[2].Value);
            double map50   = double.Parse(mVal.Groups[3].Value);
            double map5095 = double.Parse(mVal.Groups[4].Value);

            var completed = _pendingMetric with
            {
                Precision = P,
                Recall    = R,
                mAP50     = map50,
                mAP5095   = map5095,
                ValBoxLoss = _pendingMetric.BoxLoss * 1.05, // placeholder until YOLO outputs val losses
                ValClsLoss = _pendingMetric.ClsLoss * 1.05,
            };
            _pendingMetric = null;

            Dispatch(() =>
            {
                Metrics.Add(completed);
                LatestMap50     = map50;
                LatestMap5095   = map5095;
                LatestPrecision = P;
                LatestRecall    = R;
                CheckOverfitting();
            });
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        Dispatch(() =>
        {
            IsTraining = false;
            StatusCode = _process?.ExitCode == 0 ? "done" : "error";
            Eta        = "—";
        });
    }

    private void CheckOverfitting()
    {
        if (Metrics.Count < 5) return;
        var last5 = Metrics.TakeLast(5).ToList();
        // check if val loss consistently > train loss by >15%
        int overfit = last5.Count(m => m.ValLoss > m.TrainLoss * 1.15);
        IsOverfitting = overfit >= 4;
    }

    // ── System monitor ───────────────────────────────────────────────
    private void StartSysMonitor()
    {
        _sysTimer = new System.Timers.Timer(2500);
        _sysTimer.Elapsed += async (_, _) => await PollSystemAsync();
        _sysTimer.Start();
    }

    private async System.Threading.Tasks.Task PollSystemAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("nvidia-smi",
                "--query-gpu=utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits")
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return;
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();

            var parts = output.Trim().Split(',');
            if (parts.Length < 3) return;

            if (double.TryParse(parts[0].Trim(), out double gpu)     &&
                double.TryParse(parts[1].Trim(), out double memUsed) &&
                double.TryParse(parts[2].Trim(), out double memTotal))
            {
                Dispatch(() =>
                {
                    GpuUsage      = gpu;
                    GpuMemProgress = memTotal > 0 ? memUsed / memTotal : 0;
                    GpuMemText    = $"{memUsed / 1024.0:F1} / {memTotal / 1024.0:F1} GB";
                });
            }
        }
        catch { /* nvidia-smi not available */ }

        // CPU via simple WMI-free method: two samples
        try
        {
            var start = Process.GetCurrentProcess().TotalProcessorTime;
            await System.Threading.Tasks.Task.Delay(200);
            var end     = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuPct  = (end - start).TotalMilliseconds / (0.2 * Environment.ProcessorCount * 10.0);
            Dispatch(() => CpuUsage = Math.Min(100, cpuPct));
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private string? FindPython()
    {
        var venv = Path.Combine(ProjectPath, ".venv", "Scripts", "python.exe");
        if (File.Exists(venv)) return venv;
        var env = Path.Combine(ProjectPath, "env", "Scripts", "python.exe");
        if (File.Exists(env)) return env;
        return null;
    }

    private string BuildArgs(bool resume)
    {
        var parts = new List<string>
        {
            $"--model {SelectedModel}",
            $"--epochs {TotalEpochs}",
            $"--batch {BatchSize}",
            $"--imgsz {ImgSize}",
            $"--fliplr {AugFliplr:F2}",
            $"--degrees {AugDegrees:F1}",
            $"--scale {AugScale:F2}",
            $"--mosaic {AugMosaic:F2}",
            $"--blur {AugBlur:F2}",
            $"--hsv_s {AugHsvS:F2}",
        };
        if (resume) parts.Add("--resume");
        if (EnableAmp) parts.Add("--amp");
        return string.Join(" ", parts);
    }

    private static string FormatEta(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalSeconds}s";
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        LogLines.Add(line);
        if (LogLines.Count > 500) LogLines.RemoveAt(0);
    }

    private static void Dispatch(Action action)
        => WpfApp.Current?.Dispatcher.InvokeAsync(action);

    private void BrowseProject()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title            = "選擇 NewProjectV2 目錄",
            InitialDirectory = ProjectPath,
        };
        if (dlg.ShowDialog() == true)
            ProjectPath = dlg.FolderName;
    }

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void OnDeactivated() { }

    public void Dispose()
    {
        _sysTimer?.Stop();
        _sysTimer?.Dispose();
        try { _process?.Kill(entireProcessTree: true); } catch { }
        _process?.Dispose();
    }
}
