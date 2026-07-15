namespace Vaeron.HelixStudio.Features.Settings.Contracts;

public enum ThemeMode { Light, Dark }
public enum DataDensity { Comfortable, Compact }

public sealed record AppSettingsSnapshot
{
    public required ThemeMode ThemeMode { get; init; }
    public required double AccentHue { get; init; }
    public required double AccentChroma { get; init; }
    public required int UiScalePercent { get; init; }
    public required DataDensity DataDensity { get; init; }
    public required bool SidebarCollapsedByDefault { get; init; }
    public required bool StartWithWindows { get; init; }
    public required string StartupPage { get; init; }
    public required bool ShowEventBadge { get; init; }
}
