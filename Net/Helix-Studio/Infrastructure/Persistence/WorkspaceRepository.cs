using System.IO;
using Helix_Studio.Model.Workspace;
using Microsoft.Data.Sqlite;

namespace Helix_Studio.Infrastructure.Persistence;

/// <summary>工作區資料之 SQLite 存取。</summary>
public sealed class WorkspaceRepository
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HorizonNovaStudio");

    private static readonly string DbFile = Path.Combine(AppDataDir, "workspaces.db");

    private string ConnectionString => $"Data Source={DbFile}";

    /// <summary>建立資料庫檔與資料表，並於空資料庫時插入預設工作區。</summary>
    public void Initialize()
    {
        Directory.CreateDirectory(AppDataDir);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS workspaces (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT '',
                is_active   INTEGER NOT NULL DEFAULT 0,
                created_at  TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
            );
            """;
        command.ExecuteNonQuery();

        command.CommandText = "SELECT COUNT(*) FROM workspaces;";
        var count = (long)(command.ExecuteScalar() ?? 0L);
        if (count == 0)
        {
            command.CommandText = """
                INSERT INTO workspaces (name, description, is_active)
                VALUES ('Default', '預設工作區', 1);
                """;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>查詢所有工作區列舉。</summary>
    public List<WorkspaceModel> GetAll()
    {
        var list = new List<WorkspaceModel>();
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, description, is_active, created_at FROM workspaces ORDER BY id;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new WorkspaceModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                CreatedAt = reader.GetString(4),
            });
        }

        return list;
    }

    /// <summary>查詢目前使用中工作區；無則回傳 null。</summary>
    public WorkspaceModel? GetActive()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, description, is_active, created_at FROM workspaces WHERE is_active = 1 LIMIT 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new WorkspaceModel
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            IsActive = true,
            CreatedAt = reader.GetString(4),
        };
    }

    /// <summary>建立新工作區並回傳模型（非使用中）。</summary>
    public WorkspaceModel Create(string name, string description)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO workspaces (name, description, is_active) VALUES ($name, $desc, 0);";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$desc", description);
        command.ExecuteNonQuery();

        command.CommandText = "SELECT last_insert_rowid();";
        command.Parameters.Clear();
        var newId = (long)(command.ExecuteScalar() ?? 0L);

        return new WorkspaceModel
        {
            Id = (int)newId,
            Name = name,
            Description = description,
            IsActive = false,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    /// <summary>更新指定工作區名稱與描述。</summary>
    public void Update(int id, string name, string description)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE workspaces SET name = $name, description = $desc WHERE id = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$desc", description);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>刪除指定工作區；成功刪除回傳 true。</summary>
    public bool Delete(int id)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspaces WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>將指定工作區設為唯一使用中項目。</summary>
    public void SetActive(int id)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE workspaces SET is_active = 0;";
        command.ExecuteNonQuery();

        command.CommandText = "UPDATE workspaces SET is_active = 1 WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>查詢工作區總筆數。</summary>
    public int QueryWorkspaceCount()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM workspaces;";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }
}
