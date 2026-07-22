namespace Vaeron.Platform.AppRuntime;

public sealed class NotificationRuntime
{
    public event Action<string, bool>? NotificationRequested; // (message, isSuccess)

    public void ApplyShowResult(bool success, string message)
        => NotificationRequested?.Invoke(message, success);
}
