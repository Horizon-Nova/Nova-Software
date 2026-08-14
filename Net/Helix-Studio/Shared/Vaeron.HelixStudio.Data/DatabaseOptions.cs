namespace Vaeron.HelixStudio.Data;

/// <summary>公開表面：資料庫檔案名稱。目錄由 Paths.Local 決定，不在此重複。</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string FileName { get; init; } = "helix-studio.db";
}
