using System.IO;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HelixStudio.Core;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>工具套件檔：看裡面有什麼、驗簽章、裝起來</summary>
/// <remarks>驗章只用內建的公鑰，不連任何伺服器；廠區沒有對外網路也驗得了。</remarks>
public class PackageStore
{
    /// <summary>要登錄工具時才連資料庫；只是看一個套件檔不必連</summary>
    private Lazy<HelixStudioDbContext> Connection { get; } = new(() => new HelixStudioDbContext(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options));

    private HelixStudioDbContext Db => Connection.Value;

    #region 看一個套件檔裡面是什麼

    /// <summary>打開一個套件檔，讀出它宣告了什麼，並且驗它的簽章</summary>
    /// <param name="path">套件檔在哪裡</param>
    public PackageItem Read(string path)
    {
        // 這個檔案本身：多大、叫什麼
        var file = new FileInfo(path);
        var item = new PackageItem { Path = path, Name = file.Name, Bytes = file.Length };

        // 當成 zip 打開；打不開就到此為止，後面的檢查都沒有東西可以查
        var opened = Enumerable
            .Repeat(path, 1)
            .Select(one => Try(() => ZipFile.OpenRead(one)))
            .ToList();

        item.Faults.AddRange(opened.Where(one => one.Value is null).Select(one =>
            $"這個檔案打不開：{one.Fault}。平台只收 .stpkg（公司簽章套件）；如果你拿到的是 .zip 或 .msi，請回報給資訊部重新包裝。"));

        using var zip = opened.Select(one => one.Value).FirstOrDefault();

        // 說明檔與簽章檔，兩者都要在
        var manifest = zip?.GetEntry(ManifestName);
        var signature = zip?.GetEntry(SignatureName);

        item.Faults.AddRange(Enumerable.Repeat($"套件裡沒有 {ManifestName}，看不出這是哪一支工具。", 1)
            .Where(_ => zip is not null && manifest is null));
        item.Faults.AddRange(Enumerable.Repeat("套件裡沒有簽章，無法確認它有沒有被改過。請向套件提供者索取正式簽章版本。", 1)
            .Where(_ => zip is not null && signature is null));

        // 說明檔的每一欄
        var declared = Enumerable
            .Repeat(manifest, 1)
            .Where(one => one is not null)
            .Select(one => Try(() => Bytes(one!)))
            .Select(one => Try(() => JsonSerializer.Deserialize<ManifestItem>(one.Value ?? [])))
            .ToList();

        item.Faults.AddRange(declared.Where(one => one.Value is null).Select(one => $"套件的說明檔讀不出來：{one.Fault}"));
        item.Manifests.AddRange(declared.Select(one => one.Value).Where(one => one is not null)!);

        // 算出這個套件現在的摘要值：每一筆的名字與內容接起來雜湊，名字或內容動過值就不一樣
        var entries = Enumerable.Repeat(zip, 1).Where(one => one is not null).SelectMany(one => one!.Entries)
            .Where(one => one.FullName != SignatureName)
            .OrderBy(one => one.FullName, StringComparer.Ordinal)
            .ToList();

        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        entries.ForEach(one =>
        {
            digest.AppendData(Encoding.UTF8.GetBytes(one.FullName));
            digest.AppendData(Separator);
            digest.AppendData(Bytes(one));
        });

        var hash = digest.GetHashAndReset();

        // 用內建的公鑰驗簽章
        using var rsa = RSA.Create();

        rsa.ImportSubjectPublicKeyInfo(Anchors.Package(), out _);

        var sealed_ = Enumerable
            .Repeat(signature, 1)
            .Where(one => one is not null)
            .Select(one => Try(() => JsonSerializer.Deserialize<SignatureItem>(Bytes(one!))))
            .Select(one => one.Value)
            .Where(one => one is not null)
            .Select(one => rsa.VerifyHash(hash, Convert.FromBase64String($"{one!.signature}"),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            .ToList();

        item.Faults.AddRange(sealed_.Where(one => !one).Select(_ =>
            "簽章驗證失敗：這個套件沒有資訊部簽章，或內容被改過。請向套件提供者索取正式簽章版本。"));

        // 說明檔要指得出載哪一支組件、哪一個畫面，而且那一支組件真的在套件裡
        item.Manifests.ForEach(one =>
        {
            item.Faults.AddRange(Enumerable.Repeat("說明檔沒有寫 entry（要載入的組件檔名）。", 1)
                .Where(_ => string.IsNullOrWhiteSpace(one.entry)));
            item.Faults.AddRange(Enumerable.Repeat("說明檔沒有寫 class（組件裡那個畫面的型別全名）。", 1)
                .Where(_ => string.IsNullOrWhiteSpace(one._class)));
            item.Faults.AddRange(Enumerable.Repeat($"套件裡找不到 {one.entry}。", 1)
                .Where(_ => !string.IsNullOrWhiteSpace(one.entry))
                .Where(_ => entries.All(two => two.FullName != Files + one.entry)));
        });

        return item;
    }

    #endregion

    #region 把一個套件裝起來

    /// <summary>把一個套件解開到這台裝置上，並登錄成一支工具</summary>
    /// <param name="item">已經讀過也驗過的那一個套件</param>
    /// <param name="source">這個套件從哪裡來</param>
    public string Install(PackageItem item, string source)
    {
        var manifest = item.Manifests.First();

        // 這支工具的檔案放在這裡：一支一個資料夾，版本各自一層，舊版還在就先清掉
        var home = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelixStudio", "tools", $"{manifest.tool_no}", $"{manifest.version}");

        Directory.CreateDirectory(home);
        Directory.EnumerateFiles(home).ToList().ForEach(File.Delete);

        // 把套件裡 files/ 底下的每一個檔案解出來
        using var zip = ZipFile.OpenRead(item.Path);

        zip.Entries
            .Where(one => one.FullName.StartsWith(Files, StringComparison.Ordinal))
            .Where(one => one.Length > 0)
            .ToList()
            .ForEach(one =>
            {
                var into = Path.Combine(home, one.FullName[Files.Length..].Replace('/', Path.DirectorySeparatorChar));

                Directory.CreateDirectory(Path.GetDirectoryName(into)!);
                one.ExtractToFile(into, true);
            });

        // 登錄成一支工具：已經有這個代碼就換成這一版，沒有就新增一列
        var rows = Db.tool_managements.Where(one => one.tool_no == manifest.tool_no).ToList();

        rows.ForEach(one => Fill(one, manifest, item, home, source));

        Db.tool_managements.AddRange(Enumerable
            .Repeat(new tool_management(), 1)
            .Where(_ => rows.Count == 0)
            .Select(one => Fill(one, manifest, item, home, source)));

        Db.SaveChanges();

        return $"{manifest.tool_no}";
    }

    /// <summary>把套件宣告的每一欄寫進這一列</summary>
    /// <param name="row">要寫的那一列</param>
    /// <param name="manifest">套件宣告了什麼</param>
    /// <param name="item">那一個套件檔</param>
    /// <param name="home">檔案解到哪一個資料夾</param>
    /// <param name="source">從哪裡來</param>
    private static tool_management Fill(tool_management row, ManifestItem manifest, PackageItem item,
        string home, string source)
    {
        row.type = "tool";
        row.tool_no = $"{manifest.tool_no}";
        row.name = $"{manifest.name}";
        row.icon = $"{manifest.icon}";
        row.cat = $"{manifest.cat}";
        row.vendor = $"{manifest.vendor}";
        row.version = $"{manifest.version}";
        row.installed_version = $"{manifest.version}";
        row.state = "installed";
        row.required = false;
        row.pinned = false;
        row.badge = 0;
        row.auto_update = false;
        row.audience = $"{manifest.audience}";
        row.size = Size(item.Bytes);
        row.updated = DateOnly.FromDateTime(DateTime.Now);
        row.description = $"{manifest.description}";
        row.seats = "{\"used\": 1, \"total\": 1}";
        row.capabilities = JsonSerializer.Serialize(manifest.capabilities);
        row.permissions = JsonSerializer.Serialize(manifest.permissions);
        row.changelog = JsonSerializer.Serialize(manifest.changelog);
        row.usage = "[]";
        row.entry = $"{manifest.entry}";
        row._class = $"{manifest._class}";
        row.home = home;
        row.source = source;
        row.updated_at = DateTime.UtcNow;

        return row;
    }

    #endregion

    #region 列出套件庫裡的每一個套件

    /// <summary>掃套件庫那一個資料夾，列出裡面每一個套件檔</summary>
    /// <param name="folder">套件庫資料夾</param>
    public IReadOnlyList<PackageItem> Library(string folder) =>
        Enumerable
            .Repeat(folder, 1)
            .Where(one => one.Length > 0)
            .Where(Directory.Exists)
            .SelectMany(one => Directory.EnumerateFiles(one, "*" + Extension, SearchOption.TopDirectoryOnly))
            .OrderBy(one => one, StringComparer.OrdinalIgnoreCase)
            .Select(Read)
            .ToList();

    #endregion

    #region 對照

    /// <summary>套件檔的副檔名</summary>
    public const string Extension = ".stpkg";


    /// <summary>說明檔在套件裡叫什麼</summary>
    private const string ManifestName = "manifest.json";

    /// <summary>簽章在套件裡叫什麼</summary>
    private const string SignatureName = "signature.json";

    /// <summary>工具的檔案放在套件裡的哪一層</summary>
    private const string Files = "files/";

    /// <summary>名字與內容之間的分隔</summary>
    private static readonly byte[] Separator = [0];

    /// <summary>幾位元組起算哪一個單位</summary>
    private static IReadOnlyList<KeyValuePair<long, string>> Units { get; } =
    [
        new(1, "B"),
        new(1024, "KB"),
        new(1024 * 1024, "MB"),
        new(1024L * 1024 * 1024, "GB"),
    ];

    /// <summary>幾位元組寫成人看得懂的大小</summary>
    /// <param name="bytes">幾位元組</param>
    public static string Size(long bytes)
    {
        var unit = Units.Last(one => bytes >= one.Key);

        return $"{bytes / (double)unit.Key:0.#} {unit.Value}";
    }

    /// <summary>把套件裡的一筆整個讀進記憶體</summary>
    /// <param name="entry">那一筆</param>
    private static byte[] Bytes(ZipArchiveEntry entry)
    {
        using var reading = entry.Open();
        using var buffer = new MemoryStream();

        reading.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>做一件可能失敗的事，把結果與失敗的原因一起帶回來</summary>
    /// <param name="work">要做的那一件事</param>
    private static Tried<T> Try<T>(Func<T> work)
    {
        try
        {
            return new Tried<T> { Value = work() };
        }
        catch (Exception fault)
        {
            return new Tried<T> { Fault = fault.Message };
        }
    }

    #endregion

    #region 模型

    /// <summary>一個套件檔現在的樣子</summary>
    public class PackageItem
    {
        /// <summary>這個檔案在哪裡</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>檔名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>幾位元組</summary>
        public long Bytes { get; set; }

        /// <summary>套件宣告了什麼；讀不出來就是零筆</summary>
        public List<ManifestItem> Manifests { get; set; } = [];

        /// <summary>沒通過的每一項檢查；零筆就是可以安裝</summary>
        public List<string> Faults { get; set; } = [];
    }

    /// <summary>套件的說明檔</summary>
    public class ManifestItem
    {
        /// <summary>工具代碼</summary>
        public string? tool_no { get; set; }

        /// <summary>工具名稱</summary>
        public string? name { get; set; }

        /// <summary>版本</summary>
        public string? version { get; set; }

        /// <summary>維護團隊</summary>
        public string? vendor { get; set; }

        /// <summary>分類</summary>
        public string? cat { get; set; }

        /// <summary>圖示名稱</summary>
        public string? icon { get; set; }

        /// <summary>給誰用</summary>
        public string? audience { get; set; }

        /// <summary>說明</summary>
        public string? description { get; set; }

        /// <summary>開起來要載入的組件檔名</summary>
        public string? entry { get; set; }

        /// <summary>組件裡那個畫面的型別全名</summary>
        [System.Text.Json.Serialization.JsonPropertyName("class")]
        public string? _class { get; set; }

        /// <summary>這支工具會做的事</summary>
        public List<string> capabilities { get; set; } = [];

        /// <summary>會用到的權限</summary>
        public List<string> permissions { get; set; } = [];

        /// <summary>版本與更新說明</summary>
        public List<JsonElement> changelog { get; set; } = [];
    }

    /// <summary>套件的簽章</summary>
    public class SignatureItem
    {
        /// <summary>用哪一種演算法</summary>
        public string? alg { get; set; }

        /// <summary>被簽的那個摘要值</summary>
        public string? digest { get; set; }

        /// <summary>簽章本身</summary>
        public string? signature { get; set; }
    }

    /// <summary>一件可能失敗的事做完之後</summary>
    private class Tried<T>
    {
        /// <summary>做成了的話拿到什麼</summary>
        public T? Value { get; set; }

        /// <summary>沒做成的話是什麼原因</summary>
        public string Fault { get; set; } = string.Empty;
    }

    #endregion
}
