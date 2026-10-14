using System.Globalization;
using System.Text;

namespace Vaeron.VNCSCP.Internal.Rfb;

/// <summary>逐步交握診斷。每行前綴當地時間戳。</summary>
public sealed class RfbTrace
{
    private readonly StringBuilder _lines = new();

    public string Text => _lines.ToString();

    public void Write(string line) => _lines
        .Append('[')
        .Append(DateTimeOffset.UtcNow.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
        .Append("] ")
        .AppendLine(line);
}
