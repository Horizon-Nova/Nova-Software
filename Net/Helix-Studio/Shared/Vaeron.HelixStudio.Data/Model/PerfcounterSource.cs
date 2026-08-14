using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class PerfcounterSource
{
    public string MetricKey { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Counter { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string MatchKind { get; set; } = null!;

    public virtual MetricSource MetricKeyNavigation { get; set; } = null!;
}
