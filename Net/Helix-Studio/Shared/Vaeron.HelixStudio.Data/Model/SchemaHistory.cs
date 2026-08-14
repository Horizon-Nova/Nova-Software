using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class SchemaHistory
{
    public string Name { get; set; } = null!;

    public string AppliedAt { get; set; } = null!;
}
