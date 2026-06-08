using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;

namespace Helix_Studio.AppRuntime;

/// <summary>
/// 閒置鎖定 Runtime，負責監控使用者輸入並於逾時時提出鎖定請求。
/// </summary>
public sealed class InactivityRuntime : IDisposable
{
    private const string SettingsSection = "Settings";

    private static readonly IReadOnlyDictionary<int, TimeSpan> TimeoutByIndex =
        new Dictionary<int, TimeSpan>
        {
            [1] = TimeSpan.FromMinutes(5),
            [2] = TimeSpan.FromMinutes(15),
            [3] = TimeSpan.FromMinutes(30),
            [4] = TimeSpan.FromHours(1),
        };

    private readonly DispatcherTimer _timer;
    private readonly IniRepository _iniRepository;
    private TimeSpan? _timeout;
    private bool _isLocked;

    /// <summary>
    /// 當達到閒置逾時，通知 UI 啟動鎖定流程。
    /// </summary>
    public event EventHandler? LockRequested;

    /// <summary>
    /// 建立閒置鎖定 Runtime，並依目前設定啟用監控。
    /// </summary>
    public InactivityRuntime(IniRepository iniRepository)
    {
        _iniRepository = iniRepository;
        _iniRepository.Changed += OnIniChanged;

        _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        _timer.Tick += OnTick;
        InputManager.Current.PreProcessInput += OnPreProcessInput;

        ApplyTimeoutFromIni();
    }

    /// <summary>
    /// 套用逾時設定；<see langword="null"/> 代表停用閒置鎖定。
    /// </summary>
    public void ApplyTimeout(TimeSpan? timeout)
    {
        _timeout = timeout;

        if (_timeout is null || _timeout.Value <= TimeSpan.Zero)
        {
            _timer.Stop();
            return;
        }

        if (_isLocked)
        {
            return;
        }

        ResetTimer();
    }

    /// <summary>
    /// 通知 Runtime 已進入鎖定狀態，暫停計時。
    /// </summary>
    public void ApplyLocked()
    {
        _isLocked = true;
        _timer.Stop();
    }

    /// <summary>
    /// 通知 Runtime 已解除鎖定，恢復監控。
    /// </summary>
    public void ApplyUnlocked()
    {
        _isLocked = false;
        ResetTimer();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _iniRepository.Changed -= OnIniChanged;
        _timer.Tick -= OnTick;
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (_isLocked)
        {
            return;
        }

        if (_timeout is null || _timeout.Value <= TimeSpan.Zero)
        {
            return;
        }

        var input = e.StagingItem?.Input;
        if (input is null)
        {
            return;
        }

        if (input is KeyboardEventArgs or MouseEventArgs)
        {
            ResetTimer();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isLocked)
        {
            return;
        }

        if (_timeout is null || _timeout.Value <= TimeSpan.Zero)
        {
            _timer.Stop();
            return;
        }

        _isLocked = true;
        _timer.Stop();
        LockRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetTimer()
    {
        if (_timeout is null || _timeout.Value <= TimeSpan.Zero)
        {
            _timer.Stop();
            return;
        }

        _timer.Stop();
        _timer.Interval = _timeout.Value;
        _timer.Start();
    }

    private void OnIniChanged(object? sender, EventArgs e) => ApplyTimeoutFromIni();

    private void ApplyTimeoutFromIni()
    {
        var settings = _iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);
        var timeout = TimeoutByIndex.TryGetValue(settings.InactivityIndex, out var resolved)
            ? resolved
            : (TimeSpan?)null;

        ApplyTimeout(timeout);
    }
}
