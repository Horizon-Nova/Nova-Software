using Microsoft.Data.Sqlite;

namespace Vaeron.HelixStudio.Data.Internal;

/// <summary>把打包的結構指令碼套用到資料庫。結構的來源是那些 .sql，這裡只負責讀出與執行。</summary>
internal static class SchemaApplier
{
    private const string SqlExtension = ".sql";

    private const string HistoryTableSql =
        "CREATE TABLE IF NOT EXISTS __schema_history (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)";

    public static IReadOnlyList<string> Apply(SqliteConnection connection)
    {
        Execute(connection, HistoryTableSql);

        var applied = ReadApplied(connection);
        var pending = ScriptNames()
            .Where(name => !applied.Contains(name))
            .ToList();

        foreach (var name in pending)
        {
            Execute(connection, ReadScript(name));
            Record(connection, name);
        }

        return pending;
    }

    private static IEnumerable<string> ScriptNames()
        => typeof(SchemaApplier).Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(SqlExtension, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

    private static string ReadScript(string name)
    {
        using var stream = typeof(SchemaApplier).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HashSet<string> ReadApplied(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM __schema_history";

        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static void Record(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO __schema_history (name, applied_at) VALUES ($name, $appliedAt)";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
