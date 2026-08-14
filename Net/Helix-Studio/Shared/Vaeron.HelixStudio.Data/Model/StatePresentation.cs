using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.Data.Model;

public partial class StatePresentation
{
    public string State { get; set; } = null!;

    public int Rank { get; set; }

    public string Label { get; set; } = null!;

    public string LogLevel { get; set; } = null!;

    public int ShowsReading { get; set; }

    public virtual ICollection<Software> Softwares { get; set; } = new List<Software>();

    public virtual ICollection<ToggleAction> ToggleActions { get; set; } = new List<ToggleAction>();

    public virtual ICollection<StateFilter> FilterKeys { get; set; } = new List<StateFilter>();
}
