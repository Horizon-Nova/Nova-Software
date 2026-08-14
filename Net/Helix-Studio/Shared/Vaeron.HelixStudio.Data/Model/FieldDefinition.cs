using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class FieldDefinition
{
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public int Editable { get; set; }

    public string Note { get; set; } = null!;

    public int? Min { get; set; }

    public int? Max { get; set; }

    public int Position { get; set; }

    public virtual ICollection<QuotaDraft> QuotaDrafts { get; set; } = new List<QuotaDraft>();

    public virtual ICollection<SoftwareValue> SoftwareValues { get; set; } = new List<SoftwareValue>();
}
