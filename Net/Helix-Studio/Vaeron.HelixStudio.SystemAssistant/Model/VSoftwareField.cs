using System;
using System.Collections.Generic;

namespace Vaeron.HelixStudio.SystemAssistant.Model;

public partial class VSoftwareField
{
    public string? SoftwareId { get; set; }

    public string? FieldKey { get; set; }

    public string? Label { get; set; }

    public string? Unit { get; set; }

    public int? Editable { get; set; }

    public int? Position { get; set; }

    public string? StoredValue { get; set; }

    public string? DraftValue { get; set; }

    public string? DisplayValue { get; set; }

    public string? EditState { get; set; }

    public string? IconTitle { get; set; }

    public string? Placeholder { get; set; }

    public string? Note { get; set; }
}
