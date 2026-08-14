using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class Reading
{
    public string ScopeId { get; set; } = null!;

    public string MetricKey { get; set; } = null!;

    public double Value { get; set; }
}
