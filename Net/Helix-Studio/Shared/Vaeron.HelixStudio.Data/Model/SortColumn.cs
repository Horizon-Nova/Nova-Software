using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class SortColumn
{
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string Alignment { get; set; } = null!;

    public double? Width { get; set; }

    public string DefaultDirection { get; set; } = null!;

    public int Position { get; set; }

    public virtual SortDirection DefaultDirectionNavigation { get; set; } = null!;
}
