using System.Text;

namespace Helix_Studio.Infrastructure.Utilities;

/// <summary>極簡 INI 格式之剖析與序列化（無巢狀區段以外之進階語法）。</summary>
public static class IniDocumentUtility
{
    /// <summary>將 INI 文字剖析為區段名稱對應鍵值字典。</summary>
    public static Dictionary<string, Dictionary<string, string>> Parse(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        foreach (var rawLine in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var sectionName = line[1..^1].Trim();
                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[sectionName] = currentSection;
                }

                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || currentSection is null)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            currentSection[key] = value;
        }

        return sections;
    }

    /// <summary>將區段與鍵值序列化為 INI 文字。</summary>
    public static string Serialize(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sections)
    {
        var builder = new StringBuilder();
        var isFirstSection = true;

        foreach (var section in sections)
        {
            if (!isFirstSection)
            {
                builder.AppendLine();
            }

            builder.Append('[').Append(section.Key).AppendLine("]");
            foreach (var pair in section.Value)
            {
                builder.Append(pair.Key).Append('=').AppendLine(pair.Value);
            }

            isFirstSection = false;
        }

        return builder.ToString();
    }
}
