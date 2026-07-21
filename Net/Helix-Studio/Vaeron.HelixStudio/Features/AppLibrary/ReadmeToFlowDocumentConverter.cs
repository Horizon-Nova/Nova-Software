using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Features.AppLibrary;

/// <summary>README 簡化 markdown（標題／條列／程式碼區塊）轉 FlowDocument</summary>
public sealed class ReadmeToFlowDocumentConverter : IValueConverter
{
    /// <summary>FlowDocument 是程式建構的，拿不到 DynamicResource，改由這裡向樣式庫查鍵；
    /// 查不到才退回型別預設值，避免主題缺鍵時整份文件建不出來。</summary>
    private static T Token<T>(string key, T fallback)
        => System.Windows.Application.Current?.TryFindResource(key) is T value ? value : fallback;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var document = new FlowDocument
        {
            FontFamily = Token("AppFontFamily", new FontFamily("Noto Sans TC")),
            FontSize = Token<double>("FontSizeLg", 12),
        };
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            document.Blocks.Add(new Paragraph(new Run("（此工具沒有提供說明文件）"))
            {
                Foreground = Token<Brush>("TextMutedBrush", Brushes.Gray),
            });
            return document;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        List? currentList = null;
        var inCodeBlock = false;
        var codeBuffer = new List<string>();

        void FlushList()
        {
            if (currentList is null) return;
            document.Blocks.Add(currentList);
            currentList = null;
        }

        void FlushCode()
        {
            if (codeBuffer.Count == 0) return;
            var codeParagraph = new Paragraph(new Run(string.Join('\n', codeBuffer)))
            {
                FontFamily = Token("MonoFontFamily", new FontFamily("Consolas")),
                Background = Token<Brush>("CodeBlockBgBrush", Brushes.WhiteSmoke),
                Padding = new System.Windows.Thickness(8),
                Margin = new System.Windows.Thickness(0, 4, 0, 4),
            };
            document.Blocks.Add(codeParagraph);
            codeBuffer.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("```"))
            {
                if (inCodeBlock) { FlushCode(); inCodeBlock = false; }
                else { FlushList(); inCodeBlock = true; }
                continue;
            }
            if (inCodeBlock) { codeBuffer.Add(rawLine); continue; }

            if (line.StartsWith("## "))
            {
                FlushList();
                document.Blocks.Add(new Paragraph(new Run(line[3..]))
                {
                    FontWeight = Token("FontWeightStrong", System.Windows.FontWeights.Bold),
                    FontSize = Token<double>("FontSizeH1", 14),
                    Margin = new System.Windows.Thickness(0, 12, 0, 4),
                });
                continue;
            }

            if (line.StartsWith("- "))
            {
                currentList ??= new List { MarkerStyle = System.Windows.TextMarkerStyle.Disc, Margin = new System.Windows.Thickness(0, 2, 0, 2) };
                currentList.ListItems.Add(new ListItem(new Paragraph(new Run(line[2..]))));
                continue;
            }

            FlushList();
            if (!string.IsNullOrWhiteSpace(line))
                document.Blocks.Add(new Paragraph(new Run(line)) { Margin = new System.Windows.Thickness(0, 2, 0, 2) });
        }
        FlushList();
        FlushCode();

        return document;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
