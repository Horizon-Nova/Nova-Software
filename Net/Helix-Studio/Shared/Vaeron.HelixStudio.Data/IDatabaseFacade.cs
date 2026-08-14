using Microsoft.Data.Sqlite;

namespace Vaeron.HelixStudio.Data;

/// <summary>跨功能單元往來的唯一入口。其他組件僅能經此取得資料庫連線。</summary>
public interface IDatabaseFacade
{
    /// <summary>資料庫檔案的完整路徑。</summary>
    public string FilePath { get; }

    /// <summary>開啟一條連線。呼叫端負責釋放。</summary>
    public SqliteConnection OpenConnection();

    /// <summary>套用尚未套用的結構指令碼，回傳本次套用的名稱。</summary>
    public IReadOnlyList<string> ApplySchema();
}
