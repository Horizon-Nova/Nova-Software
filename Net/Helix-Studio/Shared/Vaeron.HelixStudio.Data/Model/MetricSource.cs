using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class MetricSource
{
    public string MetricKey { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public double Divisor { get; set; }

    public virtual EtwSource? EtwSource { get; set; }

    public virtual MachineMetric? MachineMetric { get; set; }

    public virtual PerfcounterSource? PerfcounterSource { get; set; }
}
