using System;
using System.Windows;
using System.Windows.Controls;

namespace Helix_Studio.Components;

/// <summary>模組頁用之 Markdown README 顯示元件。</summary>
public partial class ReadmeViewerDialog : UserControl
{
    /// <summary>識別 <see cref="MarkdownText"/> 相依性屬性。</summary>
    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(
            nameof(MarkdownText),
            typeof(string),
            typeof(ReadmeViewerDialog),
            new PropertyMetadata(string.Empty, OnMarkdownStateChanged));

    /// <summary>識別 <see cref="AssetPathRoot"/> 相依性屬性。</summary>
    public static readonly DependencyProperty AssetPathRootProperty =
        DependencyProperty.Register(
            nameof(AssetPathRoot),
            typeof(string),
            typeof(ReadmeViewerDialog),
            new PropertyMetadata(string.Empty, OnMarkdownStateChanged));

    /// <summary>Markdown 來源文字。</summary>
    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    /// <summary>內嵌圖片等資源之根目錄路徑。</summary>
    public string AssetPathRoot
    {
        get => (string)GetValue(AssetPathRootProperty);
        set => SetValue(AssetPathRootProperty, value);
    }

    /// <summary>初始化並套用目前 Markdown 狀態。</summary>
    public ReadmeViewerDialog()
    {
        InitializeComponent();
        ApplyMarkdownState();
    }

    private static void OnMarkdownStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is ReadmeViewerDialog viewer)
        {
            viewer.ApplyMarkdownState();
        }
    }

    private void ApplyMarkdownState()
    {
        if (MarkdownHost is null)
        {
            return;
        }

        Visibility = string.IsNullOrWhiteSpace(MarkdownText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var assetPathRoot = string.IsNullOrWhiteSpace(AssetPathRoot)
            ? string.Empty
            : AppendDirectorySeparator(AssetPathRoot);

        MarkdownHost.AssetPathRoot = assetPathRoot;
        MarkdownHost.BaseUri = new Uri(
            string.IsNullOrEmpty(assetPathRoot) ? "file:///" : assetPathRoot,
            UriKind.Absolute);
        MarkdownHost.Markdown = MarkdownText ?? string.Empty;
    }

    private static string AppendDirectorySeparator(string path)
    {
        var lastChar = path[^1];
        return lastChar == System.IO.Path.DirectorySeparatorChar || lastChar == System.IO.Path.AltDirectorySeparatorChar
            ? path
            : path + System.IO.Path.DirectorySeparatorChar;
    }
}

