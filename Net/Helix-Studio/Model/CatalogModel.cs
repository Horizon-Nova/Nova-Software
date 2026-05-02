namespace Helix_Studio.Model.Catalog;

/// <summary>目錄中單一條目之中繼資料（標題等以資源鍵表示）。</summary>
public sealed class CatalogEntry
{
    /// <summary>條目識別碼。</summary>
    public required string Id { get; init; }

    /// <summary>標題之資源鍵。</summary>
    public required string TitleResourceKey { get; init; }

    /// <summary>說明之資源鍵。</summary>
    public required string DescriptionResourceKey { get; init; }

    /// <summary>狀態文字之資源鍵。</summary>
    public required string StatusResourceKey { get; init; }
}

/// <summary>目錄頁面描述：路由與所含條目集合。</summary>
public sealed class CatalogPageDescriptor
{
    /// <summary>頁面路由識別。</summary>
    public required string Route { get; init; }

    /// <summary>頁面標題之資源鍵。</summary>
    public required string TitleResourceKey { get; init; }

    /// <summary>頁面說明之資源鍵。</summary>
    public required string DescriptionResourceKey { get; init; }

    /// <summary>頁面所含目錄條目清單。</summary>
    public required IReadOnlyList<CatalogEntry> Entries { get; init; }
}
