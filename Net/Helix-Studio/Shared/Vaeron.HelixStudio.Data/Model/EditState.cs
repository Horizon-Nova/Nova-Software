using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class EditState
{
    public int Editable { get; set; }

    public int Dirty { get; set; }

    public int Present { get; set; }

    public string State { get; set; } = null!;

    public string IconTitle { get; set; } = null!;

    public string Placeholder { get; set; } = null!;
}
