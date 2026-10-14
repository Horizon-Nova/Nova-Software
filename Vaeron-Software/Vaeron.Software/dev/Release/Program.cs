using System.Text;
using HelixStudio.Release;

Console.OutputEncoding = new UTF8Encoding(false);

// 發佈命令列指名的那一個產品；停下來的原因照實印出來
try
{
    new Publisher().Publish(args.FirstOrDefault() ?? string.Empty);

    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"發佈停下來了：{error.Message}");

    return 1;
}
