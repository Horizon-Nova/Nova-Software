using System.Text;
using HelixStudio.Setup;

// 建置輸出用 UTF-8 讀這裡印的字
Console.OutputEncoding = new UTF8Encoding(false);

// 準備環境；停下來的原因用 MSBuild 認得的錯誤格式印，建置就停在這裡
try
{
    new Workspace().Prepare(args[0], args[1]);

    return 0;
}
catch (Exception error)
{
    Console.WriteLine($"Setup : error HX0001 : {error.Message}");

    return 1;
}
