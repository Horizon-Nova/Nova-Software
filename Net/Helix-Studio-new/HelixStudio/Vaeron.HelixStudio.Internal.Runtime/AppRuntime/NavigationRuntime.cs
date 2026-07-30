namespace Vaeron.HelixStudio.Internal.Runtime.AppRuntime;

/// <summary>畫面導覽事件</summary>
public sealed class NavigationRuntime
{
    public event Action<string>? NavigationRequested;

    public void ApplyNavigateToModule(string moduleKey)
        => NavigationRequested?.Invoke(moduleKey);
}
