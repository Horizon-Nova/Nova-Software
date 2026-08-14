using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class HeatBand
{
    public string Metric { get; set; } = null!;

    public double Threshold { get; set; }

    public string Level { get; set; } = null!;
}
