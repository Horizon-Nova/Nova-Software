using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>各 Feature 的資料庫 DbContext 型別是 internal（VAE006），App 根專案無法直接持有具體型別
/// 逐一呼叫建表／欄位遷移——改由每個 Feature 自己實作本介面、於 DI 註冊，App 啟動時只解析
/// IEnumerable&lt;IFeatureDatabaseInitializer&gt; 逐一呼叫，不需要知道任何具體 DbContext 型別。</summary>
public interface IFeatureDatabaseInitializer
{
    public Task InitializeAsync();
}

public sealed record ColumnMigration(string Table, string Column, string SqlType, string? DefaultLiteral = null);

/// <summary>建表／欄位遷移的共用邏輯，供各 Feature 的 IFeatureDatabaseInitializer 實作呼叫。</summary>
public static class DatabaseBootstrap
{
    public static async Task EnsureCreatedAsync<TContext>(IDbContextFactory<TContext> factory) where TContext : DbContext
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        var script = db.Database.GenerateCreateScript().Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ");
        await db.Database.ExecuteSqlRawAsync(script);
    }

    /// <summary>既有表補欄位。DefaultLiteral 有值時補上 SQLite 的 DEFAULT 子句，讓既有列同步回填，
    /// 不是只讓「之後新寫入的列」有預設值——C# 端非 nullable 型別的屬性，讀到既有列的 NULL 會直接炸例外。</summary>
    public static async Task EnsureColumnExistsAsync<TContext>(IDbContextFactory<TContext> factory, ColumnMigration migration)
        where TContext : DbContext
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var checkCmd = db.Database.GetDbConnection().CreateCommand();
        checkCmd.CommandText = $"PRAGMA table_info({migration.Table})";
        await db.Database.OpenConnectionAsync();
        await using var reader = await checkCmd.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), migration.Column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        await reader.DisposeAsync();
        if (!exists)
        {
            var defaultClause = migration.DefaultLiteral is null ? "" : string.Concat(" DEFAULT '", migration.DefaultLiteral, "'");
            var alterSql = string.Concat("ALTER TABLE ", migration.Table, " ADD COLUMN ", migration.Column, " ", migration.SqlType, defaultClause);
            await db.Database.ExecuteSqlRawAsync(alterSql);
        }

        // 保險回填：即使欄位早就存在，也可能是先前某次啟動在補上 DEFAULT 子句之前就已經新增過，留下 NULL 值
        if (migration.DefaultLiteral is not null)
        {
            var backfillSql = string.Concat("UPDATE ", migration.Table, " SET ", migration.Column, " = '", migration.DefaultLiteral, "' WHERE ", migration.Column, " IS NULL");
            await db.Database.ExecuteSqlRawAsync(backfillSql);
        }
    }
}
