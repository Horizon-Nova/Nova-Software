using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class SortDirection
{
    public string Name { get; set; } = null!;

    public int Sign { get; set; }

    public string Opposite { get; set; } = null!;

    public string Arrow { get; set; } = null!;

    public virtual ICollection<SortColumn> SortColumns { get; set; } = new List<SortColumn>();
}
