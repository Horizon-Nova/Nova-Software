using System.Text;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

namespace DbTools;

internal class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var auditTag = DbTools.Internal.InternalAuditVerifier.GetInternalHash();
        Console.WriteLine(auditTag);

        PrintHeader();

        #region 參數解析

        string? folderArg = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--folder", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                folderArg = args[++i];
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(folderArg))
        {
            PrintHelp();
            return 1;
        }

        #endregion

        #region 找到 appsettings.json

        var sqlFolder = Path.GetFullPath(folderArg);

        if (!Directory.Exists(sqlFolder))
        {
            Console.WriteLine($"[失敗] 找不到資料夾：{sqlFolder}");
            return 1;
        }

        var appSettingsPath = FindAppSettings(sqlFolder);

        if (appSettingsPath is null)
        {
            Console.WriteLine("[失敗] 找不到 appsettings.json，請確認資料夾路徑正確。");
            return 1;
        }

        var appSettingsDir = Path.GetDirectoryName(appSettingsPath)!;

        Console.WriteLine($"[設定] {appSettingsPath}");
        Console.WriteLine($"[SQL 資料夾] {sqlFolder}");
        Console.WriteLine();

        var cfg = new ConfigurationBuilder()
            .SetBasePath(appSettingsDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connMap = cfg.GetSection("ConnectionStrings")
            .GetChildren()
            .ToDictionary(
                c => c.Key.Trim(),
                c => c.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        if (connMap.Count == 0)
        {
            Console.WriteLine("[失敗] appsettings.json 缺少 ConnectionStrings。");
            return 1;
        }

        #endregion

        #region 執行 SQL

        Console.WriteLine("════════════════════════════════════════════════════════════════");
        Console.WriteLine("[DbTools] 階段一：執行 SQL");
        Console.WriteLine("════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        var sqlFiles = Directory.GetFiles(sqlFolder, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (sqlFiles.Count == 0)
        {
            Console.WriteLine("[警告] sql 資料夾內沒有 .sql 檔案。");
        }
        else
        {
            foreach (var sqlFile in sqlFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(sqlFile);
                var dbKey = ExtractDbKey(fileName);

                // 000_init.sql 跑在所有連線
                if (string.Equals(dbKey, "init", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Init] {Path.GetFileName(sqlFile)} → 套用至所有資料庫");
                    foreach (var kv in connMap)
                    {
                        ExecuteSqlFile(sqlFile, kv.Key, kv.Value);
                    }
                    continue;
                }

                // 其他 SQL 檔依資料庫名稱對應連線
                if (!connMap.TryGetValue(dbKey, out var conn) || string.IsNullOrWhiteSpace(conn))
                {
                    Console.WriteLine($"[警告] {Path.GetFileName(sqlFile)} → 找不到對應連線 [{dbKey}]，跳過");
                    continue;
                }

                ExecuteSqlFile(sqlFile, dbKey, conn);
            }
        }

        #endregion

        #region 執行 BuildModels

        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════════════════════");
        Console.WriteLine("[DbTools] 階段二：BuildModels");
        Console.WriteLine("════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        var outputRootSetting = cfg["ScaffoldSettings:ModelOutputRoot"];
        var rawTablesMap = cfg.GetSection("ScaffoldSettings:Tables")
            .Get<Dictionary<string, string[]>>() ?? new();

        if (string.IsNullOrWhiteSpace(outputRootSetting) || rawTablesMap.Count == 0)
        {
            Console.WriteLine("[警告] 缺少 ScaffoldSettings，略過 BuildModels。");
            Console.WriteLine("[DbTools] 完成");
            return 0;
        }

        var outputRoot = Path.IsPathRooted(outputRootSetting)
            ? Path.GetFullPath(outputRootSetting)
            : Path.GetFullPath(Path.Combine(appSettingsDir, outputRootSetting));

        Console.WriteLine($"[Model 輸出] {outputRoot}");
        Console.WriteLine();

        var tablesMap = NormalizeTablesMap(rawTablesMap);

        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);

        using var provider = services.BuildServiceProvider();
        var scaffolder = provider.GetRequiredService<IReverseEngineerScaffolder>();

        foreach (var kv in tablesMap)
        {
            var key = kv.Key;
            var tables = kv.Value;

            if (!connMap.TryGetValue(key, out var conn) || string.IsNullOrWhiteSpace(conn))
            {
                Console.WriteLine($"[Skip] {key} 無對應連線字串");
                continue;
            }

            var outDir = Path.Combine(outputRoot, key);
            Directory.CreateDirectory(outDir);

            Console.WriteLine($"=> 產生 {key}（{tables.Length} 張表）");
            Console.WriteLine("   表清單: " + string.Join(", ", tables));
            Console.WriteLine("   輸出: " + outDir);

            var model = scaffolder.ScaffoldModel(
                connectionString: conn,
                databaseOptions: new DatabaseModelFactoryOptions(tables, Array.Empty<string>()),
                modelOptions: new ModelReverseEngineerOptions
                {
                    UseDatabaseNames = true
                },
                codeOptions: new ModelCodeGenerationOptions
                {
                    ContextName = $"{key}DbContext",
                    ContextDir = ".",
                    ModelNamespace = $"Models.{key}",
                    ContextNamespace = $"Models.{key}",
                    SuppressOnConfiguring = true,
                    SuppressConnectionStringWarning = true,
                    UseDataAnnotations = true,
                    UseNullableReferenceTypes = true
                });

            scaffolder.Save(model, outDir, overwriteFiles: true);
            AddWarningCommentsToGeneratedFiles(outDir);
        }

        #endregion

        Console.WriteLine();
        Console.WriteLine("[DbTools] 完成");
        return 0;
    }

    #region SQL 執行

    private static void ExecuteSqlFile(string filePath, string dbKey, string connectionString)
    {
        Console.WriteLine($"[SQL] {Path.GetFileName(filePath)} → [{dbKey}]");

        string sql;
        try
        {
            sql = File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   [失敗] 讀取檔案失敗：{ex.Message}");
            return;
        }

        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.ExecuteNonQuery();

            Console.WriteLine($"   [成功]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   [失敗] {ex.Message}");
        }
    }

    private static string ExtractDbKey(string fileNameWithoutExtension)
    {
        // 去掉編號前綴，例如 001_weather_logs → weather_logs
        var parts = fileNameWithoutExtension.Split('_', 2);
        return parts.Length > 1 ? parts[1] : fileNameWithoutExtension;
    }

    #endregion

    #region BuildModels 輔助

    private static Dictionary<string, string[]> NormalizeTablesMap(Dictionary<string, string[]> rawTablesMap)
    {
        var tablesMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in rawTablesMap)
        {
            var key = (kv.Key ?? string.Empty).Trim();
            var list = (kv.Value ?? Array.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (!string.IsNullOrWhiteSpace(key) && list.Length > 0)
                tablesMap[key] = list;
        }

        return tablesMap;
    }

    private static void AddWarningCommentsToGeneratedFiles(string directory)
    {
        const string marker = "AUTO-GENERATED by DbTools";

        var warningComment = $@"// ╔════════════════════════════════════════════════════════════════════════╗
// ║                     嚴重警告 / CRITICAL WARNING                        ║
// ╠════════════════════════════════════════════════════════════════════════╣
// ║  此檔案為系統自動生成，禁止手動修改！                                    ║
// ║  This file is {marker}. DO NOT MODIFY manually!                       ║
// ║                                                                          ║
// ║  任何手動修改將在下次重新生成時被覆蓋且遺失！                            ║
// ║  Any manual changes will be OVERWRITTEN and LOST on next generation!     ║
// ║                                                                          ║
// ║  如需修改，請調整資料庫結構後重新執行 DbTools 工具。                     ║
// ╚════════════════════════════════════════════════════════════════════════╝

";

        foreach (var filePath in Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var content = File.ReadAllText(filePath);
                if (!content.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(filePath, warningComment + content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   [警告] 無法處理 {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }

    #endregion

    #region 輔助

    private static string? FindAppSettings(string startFolder)
    {
        // 從 sql/ 資料夾往上找 appsettings.json
        for (var dir = new DirectoryInfo(startFolder); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static void PrintHeader()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    DbTools - PostgreSQL                       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("使用方式：");
        Console.WriteLine("  DbTools --folder <sql資料夾路徑>");
        Console.WriteLine();
        Console.WriteLine("範例：");
        Console.WriteLine("  DbTools --folder Vaeron.WebApp/sql/");
        Console.WriteLine();
        Console.WriteLine("說明：");
        Console.WriteLine("  工具會自動往上找 appsettings.json");
        Console.WriteLine("  SQL 檔案命名規則：000_init.sql / 001_<db名稱>.sql");
        Console.WriteLine("  000_init.sql 套用至所有資料庫");
        Console.WriteLine("  其他 SQL 依檔名對應 ConnectionStrings key");
    }

    #endregion
}
