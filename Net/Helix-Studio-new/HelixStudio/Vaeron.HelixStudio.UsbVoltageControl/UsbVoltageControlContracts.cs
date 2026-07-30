namespace Vaeron.HelixStudio.UsbVoltageControl;

public sealed record UsbDeviceSnapshot
{
    public required string DeviceId { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required bool Connected { get; init; }
    public required bool SupportsPowerControl { get; init; }
    public string LastInterruption { get; init; } = "";
    public double? Voltage { get; init; }
    public required double Target { get; init; }
    public bool IsWithinTolerance => Voltage is not null && Math.Abs(Voltage.Value - Target) <= ToleranceVolts;
    public const double ToleranceVolts = 0.15;
}

public sealed record ApplyVoltageRequest
{
    public required string DeviceId { get; init; }
    public required double TargetVoltage { get; init; }
}

public sealed record DeviceListSnapshot(IReadOnlyList<UsbDeviceSnapshot> Devices, string HardwareStatusMessage);
