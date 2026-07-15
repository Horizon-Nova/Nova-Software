using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Features.AppLibrary;

/// <summary>把匯入工具的 README 純文字（## 標題／- 條列／```程式碼區塊``` 這種簡化 markdown
/// 語法）解析成有版面層次的 FlowDocument——之前直接把整段原始文字塞進一個 TextBlock，
/// "## 標題"、"- 條列" 這些語法符號會照字面顯示出來，不是真的呈現成標題/清單，
/// 對齊 blueprint LibraryScreen.dc.html 的 parseReadme／buildReadmeSections 行為。
/// 只支援這三種區塊，不是完整 markdown 規格——README 內容目前只會用到這幾種寫法
/// （見 seed/library.js 的既有 readme 範例），沒有必要做一個完整的 markdown 剖析器。</summary>
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
