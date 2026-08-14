using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class ProcessSeen
{
    public string Id { get; set; } = null!;

    public string? ProductName { get; set; }

    public string? CompanyName { get; set; }

    public string? FileVersion { get; set; }

    public string? ExePath { get; set; }

    public int HasWindow { get; set; }

    public int SessionId { get; set; }

    public int IsCritical { get; set; }
}
