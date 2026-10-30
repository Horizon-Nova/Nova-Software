using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    #region 看一個套件檔裡面是什麼

    /// <summary>打開一個套件檔，讀出它宣告了什麼，並且驗它的簽章</summary>
    /// <param name="path">套件檔在哪裡</param>
    public PackageItem Read(string path)
    {
        // 這個檔案本身：多大、叫什麼
        var file = new FileInfo(path);
        var item = new PackageItem { Path = path, Name = file.Name, Bytes = file.Length };

        // 開頭四個位元組是 zip 的 PK 標記才當成 zip 打開；不是就到此為止，後面的檢查都沒有東西可以查
        var head = new byte[ZipHead.Length];

        using (var reading = File.OpenRead(path))
        {
            // 檔案不到四個位元組就讀不滿，留下的是 0，下面比對不會過
            reading.Read(head, 0, head.Length);
        }

        var zipped = head.SequenceEqual(ZipHead);

        item.Faults.AddRange(Enumerable.Repeat(
            "這個檔案不是套件格式。平台只收 .stpkg（公司簽章套件）；如果你拿到的是 .zip 或 .msi，請回報給資訊部重新包裝。", 1)
            .Where(_ => !zipped));

        using var zip = zipped ? ZipFile.OpenRead(path) : null;

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
            .Select(one => JsonSerializer.Deserialize<ManifestItem>(Bytes(one!)))
            .ToList();

        item.Faults.AddRange(declared.Where(one => one is null).Select(_ => $"套件的說明檔 {ManifestName} 是空的。"));
        item.Manifests.AddRange(declared.Where(one => one is not null)!);

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
            .Select(one => JsonSerializer.Deserialize<SignatureItem>(Bytes(one!)))
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

    /// <summary>把一個套件解開到這台裝置上，帶回照套件宣告填好的那一列 tool（寫進資料庫由 ShellStore 做）</summary>
    /// <param name="item">已經讀過也驗過的那一個套件</param>
    /// <param name="source">這個套件從哪裡來</param>
    /// <param name="registered">資料庫目前登錄的那一版裝在哪裡；清舊版本時留著它，登錄沒換成功時工具還能用</param>
    public tool Install(PackageItem item, string source, string registered)
    {
        var manifest = item.Manifests.First();

        // 這支工具的檔案放在這裡：一支一個資料夾，版本各自一層，這一版還在就先清掉
        var home = Home(manifest);

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

        // 其他版本的資料夾：資料庫目前登錄的那一版留著；這個行程沒載著的其他版刪掉，正在用的那一版 Windows 鎖著，留到下次匯入
        var mapped = Process.GetCurrentProcess().Modules.OfType<ProcessModule>().Select(one => one.FileName).ToList();

        Directory.EnumerateDirectories(Path.GetDirectoryName(home)!)
            .Where(one => !string.Equals(one, home, StringComparison.OrdinalIgnoreCase))
            .Where(one => !string.Equals(one, registered, StringComparison.OrdinalIgnoreCase))
            .Where(one => !mapped.Any(file => file.StartsWith(one + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .ForEach(one => Directory.Delete(one, true));

        // 這一支工具的那一列：套件宣告的每一欄、解到哪裡、從哪裡來
        return Fill(new tool(), manifest, item, home, source);
    }

    /// <summary>把套件宣告的每一欄寫進這一列</summary>
    /// <param name="row">要寫的那一列</param>
    /// <param name="manifest">套件宣告了什麼</param>
    /// <param name="item">那一個套件檔</param>
    /// <param name="home">檔案解到哪一個資料夾</param>
    /// <param name="source">從哪裡來</param>
    private static tool Fill(tool row, ManifestItem manifest, PackageItem item,
        string home, string source)
    {
        row.type = "tool";
        row.tool_no = $"{manifest.tool_no}";
        row.name = $"{manifest.name}";
        row.icon = $"{manifest.icon}";
        row.cat = $"{manifest.cat}";
        row.vendor = $"{manifest.vendor}";
        row.version = $"{manifest.version}";
        row.bytes = item.Bytes;
        row.description = $"{manifest.description}";
        row.capabilities = [.. manifest.capabilities];
        row.permissions = [.. manifest.permissions];
        row.changelog = JsonSerializer.Serialize(manifest.changelog);
        row.guide = JsonSerializer.Serialize(manifest.guide);
        row.entry = $"{manifest.entry}";
        row._class = $"{manifest._class}";
        row.settings = manifest.settings;
        row.home = home;
        row.source = source;
        row.created_at = DateTime.UtcNow;
        row.updated_at = DateTime.UtcNow;

        return row;
    }

    #endregion

    #region 把一支工具的檔案刪掉

    /// <summary>移除一支工具的檔案：它的資料夾（每一版一層）整個刪掉；這一次開過的組件還映射在平台這個行程裡，Windows 鎖著它的檔，就留著</summary>
    /// <param name="home">裝著的那一版的資料夾</param>
    /// <returns>刪掉了是空字串；留著的話是那個資料夾</returns>
    public string DeleteHome(string home)
    {
        var folder = Path.GetDirectoryName(home)!;
        var loaded = Process.GetCurrentProcess().Modules
            .OfType<ProcessModule>()
            .Any(one => one.FileName.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

        new[] { folder }.Where(_ => !loaded).Where(Directory.Exists).ToList().ForEach(one => Directory.Delete(one, true));

        return loaded ? folder : string.Empty;
    }

    #endregion

    #region 一支工具的檔案在不在、有沒有在用

    /// <summary>一支工具要載入的組件檔在不在這台電腦上</summary>
    /// <param name="home">裝著的那一版的資料夾</param>
    /// <param name="entry">組件檔名</param>
    public bool QueryPresent(string home, string entry) =>
        File.Exists(Path.Combine(home, entry));

    /// <summary>這個資料夾底下有沒有組件還映射在平台這個行程裡（Windows 鎖著它的檔，換不掉也刪不掉）</summary>
    /// <param name="folder">那一個資料夾</param>
    public bool QueryInUse(string folder) =>
        Process.GetCurrentProcess().Modules
            .OfType<ProcessModule>()
            .Any(one => one.FileName.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region 一個套件裝在哪裡

    /// <summary>一個套件的檔案解到哪個資料夾：一支工具一個資料夾，版本各自一層</summary>
    /// <param name="manifest">套件宣告了什麼</param>
    public static string Home(ManifestItem manifest) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelixStudio", "tools", $"{manifest.tool_no}", $"{manifest.version}");

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

    /// <summary>把套件裡的一筆整個讀進記憶體</summary>
    /// <param name="entry">那一筆</param>
    private static byte[] Bytes(ZipArchiveEntry entry)
    {
        using var reading = entry.Open();
        using var buffer = new MemoryStream();

        reading.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>zip 檔開頭的四個位元組：PK</summary>
    private static readonly byte[] ZipHead = [0x50, 0x4B, 0x03, 0x04];

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

        /// <summary>給哪一版平台：net10（Win10～11）或 net6（Win7）；舊套件沒寫就是 net10</summary>
        public string? runtime { get; set; }

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

        /// <summary>組件裡設定畫面的型別全名；沒有就是這支工具沒有設定</summary>
        public string? settings { get; set; }

        /// <summary>這支工具會做的事</summary>
        public List<string> capabilities { get; set; } = [];

        /// <summary>會用到的權限</summary>
        public List<string> permissions { get; set; } = [];

        /// <summary>版本與更新說明</summary>
        public List<JsonElement> changelog { get; set; } = [];

        /// <summary>操作說明，一步一個元素：text 這一步做什麼、image 截圖在套件裡的相對路徑</summary>
        public List<JsonElement> guide { get; set; } = [];
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

    #endregion
}
