using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Sampling.Model;

public partial class VSamplingTarget
{
    public string? ScopeId { get; set; }

    public string? MetricKey { get; set; }

    public string? Provider { get; set; }

    public double? Divisor { get; set; }

    public string? Instance { get; set; }

    public string? Category { get; set; }

    public string? Counter { get; set; }

    public string? Kind { get; set; }

    public string? MatchKind { get; set; }

    public string? EtwProvider { get; set; }

    public string? EtwGuid { get; set; }
}
