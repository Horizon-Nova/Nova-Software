using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class ProviderStatus
{
    public string Provider { get; set; } = null!;

    public int Available { get; set; }

    public string Reason { get; set; } = null!;
}
