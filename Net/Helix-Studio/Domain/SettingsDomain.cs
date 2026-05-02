using System;
using System.Collections.Generic;
using Helix_Studio.Model.Configuration;
using Helix_Studio.Model.EventLog;

namespace Helix_Studio.Domain;

/// <summary>應用程式設定相關之索引對應與預設值（純領域）。</summary>
public static class SettingsDomain
{
    private static readonly IReadOnlyDictionary<int, string> CultureNameByIndex =
        new Dictionary<int, string>
        {
            [1] = AppCultureNames.En,
        };

    private static readonly IReadOnlyDictionary<int, AppThemeMode> ThemeModeByIndex =
        new Dictionary<int, AppThemeMode>
        {
            [1] = AppThemeMode.Dark,
            [2] = AppThemeMode.System,
        };

    private static readonly IReadOnlyDictionary<int, TimeSpan> InactivityTimeoutByIndex =
        new Dictionary<int, TimeSpan>
        {
            [1] = TimeSpan.FromMinutes(5),
            [2] = TimeSpan.FromMinutes(15),
            [3] = TimeSpan.FromMinutes(30),
            [4] = TimeSpan.FromHours(1),
        };

    private static readonly IReadOnlyDictionary<int, LogLevel> LogMinLevelByIndex =
        new Dictionary<int, LogLevel>
        {
            [1] = LogLevel.Warning,
            [2] = LogLevel.Error,
            [3] = LogLevel.Debug,
        };

    private static readonly IReadOnlyDictionary<int, int> RetentionDaysByIndex =
        new Dictionary<int, int>
        {
            [0] = 7,
            [2] = 90,
            [3] = 180,
        };

    /// <summary>依語言索引查詢文化名稱字串。</summary>
    public static string QueryCultureName(int languageIndex) =>
        CultureNameByIndex.TryGetValue(languageIndex, out var cultureName)
            ? cultureName
            : AppCultureNames.ZhHant;

    /// <summary>依主題索引查詢主題模式。</summary>
    public static AppThemeMode QueryThemeMode(int themeIndex) =>
        ThemeModeByIndex.TryGetValue(themeIndex, out var themeMode)
            ? themeMode
            : AppThemeMode.Light;

    /// <summary>依閒置鎖定索引查詢逾時時間。</summary>
    public static TimeSpan QueryInactivityTimeout(int inactivityIndex) =>
        InactivityTimeoutByIndex.TryGetValue(inactivityIndex, out var timeout)
            ? timeout
            : TimeSpan.Zero;

    /// <summary>依日誌層級索引查詢最低紀錄層級。</summary>
    public static LogLevel QueryLogMinLevel(int logLevelIndex) =>
        LogMinLevelByIndex.TryGetValue(logLevelIndex, out var minLevel)
            ? minLevel
            : LogLevel.Info;

    /// <summary>依保留天數索引查詢日誌保留天數。</summary>
    public static int QueryRetentionDays(int retentionIndex) =>
        RetentionDaysByIndex.TryGetValue(retentionIndex, out var days)
            ? days
            : 30;
}
