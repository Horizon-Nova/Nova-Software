using Microsoft.EntityFrameworkCore;

namespace Vaeron.Kernel.Data;

/// <summary>把打包的結構定義檔套用到 app.db。結構的來源是這些 SQL 檔，不由 C# 型別推導。</summary>
public static class VncscpSchema
{
    private const string BaselineScript = "Baseline001.sql";

    private static readonly IReadOnlyList<string> BaselineOnly = [BaselineScript];

    /// <summary>connection 表現有的欄位就是資料庫目前的形狀，逐種形狀對應要套用的腳本。</summary>
    private static readonly IReadOnlyList<SchemaShape> ShapeByMarkerColumn =
    [
        new("group_id", ["Legacy002.sql", BaselineScript]),
        new("group_name", BaselineOnly)
    ];

    public static async Task ApplyAsync(VncscpDbContext dbContext, CancellationToken cancellationToken)
    {
        var columns = await ReadConnectionColumnsAsync(dbContext, cancellationToken);

        var scripts = ShapeByMarkerColumn
            .Where(shape => columns.Contains(shape.MarkerColumn))
            .Select(shape => shape.Scripts)
            .DefaultIfEmpty(BaselineOnly)
            .First();

        // 整批套用或整批不套用。中途失敗留下半套結構會讓下一次啟動誤判資料庫形狀。
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var script in scripts.Select(ReadScript))
        {
            await dbContext.Database.ExecuteSqlRawAsync(script, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>connection 表不存在時為空集合，代表全新資料庫。</summary>
    private static async Task<IReadOnlySet<string>> ReadConnectionColumnsAsync(
        VncscpDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('connection')";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static string ReadScript(string name)
    {
        var assembly = typeof(VncscpSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Vaeron.Kernel.Data.Schema.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>某一種資料庫形狀，以及把它帶到目前形狀所需的腳本。</summary>
    private sealed record SchemaShape(string MarkerColumn, IReadOnlyList<string> Scripts);
}
