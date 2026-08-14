namespace Vaeron.HelixStudio.Shell.Internal;

/// <summary>業務規則集中處。呼叫 Store 取得事實，決定回傳的內容與形狀。</summary>
internal sealed class ShellLogic(ShellStore store)
{
    public ValueTask<ShellSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        var runningNames = store.InstalledApps
            .Where(app => app.State == ShellStore.RunningState)
            .Select(app => app.Name)
            .ToHashSet();

        var runningTools = store.ToolsNav
            .Where(item => runningNames.Contains(item.AppName))
            .ToList();

        var snapshot = new ShellSnapshot(
            store.MainNav,
            runningTools,
            store.SettingsNav,
            store.Workspaces.First(),
            store.ChatSeed);

        return ValueTask.FromResult(snapshot);
    }
}
