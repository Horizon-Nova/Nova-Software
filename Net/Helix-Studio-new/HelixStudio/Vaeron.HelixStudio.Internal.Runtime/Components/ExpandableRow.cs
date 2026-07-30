namespace Vaeron.HelixStudio.Internal.Runtime.Components;

/// <summary>把「畫面上同時只有一列能展開、點擊已展開列會收合」這個各 Feature 各自用參照比對
/// （MultiBinding + ReferenceEqualsToVisibilityConverter）或各自土法重造的形狀，統一成跟
/// AppsManagement 的 ToolRow 同一種讀法：每一列自帶一個 bool IsExpanded，XAML 端一律用
/// DataTrigger 讀這個屬性（見 Controls.xaml 的 ExpandableDataGridRowStyle），不再需要
/// 逐頁各自定義比對邏輯或轉換器。</summary>
public sealed record ExpandableRow<TItem>(TItem Item, bool IsExpanded);

public static class ExpandableRows
{
    /// <summary>依目前展開中的 key 重建整批列——跟 AppsManagement.ApplyBuildRows 同一種「整批重建」
    /// 而非逐列原地修改，因為列的來源快照本身多半是不可變 record。</summary>
    public static IReadOnlyList<ExpandableRow<TItem>> Build<TItem, TKey>(
        IEnumerable<TItem> items, TKey? expandedKey, Func<TItem, TKey> keySelector)
        => items.Select(i => new ExpandableRow<TItem>(i, expandedKey is not null && Equals(expandedKey, keySelector(i)))).ToList();

    /// <summary>點擊同一列收合、點擊別列切換過去——跟 AppsManagement.ApplyToggleExpand 同一種判斷。
    /// 用 object.Equals 而非 TKey : IEquatable&lt;TKey&gt; 限制式——後者搭配無 class/struct 限制的
    /// TKey? 在明確指定型別引數（例如 Toggle&lt;int&gt;）時，Roslyn 對 Nullable&lt;TKey&gt; 的解析會出錯
    /// （CS1503：int? 無法轉成 int），object.Equals 對 null 安全、對任何型別都成立，直接繞開這個問題。</summary>
    public static TKey? Toggle<TKey>(TKey? currentExpandedKey, TKey clickedKey)
        => Equals(currentExpandedKey, clickedKey) ? default : clickedKey;
}
