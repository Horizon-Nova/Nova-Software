using Microsoft.Data.Sqlite;
using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Data.Internal;

/// <summary>只委派，不含邏輯。</summary>
internal sealed class DatabaseFacade(DatabaseOptions options) : IDatabaseFacade
{
    public string FilePath { get; } = Path.Combine(Paths.Local, options.FileName);

    public SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    public IReadOnlyList<string> ApplySchema()
    {
        using var connection = OpenConnection();
        return SchemaApplier.Apply(connection);
    }
}
