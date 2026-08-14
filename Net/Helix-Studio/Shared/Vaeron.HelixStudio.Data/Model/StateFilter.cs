using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class StateFilter
{
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string NextKey { get; set; } = null!;

    public int Highlighted { get; set; }

    public virtual ICollection<StatePresentation> States { get; set; } = new List<StatePresentation>();
}
