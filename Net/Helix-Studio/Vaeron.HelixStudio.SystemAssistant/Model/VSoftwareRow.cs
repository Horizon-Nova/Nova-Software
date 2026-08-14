using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.SystemAssistant.Model;

public partial class VSoftwareRow
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Publisher { get; set; }

    public string? State { get; set; }

    public string? StateLabel { get; set; }

    public string? ToggleLabel { get; set; }

    public string? ToggleTitle { get; set; }

    public string? ToggleIcon { get; set; }

    public int? ToggleEnabled { get; set; }

    public string? LogLevel { get; set; }

    public string? MetaText { get; set; }

    public string? CpuText { get; set; }

    public string? RamText { get; set; }

    public string? NetText { get; set; }

    public string? DiskText { get; set; }

    public string? CpuLevel { get; set; }

    public string? NetLevel { get; set; }

    public double? CpuPct { get; set; }

    public double? RamMb { get; set; }

    public double? NetMbps { get; set; }

    public double? DiskMbs { get; set; }

    public int? ChangedCount { get; set; }
}
