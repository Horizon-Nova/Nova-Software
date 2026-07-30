using System.Windows;

namespace Vaeron.HelixStudio.Internal.Runtime.Components;

/// <summary>單一佔位色塊（骨架屏一格內可以放多個，例如「操作」欄的兩顆按鈕佔位）。</summary>
public sealed class TableSkeletonChip
{
    public double Width { get; init; } = 60;
    public double Height { get; init; } = 11;
    public double CornerRadius { get; init; } = 6;
    public Thickness Margin { get; init; } = new(8, 0, 0, 0);
}

/// <summary>TableSkeletonView 的欄位宣告</summary>
public sealed class TableSkeletonColumn
{
    public GridLength Width { get; init; } = new(100);
    public string? Header { get; init; }
    public HorizontalAlignment Alignment { get; init; } = HorizontalAlignment.Left;
    public Thickness HeaderMargin { get; init; } = new(8, 9, 8, 9);
    public IReadOnlyList<TableSkeletonChip> Chips { get; init; } = [];
}
