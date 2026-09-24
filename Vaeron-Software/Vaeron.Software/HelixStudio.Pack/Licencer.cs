using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HelixStudio.Pack;

/// <summary>發授權：把客戶送來的申請碼變成一把綁那台機器的 KEY</summary>
/// <remarks>授權與套件用不同的金鑰：套件私鑰外流不等於可以發授權，反過來也一樣。</remarks>
public static class Licencer
{
    #region 產生一組發證金鑰

    /// <summary>產生一組發證金鑰：私鑰留著發授權，公鑰貼進平台</summary>
    /// <param name="folder">兩個檔案要放到哪一個資料夾</param>
    public static string Key(string folder)
    {
        using var rsa = RSA.Create(Bits);

        var secret = Path.Combine(folder, "helix-licence-key.pem");
        var open = Path.Combine(folder, "helix-licence-public.txt");

        Directory.CreateDirectory(folder);
        File.WriteAllText(secret, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(open, Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));

        return $"發證私鑰寫到 {secret}{Environment.NewLine}"
             + $"發證公鑰寫到 {open}{Environment.NewLine}{Environment.NewLine}"
             + Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    #endregion

    #region 看一張申請碼裡面是什麼

    /// <summary>把客戶送來的申請碼攤開來看</summary>
    /// <param name="code">申請碼</param>
    public static string Read(string code)
    {
        var ask = Ask(code);

        return $"這台機器讀得到 {ask.Marks.Count} 項："
             + string.Concat(ask.Marks.Select(one => $"{Environment.NewLine}  {one.Kind}　{one.Value}"));
    }

    #endregion

    #region 發一把授權

    /// <summary>照申請碼發一把綁那台機器的授權</summary>
    /// <param name="code">客戶送來的申請碼</param>
    /// <param name="secret">發證私鑰（PEM）</param>
    /// <param name="customer">賣給誰</param>
    /// <param name="tools">准裝哪幾支工具，逗號隔開；空的就是全部</param>
    /// <param name="until">到期日 yyyy-MM-dd；空的就是永久</param>
    public static string Issue(string code, string secret, string customer, string tools, string until)
    {
        // 申請碼裡那台機器量到的每一項，原樣抄進授權
        var ask = Ask(code);

        var licence = new LicenceItem
        {
            Customer = customer,
            Issued = DateTime.Now.ToString("yyyy-MM-dd"),
            Expires = until,
            Tools = tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Marks = ask.Marks,
        };

        // 簽這張授權：客戶、日期、工具清單、機器資訊，任何一項改過簽章就對不上
        using var rsa = RSA.Create();

        rsa.ImportFromPem(File.ReadAllText(secret));

        licence.Signature = Convert.ToBase64String(rsa.SignHash(
            SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(licence))),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // KEY 就是整張授權壓成一段文字
        var key = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(licence)));

        return $"客戶　　{customer}{Environment.NewLine}"
             + $"發證日　{licence.Issued}{Environment.NewLine}"
             + $"到期日　{Empty(until, "永久")}{Environment.NewLine}"
             + $"准裝　　{Empty(string.Join("、", licence.Tools), "全部工具")}{Environment.NewLine}"
             + $"綁定　　{licence.Marks.Count} 項機器資訊{Environment.NewLine}{Environment.NewLine}"
             + $"把下面這一整段給客戶：{Environment.NewLine}{key}";
    }

    #endregion

    #region 對照

    /// <summary>怎麼用</summary>
    public const string Usage = """
        helixkey key   [資料夾]                                 產生一組發證金鑰
        helixkey read  <申請碼>                                 看申請碼裡面是什麼
        helixkey issue <申請碼> <發證私鑰> <客戶> [工具清單] [到期日]   發一把授權
        """;

    /// <summary>金鑰幾位元</summary>
    private const int Bits = 2048;

    /// <summary>空字串就改寫成另一句話</summary>
    /// <param name="value">原來的值</param>
    /// <param name="instead">空的時候寫什麼</param>
    private static string Empty(string value, string instead) =>
        new[] { value }.Where(one => one.Length > 0).DefaultIfEmpty(instead).First();

    /// <summary>把申請碼還原成那台機器量到的每一項</summary>
    /// <param name="code">申請碼</param>
    private static AskItem Ask(string code) =>
        JsonSerializer.Deserialize<AskItem>(
            Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim()))) ?? new AskItem();

    /// <summary>把一張授權攤成固定順序的一段文字，兩邊算出來的要一樣</summary>
    /// <param name="licence">那一張授權</param>
    public static string Canonical(LicenceItem licence) =>
        string.Join("\n",
            new[]
            {
                licence.Customer,
                licence.Issued,
                licence.Expires,
                string.Join(",", licence.Tools.OrderBy(one => one, StringComparer.Ordinal)),
            }
            .Concat(licence.Marks
                .OrderBy(one => one.Kind, StringComparer.Ordinal)
                .ThenBy(one => one.Value, StringComparer.Ordinal)
                .Select(one => $"{one.Kind}={one.Value}")));

    #endregion

    #region 模型

    /// <summary>客戶送來的申請碼裡面是什麼</summary>
    public class AskItem
    {
        /// <summary>那台機器量到的每一項</summary>
        public List<MarkItem> Marks { get; set; } = [];
    }

    /// <summary>一張授權</summary>
    public class LicenceItem
    {
        /// <summary>賣給誰</summary>
        public string Customer { get; set; } = string.Empty;

        /// <summary>發證日</summary>
        public string Issued { get; set; } = string.Empty;

        /// <summary>到期日；空的就是永久</summary>
        public string Expires { get; set; } = string.Empty;

        /// <summary>准裝哪幾支工具；空的就是全部</summary>
        public List<string> Tools { get; set; } = [];

        /// <summary>發證當時那台機器量到的每一項</summary>
        public List<MarkItem> Marks { get; set; } = [];

        /// <summary>這一張授權的簽章</summary>
        public string Signature { get; set; } = string.Empty;
    }

    /// <summary>機器身上的一項識別值</summary>
    public class MarkItem
    {
        /// <summary>這是哪一種</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>讀到的值</summary>
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
