using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class ToggleAction
{
    public string State { get; set; } = null!;

    public int IsCritical { get; set; }

    public string Action { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Icon { get; set; } = null!;

    public int Enabled { get; set; }

    public virtual StatePresentation StateNavigation { get; set; } = null!;
}
