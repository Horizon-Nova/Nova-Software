using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Vaeron.HelixStudio.Contract;

namespace SqliteTool;

/// <summary>帶自己相依套件與自己資料庫的工具。SQLite 與資料庫檔都不在載體裡。</summary>
public sealed class Tool : ITool
{
    private const string FileName = "sqlite-tool.db";

    public string Name => "SQLite 工具";

    public string About => "自己帶 SQLite，資料庫檔開在自己的資料夾。";

    public string ModuleId => "sqlite-tool";

    public Screen Show(string action)
    {
        var path = Path.Combine(Environment.CurrentDirectory, FileName);

        using var connection = new SqliteConnection("Data Source=" + path);
        connection.Open();

        Run(connection, "CREATE TABLE IF NOT EXISTS visit (at TEXT NOT NULL)");
        Run(connection, "INSERT INTO visit (at) VALUES (datetime('now'))");

        return new Screen("SQLite 工具", [
            new Element("text", "工具自己帶的 SQLite　版本 " + Scalar(connection, "SELECT sqlite_version()"), ""),
            new Element("text", "資料庫檔　" + path, ""),
            new Element("text", "這個資料庫累計開啟次數　" + Scalar(connection, "SELECT COUNT(*) FROM visit"), ""),
            new Element("button", "再開一次", "again")
        ]);
    }

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!.ToString()!;
    }
}
