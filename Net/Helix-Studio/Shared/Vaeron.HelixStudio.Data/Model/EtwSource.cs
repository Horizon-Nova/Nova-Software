using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class EtwSource
{
    public string MetricKey { get; set; } = null!;

    public string ProviderName { get; set; } = null!;

    public string ProviderGuid { get; set; } = null!;

    public virtual MetricSource MetricKeyNavigation { get; set; } = null!;
}
