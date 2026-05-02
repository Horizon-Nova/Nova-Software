namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// Result of a module license key verification performed by the shell.
/// Returned by <see cref="IShellContext.VerifyModuleLicense"/>.
/// </summary>
public sealed record ModuleLicenseResult
{
    /// <summary>True when the key is valid and the hardware score passes the threshold.</summary>
    public bool   IsValid          { get; init; }

    /// <summary>
    /// True when the key format is correct and the hardware score is above the
    /// rebind threshold but below the full-pass threshold.
    /// A rebind-eligible machine can have a new key generated with its current fingerprints.
    /// </summary>
    public bool   IsRebindEligible { get; init; }

    /// <summary>Sum of the weights of all valid hardware fingerprints collected.</summary>
    public int    TotalScore       { get; init; }

    /// <summary>Human-readable reason for failure; empty on success.</summary>
    public string ErrorMessage     { get; init; } = string.Empty;

    // ── Factory helpers ────────────────────────────────────────────────────

    /// <summary>Creates a successful result.</summary>
    public static ModuleLicenseResult Valid(int score)
        => new() { IsValid = true, TotalScore = score };

    /// <summary>Creates a failed result.</summary>
    public static ModuleLicenseResult Invalid(
        string error,
        int    score          = 0,
        bool   rebindEligible = false)
        => new()
        {
            IsValid          = false,
            ErrorMessage     = error,
            TotalScore       = score,
            IsRebindEligible = rebindEligible,
        };
}
