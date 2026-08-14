using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class MachineMetric
{
    public string MetricKey { get; set; } = null!;

    public string Instance { get; set; } = null!;

    public virtual MetricSource MetricKeyNavigation { get; set; } = null!;
}
