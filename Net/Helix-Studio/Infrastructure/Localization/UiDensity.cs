using System.Windows;

namespace Helix_Studio.Infrastructure.Localization;

/// <summary>資料密度（列高）之應用程式資源鍵與套用邏輯。</summary>
public static class UiDensity
{
    /// <summary>DataGrid 表頭高度之資源鍵。</summary>
    public const string DataGridHeaderHeightKey = "UiDataGridHeaderHeight";

    /// <summary>DataGrid 資料列高度之資源鍵。</summary>
    public const string DataGridRowHeightKey = "UiDataGridRowHeight";

    /// <summary>目前選取之密度索引（0 標準、1 緊湊）。</summary>
    public static int CurrentIndex { get; private set; } = 0;

    /// <summary>依索引套用資料密度並寫入應用程式資源。</summary>
    public static void ApplyByIndex(int densityIndex)
    {
        CurrentIndex = densityIndex == 1 ? 1 : 0;

        var headerHeight = CurrentIndex == 1 ? 28 : 32;
        var rowHeight = CurrentIndex == 1 ? 28 : 34;

        Application.Current!.Resources[DataGridHeaderHeightKey] = (double)headerHeight;
        Application.Current!.Resources[DataGridRowHeightKey] = (double)rowHeight;
    }
}
