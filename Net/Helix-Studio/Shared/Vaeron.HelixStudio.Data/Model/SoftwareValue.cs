using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class SoftwareValue
{
    public string SoftwareId { get; set; } = null!;

    public string FieldKey { get; set; } = null!;

    public string? Value { get; set; }

    public virtual FieldDefinition FieldKeyNavigation { get; set; } = null!;

    public virtual Software Software { get; set; } = null!;
}
