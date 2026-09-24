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

        // 授權檔讀得出來就攤開它
        var papers = text
            .Select(one => Try(() => JsonSerializer.Deserialize<LicenceItem>(
                Encoding.UTF8.GetString(Convert.FromBase64String(one.Trim())))))
            .Where(one => one is not null)
            .ToList();

        view.Papers.AddRange(papers!);

        // 驗簽章：客戶、日期、工具清單、機器資訊，任何一項被改過就對不上
        using var rsa = RSA.Create();

        rsa.ImportSubjectPublicKeyInfo(Anchors.Licence(), out _);

        view.Sealed = papers
            .Select(one => Try(() => rsa.VerifyHash(
                SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(one!))),
                Convert.FromBase64String($"{one!.Signature}"),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)))
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

    /// <summary>做一件可能失敗的事，失敗就當作沒有結果</summary>
    /// <param name="work">要做的那一件事</param>
    private static T? Try<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception)
        {
            return default;
        }
    }

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
