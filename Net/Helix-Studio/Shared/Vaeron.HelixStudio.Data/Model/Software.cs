using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class Software
{
    public string Id { get; set; } = null!;

    public string? ProductName { get; set; }

    public string? CompanyName { get; set; }

    public string? FileVersion { get; set; }

    public string? ExePath { get; set; }

    public int HasWindow { get; set; }

    public int SessionId { get; set; }

    public int IsCritical { get; set; }

    public string State { get; set; } = null!;

    public virtual ICollection<QuotaDraft> QuotaDrafts { get; set; } = new List<QuotaDraft>();

    public virtual ICollection<SoftwareValue> SoftwareValues { get; set; } = new List<SoftwareValue>();

    public virtual StatePresentation StateNavigation { get; set; } = null!;
}
