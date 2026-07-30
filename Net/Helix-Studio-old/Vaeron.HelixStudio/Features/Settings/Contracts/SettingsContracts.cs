namespace Vaeron.HelixStudio.Features.Settings.Contracts;

public enum DataDensity { Comfortable, Compact }

public sealed record AppSettingsSnapshot
{
    public required string ThemePackId { get; init; }
    public required bool BackgroundAnimationEnabled { get; init; }
    public required int UiScalePercent { get; init; }
    public required DataDensity DataDensity { get; init; }
    public required bool SidebarCollapsedByDefault { get; init; }
    public required bool StartWithWindows { get; init; }
    public required string StartupPage { get; init; }
    public required bool ShowEventBadge { get; init; }
    public required string PowerMode { get; init; }
    public required int AcSleepMinutes { get; init; }
    public required int BatterySleepMinutes { get; init; }
    public required int ScreenTimeoutMinutes { get; init; }
    public required bool HibernateEnabled { get; init; }
    public required bool FastStartupEnabled { get; init; }
    public required string PowerButtonAction { get; init; }
    public required string LidCloseAction { get; init; }
}
