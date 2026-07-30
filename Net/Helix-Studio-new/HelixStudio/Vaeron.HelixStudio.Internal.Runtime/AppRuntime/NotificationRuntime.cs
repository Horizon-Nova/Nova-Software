namespace Vaeron.HelixStudio.Internal.Runtime.AppRuntime;

public sealed class NotificationRuntime
{
    public event Action<string, bool>? NotificationRequested; // (message, isSuccess)

    public void ApplyShowSuccess(string message) => NotificationRequested?.Invoke(message, true);

    public void ApplyShowFailure(string message) => NotificationRequested?.Invoke(message, false);
}
