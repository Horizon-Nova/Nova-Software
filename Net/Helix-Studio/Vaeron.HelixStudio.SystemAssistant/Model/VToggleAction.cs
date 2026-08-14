using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.SystemAssistant.Model;

public partial class VToggleAction
{
    public string? SoftwareId { get; set; }

    public string? Action { get; set; }

    public string? Name { get; set; }

    public string? LogLevel { get; set; }

    public string? Label { get; set; }

    public string? ExePath { get; set; }
}
