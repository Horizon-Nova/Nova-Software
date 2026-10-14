using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace HelixStudio.Pack;

/// <summary>把工具打包成平台收得了的套件</summary>
/// <remarks>
/// 套件是一個 zip：根目錄一個 manifest.json 說明這支工具，files/ 底下是它的每一個檔案，
/// signature.json 是對前面兩者的 RSA 簽章。驗章只需要公鑰，不連任何伺服器。
/// </remarks>
public static class Packer
{
    #region 產生一組簽章金鑰

    /// <summary>產生一組簽章金鑰：私鑰留著簽套件，公鑰貼進平台</summary>
    /// <param name="folder">兩個檔案要放到哪一個資料夾</param>
    public static string Key(string folder)
    {
        // 這一組金鑰
        using var rsa = RSA.Create(Bits);

        // 私鑰：只有簽套件的人拿得到
        var secret = Path.Combine(folder, "helix-signing-key.pem");

        Directory.CreateDirectory(folder);
        File.WriteAllText(secret, rsa.ExportPkcs8PrivateKeyPem());

        // 公鑰：平台拿它驗章，貼成一行 base64
        var open = Path.Combine(folder, "helix-public-key.txt");

        File.WriteAllText(open, Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));

        return $"私鑰寫到 {secret}{Environment.NewLine}"
             + $"公鑰寫到 {open}{Environment.NewLine}"
             + $"公鑰內容：{Environment.NewLine}{Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())}";
    }

    #endregion

    #region 打包並簽章

    /// <summary>把一個資料夾打包成簽章過的套件</summary>
    /// <param name="folder">裡面要有 manifest.json，其餘檔案都是這支工具的內容</param>
    /// <param name="target">產生的套件檔要放哪裡</param>
    /// <param name="secret">私鑰檔（PEM）</param>
    public static string Pack(string folder, string target, string secret)
    {
        // 這個資料夾裡的每一個檔案，manifest.json 單獨一份，其餘都是工具的內容
        var manifest = Path.Combine(folder, Manifest);
        var payload = Directory
            .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(one => !string.Equals(one, manifest, StringComparison.OrdinalIgnoreCase))
            .OrderBy(one => one, StringComparer.Ordinal)
            .ToList();

        // 套件裡每一筆的名字與內容；名字一律用正斜線，換一台機器打包出來還是一樣
        var entries = Enumerable
            .Repeat(new Entry { Name = Manifest, Bytes = File.ReadAllBytes(manifest) }, 1)
            .Concat(payload.Select(one => new Entry
            {
                Name = Files + Path.GetRelativePath(folder, one).Replace('\\', '/'),
                Bytes = File.ReadAllBytes(one),
            }))
            .OrderBy(one => one.Name, StringComparer.Ordinal)
            .ToList();

        // 把每一筆的名字與內容接起來雜湊：名字改了、內容改了，值就不一樣
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        entries.ForEach(one =>
        {
            digest.AppendData(Encoding.UTF8.GetBytes(one.Name));
            digest.AppendData(Separator);
            digest.AppendData(one.Bytes);
        });

        var hash = digest.GetHashAndReset();

        // 用私鑰簽這個雜湊值
        using var rsa = RSA.Create();

        rsa.ImportFromPem(File.ReadAllText(secret));

        var sign = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // 寫成套件檔：先寫內容，最後一筆才是簽章
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)) ?? ".");
        File.Delete(target);

        using var zip = ZipFile.Open(target, ZipArchiveMode.Create);

        entries.ForEach(one =>
        {
            using var into = zip.CreateEntry(one.Name, CompressionLevel.Optimal).Open();

            into.Write(one.Bytes);
        });

        using var signature = zip.CreateEntry(Signature, CompressionLevel.Optimal).Open();

        signature.Write(Encoding.UTF8.GetBytes(
            $"{{\"alg\":\"RS256\",\"digest\":\"{Convert.ToBase64String(hash)}\",\"signature\":\"{Convert.ToBase64String(sign)}\"}}"));

        return $"打包好了：{target}（{entries.Count} 個檔案，簽章 {sign.Length * 8} 位元）";
    }

    #endregion

    #region 對照

    /// <summary>怎麼用</summary>
    public const string Usage = """
        helixpack key  [資料夾]                    產生一組簽章金鑰
        helixpack pack <來源資料夾> <套件檔> <私鑰>   把資料夾打包成簽章過的套件
        """;

    /// <summary>金鑰幾位元</summary>
    private const int Bits = 2048;

    /// <summary>說明檔在套件裡叫什麼</summary>
    public const string Manifest = "manifest.json";

    /// <summary>簽章在套件裡叫什麼</summary>
    public const string Signature = "signature.json";

    /// <summary>工具的檔案放在套件裡的哪一層</summary>
    public const string Files = "files/";

    /// <summary>名字與內容之間的分隔，免得兩筆接起來剛好一樣</summary>
    private static readonly byte[] Separator = [0];

    #endregion

    #region 模型

    /// <summary>套件裡的一筆</summary>
    private class Entry
    {
        /// <summary>這一筆在套件裡叫什麼</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>這一筆的內容</summary>
        public byte[] Bytes { get; set; } = [];
    }

    #endregion
}
