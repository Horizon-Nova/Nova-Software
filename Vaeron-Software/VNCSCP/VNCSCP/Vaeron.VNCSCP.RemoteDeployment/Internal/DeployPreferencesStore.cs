using System.IO;
using System.Text.Json;
using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>提交表單記住的上次路徑。本功能自己的一個小檔案，不混進共用設定。</summary>
internal sealed class DeployPreferencesStore
{
    #region 統一的查詢來源

    private static string PreferencesFile => Path.Combine(Paths.Roaming, "remote-deployment-preferences.json");

    #endregion

    #region 專用查詢方法

    internal async Task<DeployPreferencesSnapshot> QueryPreferencesAsync(CancellationToken cancellationToken)
    {
        var files = new[] { PreferencesFile }.Where(File.Exists);
        var loaded = new List<DeployPreferencesSnapshot>();

        foreach (var path in files)
        {
            await using var stream = File.OpenRead(path);
            var snapshot = await JsonSerializer.DeserializeAsync<DeployPreferencesSnapshot>(
                stream, cancellationToken: cancellationToken);
            loaded.AddRange(new[] { snapshot }.OfType<DeployPreferencesSnapshot>());
        }

        return loaded.DefaultIfEmpty(new DeployPreferencesSnapshot()).First();
    }

    #endregion

    #region 基本 CRUD 操作

    internal async Task WritePreferencesAsync(DeployPreferencesSnapshot preferences, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(PreferencesFile);
        await JsonSerializer.SerializeAsync(stream, preferences, cancellationToken: cancellationToken);
    }

    #endregion
}
