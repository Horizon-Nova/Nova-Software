using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HelixStudio.Core;

namespace HelixStudio.Store;

/// <summary>這台機器的授權：讀授權檔、驗簽章、量現在的機器、算對得上幾項</summary>
/// <remarks>
/// 每次呼叫都從磁碟重讀、重算，不留結果——呼叫的每一處各自算一次，
/// 拿掉其中一處不會讓其他幾處跟著鬆掉。
/// </remarks>
public class LicenceStore
{
    #region 看這台機器現在的授權

    /// <summary>讀出這台機器現在的授權，並且跟眼前這台機器對一次</summary>
    public LicenceView Licence()
    {
        // 這台機器現在量得到的每一項
        var now = new DeviceStore().Marks();

        // 授權檔還沒有就只有申請碼，沒有別的
        var text = Enumerable.Repeat(Paper, 1).Where(File.Exists).Select(File.ReadAllText).ToList();

        var view = new LicenceView
        {
            Ask = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new AskItem { Marks = now.Select(one => new MarkItem { Kind = one.Kind, Value = one.Value }).ToList() }))),
            Marks = now.Select(one => new MarkItem { Kind = one.Kind, Value = one.Value }).ToList(),
        };

        // 授權檔是一段不是空的 Base64 就攤開它
        var papers = text
            .Select(one => one.Trim())
            .Where(one => one.Length > 0 && Convert.TryFromBase64String(one, new byte[one.Length], out _))
            .Select(one => JsonSerializer.Deserialize<LicenceItem>(Encoding.UTF8.GetString(Convert.FromBase64String(one))))
            .Where(one => one is not null)
            .ToList();

        view.Papers.AddRange(papers!);

        // 驗簽章：客戶、日期、工具清單、機器資訊，任何一項被改過就對不上
        using var rsa = RSA.Create();

        rsa.ImportSubjectPublicKeyInfo(Anchors.Licence(), out _);

        view.Sealed = papers
            .Select(one => rsa.VerifyHash(
                SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(one!))),
                Convert.FromBase64String($"{one!.Signature}"),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            .Where(one => one)
            .Count();

        // 對得上幾項：只比兩邊都有的那幾種，讀不到的不算在內
        var kinds = papers
            .SelectMany(one => one!.Marks.Select(two => two.Kind))
            .Intersect(now.Select(one => one.Kind))
            .ToList();

        view.Both = kinds.Count;
        view.Same = kinds
            .Count(kind => papers
                .SelectMany(one => one!.Marks)
                .Where(one => one.Kind == kind)
                .Select(one => one.Value)
                .Intersect(now.Where(one => one.Kind == kind).Select(one => one.Value))
                .Any());

        return view;
    }

    #endregion

    #region 看一把工具金鑰配不配得上這支工具

    /// <summary>驗一把工具金鑰：簽章、這台電腦、到期日、有沒有列這支工具；配得上回傳空字串，配不上回傳原因</summary>
    /// <param name="code">貼進來或存著的那一串</param>
    /// <param name="toolNo">要開或要解鎖的那一支工具</param>
    public string Fits(string code, string toolNo)
    {
        // 攤開：空的或不是一段 Base64 就是沒貼或貼錯了
        var plain = code.Trim();
        var paper = plain.Length > 0 && Convert.TryFromBase64String(plain, new byte[plain.Length], out _)
            ? JsonSerializer.Deserialize<LicenceItem>(Encoding.UTF8.GetString(Convert.FromBase64String(plain)))
            : null;

        // 驗簽章：用工具金鑰那一把公鑰，授權金鑰拿來貼會在這裡對不上
        using var rsa = RSA.Create();

        rsa.ImportSubjectPublicKeyInfo(Anchors.ToolKey(), out _);

        var sealedOk = paper is not null && rsa.VerifyHash(
            SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(paper))),
            Convert.FromBase64String(paper.Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // 這台電腦：只比兩邊都有的那幾種，每一種都要對得上
        var now = new DeviceStore().Marks();
        var kinds = (paper?.Marks ?? []).Select(one => one.Kind).Intersect(now.Select(one => one.Kind)).ToList();
        var same = kinds.Count(kind => (paper?.Marks ?? []).Where(one => one.Kind == kind).Select(one => one.Value)
            .Intersect(now.Where(one => one.Kind == kind).Select(one => one.Value)).Any());

        // 到期日：當天還能用；看不懂的到期日當作已經過期
        var today = DateOnly.FromDateTime(DateTime.Today);
        var alive = paper is not null && (paper.Expires.Length == 0 || (DateOnly.TryParse(paper.Expires, out var until) && until >= today));

        return new[]
            {
                (paper is null, "這一串不是工具金鑰，看不懂。整段重新複製貼上一次。"),
                (paper is not null && !sealedOk, "這把金鑰的簽章對不上：不是工具金鑰，或內容被改過。"),
                (sealedOk && (kinds.Count == 0 || same != kinds.Count), "這把工具金鑰不是發給這台電腦的。用這台電腦的申請碼重新申請。"),
                (sealedOk && !alive, $"這把工具金鑰在 {paper?.Expires} 到期了。"),
                (sealedOk && paper is not null && !paper.Tools.Contains(toolNo), $"這把工具金鑰沒有包含 {toolNo}。"),
            }
            .Where(one => one.Item1)
            .Select(one => one.Item2)
            .DefaultIfEmpty(string.Empty)
            .First();
    }

    #endregion

    #region 把客戶拿到的 KEY 存起來

    /// <summary>把一把 KEY 寫進這台機器</summary>
    /// <param name="key">客戶貼進來的那一段</param>
    public void Take(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paper)!);
        File.WriteAllText(Paper, key.Trim());
    }

    #endregion

    #region 對照

    /// <summary>授權檔放在哪裡</summary>
    private static string Paper { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HelixStudio", "licence.key");

    /// <summary>把一張授權攤成固定順序的一段文字，發證那一端算出來的要一樣</summary>
    /// <param name="licence">那一張授權</param>
    private static string Canonical(LicenceItem licence) =>
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

    /// <summary>要給賣方的申請碼內容：這台機器現在量到的每一項</summary>
    public class AskItem
    {
        /// <summary>機器身上量到的每一項</summary>
        public List<MarkItem> Marks { get; set; } = [];
    }

    /// <summary>這台機器現在的授權狀況</summary>
    public class LicenceView
    {
        /// <summary>要給賣方的申請碼</summary>
        public string Ask { get; set; } = string.Empty;

        /// <summary>這台機器現在量到的每一項</summary>
        public List<MarkItem> Marks { get; set; } = [];

        /// <summary>讀得出來的授權；沒有授權檔就是零筆</summary>
        public List<LicenceItem> Papers { get; set; } = [];

        /// <summary>簽章驗過的授權有幾張</summary>
        public int Sealed { get; set; }

        /// <summary>授權與這台機器兩邊都有的項目有幾種</summary>
        public int Both { get; set; }

        /// <summary>其中對得上的有幾種</summary>
        public int Same { get; set; }
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
