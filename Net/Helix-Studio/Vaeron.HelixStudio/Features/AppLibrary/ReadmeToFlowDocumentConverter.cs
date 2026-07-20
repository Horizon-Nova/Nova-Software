using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Features.AppLibrary;

/// <summary>README 簡化 markdown（標題／條列／程式碼區塊）轉 FlowDocument</summary>
public sealed class ReadmeToFlowDocumentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var document = new FlowDocument { FontFamily = new FontFamily("Noto Sans TC"), FontSize = 12 };
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            document.Blocks.Add(new Paragraph(new Run("（此工具沒有提供說明文件）")) { Foreground = Brushes.Gray });
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
                FontFamily = new FontFamily("Consolas"),
                Background = Brushes.WhiteSmoke,
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
                    FontWeight = System.Windows.FontWeights.Bold,
                    FontSize = 14,
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
