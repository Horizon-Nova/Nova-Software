using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HelixStudio.Pack;

namespace HelixStudio.Release;

/// <summary>發佈一個產品：它的工具套件、每一版（自帶 .NET、混淆過）、每一版一個安裝檔</summary>
/// <remarks>
/// 值都從 package\products\{產品}.json 來；外部檔案都從 package\vendor（照 vendor.json 驗過），不連網。
/// out\release\{name}-{version}\{版}\   交給客戶的
/// out\build\{name}\                    這個產品的中間檔，不交出去
/// out\build\pg\、out\build\tools\       從 package\vendor 解開的 PostgreSQL 與打包工具，兩個產品共用
/// </remarks>
public class Publisher
{
    #region 發佈一個產品

    /// <summary>發佈一個產品</summary>
    /// <param name="product">package\products 底下的哪一個：HelixStudio 或 HelixStudio.Demo</param>
    public void Publish(string product)
    {
        // 工作區與這個產品的值
        var root = Path.GetFullPath(typeof(Publisher).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(one => one.Key == "WorkspaceRoot").Value!);
        var here = Path.Combine(root, "package");
        var productFile = Path.Combine(here, "products", $"{product}.json");

        if (!File.Exists(productFile))
        {
            throw new InvalidOperationException(
                $"沒有「{product}」這個產品。可以發佈的有："
                + string.Join("、", Directory.EnumerateFiles(Path.Combine(here, "products"), "*.json").Select(Path.GetFileNameWithoutExtension))
                + "。用法：dotnet run --project dev\\Release -- HelixStudio");
        }

        var p = JsonSerializer.Deserialize<ProductItem>(File.ReadAllText(productFile), Json)!;
        var list = JsonSerializer.Deserialize<VendorList>(File.ReadAllText(Path.Combine(here, "vendor.json")), Json)!;
        var vendor = Path.Combine(here, "vendor");
        var feed = Path.Combine(vendor, list.Nuget);
        var build = Path.Combine(root, "out", "build", p.Name);
        var shared = Path.Combine(root, "out", "build");
        var secret = Path.Combine(root, p.SigningKey);

        // 簽章私鑰：只看在不在。正式版與 Demo 版的私鑰不下載、不產生，開發用的那一把也不拿來代替
        if (!File.Exists(secret))
        {
            throw new InvalidOperationException(
                $"找不到 {p.SigningKey.Replace('/', '\\')}。正式簽章私鑰請向 RuanLuojie 索取，放到 {Path.GetDirectoryName(p.SigningKey.Replace('/', '\\'))}\\");
        }

        // 離線倉庫：每一個外部檔案都在、都是 vendor.json 記的那一個；不對就停（建置一次方案，環境準備會補齊）
        list.Files.ForEach(one =>
        {
            var path = Path.Combine(vendor, one.Path);

            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"缺 {path}：先建置一次方案，環境準備會下載並驗證。");
            }

            using var reading = File.OpenRead(path);

            if (!Convert.ToHexString(SHA256.HashData(reading)).Equals(one.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{path} 跟 vendor.json 記的檢查碼不同：刪掉它再建置一次方案，環境準備會重新下載。");
            }
        });

        if (!Directory.Exists(feed))
        {
            throw new InvalidOperationException($"缺 {feed}：先建置一次方案，環境準備會收集 NuGet 離線套件。");
        }

        // 混淆器與 Inno Setup：從離線倉庫的套件解開；版本換了才重新解
        var obfuscarVersion = Metadata("Obfuscar");
        var innoVersion = Metadata("InnoSetup");
        var obfuscar = Path.Combine(shared, "tools", "obfuscar");
        var inno = Path.Combine(shared, "tools", "innosetup");
        var iscc = Path.Combine(inno, "tools", "ISCC.exe");

        new[]
            {
                (Folder: obfuscar, Version: obfuscarVersion, Package: Path.Combine(feed, $"obfuscar.globaltool.{obfuscarVersion}.nupkg")),
                (Folder: inno, Version: innoVersion, Package: Path.Combine(feed, $"tools.innosetup.{innoVersion}.nupkg")),
            }
            .Where(one => !File.Exists(Path.Combine(one.Folder, ".version")) || File.ReadAllText(Path.Combine(one.Folder, ".version")).Trim() != one.Version)
            .ToList()
            .ForEach(one =>
            {
                if (!File.Exists(one.Package))
                {
                    throw new InvalidOperationException($"缺 {one.Package}：先建置一次方案，環境準備會收集 NuGet 離線套件。");
                }

                if (Directory.Exists(one.Folder))
                {
                    Directory.Delete(one.Folder, true);
                }

                ZipFile.ExtractToDirectory(one.Package, one.Folder);
                File.WriteAllText(Path.Combine(one.Folder, ".version"), one.Version);
            });

        // NuGet 只從離線倉庫拿，裝進它自己的套件資料夾：不連 nuget.org，也不用這台電腦的快取
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", Path.Combine(shared, "nuget"));

        // 從 dotnet run 或 Visual Studio 帶進來的 MSBuild 環境變數會讓這裡叫的 dotnet 用錯 MSBuild：拿掉
        Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(one => one.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(one => Environment.SetEnvironmentVariable(one, null));

        // 這一台建置出來、正開著的這個產品鎖著檔案：關掉
        Process.GetProcessesByName("HelixStudio")
            .Where(one =>
            {
                try
                {
                    return $"{one.MainModule?.FileName}".StartsWith(build, StringComparison.OrdinalIgnoreCase);
                }
                catch (Win32Exception)
                {
                    return false;
                }
            })
            .ToList()
            .ForEach(one => one.Kill(true));

        // 只重做這個產品自己的輸出；另一個產品的發佈留著
        if (Directory.Exists(build))
        {
            Directory.Delete(build, true);
        }

        var releases = Path.Combine(root, "out", "release");
        var mine = new Regex($"^{Regex.Escape(p.Name)}-\\d");

        Enumerable.Repeat(releases, 1).Where(Directory.Exists)
            .SelectMany(one => Directory.EnumerateDirectories(one, $"{p.Name}-*"))
            .Where(one => mine.IsMatch(Path.GetFileName(one)))
            .ToList()
            .ForEach(one => Directory.Delete(one, true));

        // 每一版的 PostgreSQL：zip 裡只取 bin、lib、share；解開一次，zip 換了才重解
        p.Editions.Select(one => one.Pg).Distinct().ToList().ForEach(pg =>
        {
            var f = list.Files.First(one => one.Id == pg);
            var target = Path.Combine(shared, "pg", pg);
            var stamp = Path.Combine(target, ".sha256");

            if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() == f.Sha256)
            {
                return;
            }

            Console.WriteLine($"解開 {f.Path}");

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            using (var zip = ZipFile.OpenRead(Path.Combine(vendor, f.Path)))
            {
                zip.Entries
                    .Where(one => Regex.IsMatch(one.FullName, "^pgsql/(bin|lib|share)/") && one.Name.Length > 0)
                    .ToList()
                    .ForEach(one =>
                    {
                        var into = Path.Combine(target, one.FullName);

                        Directory.CreateDirectory(Path.GetDirectoryName(into)!);
                        one.ExtractToFile(into, true);
                    });
            }

            File.WriteAllText(stamp, f.Sha256);
        });

        // 工具套件：每一種 .NET 一份，用那一種 .NET 的版本給的 Windows 命名；平台不收另一種 .NET 的套件
        foreach (var fw in p.Editions.Select(one => one.Fw).Distinct().Order(StringComparer.Ordinal))
        {
            var os = Regex.Replace(p.Editions.First(one => one.Fw == fw).Label, "-x(64|32)$", string.Empty);

            foreach (var t in p.Tools)
            {
                var project = Path.Combine(root, t);
                var folder = Path.GetDirectoryName(project)!;

                Run("dotnet", "build", project, "-c", "Release", "-f", fw, "--nologo", "--source", feed, "-p:PrepareEnvironment=false");

                var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")))!.AsObject();
                var stage = Path.Combine(build, "stage", os, $"{manifest["name"]}");

                Directory.CreateDirectory(stage);
                File.Copy(Path.Combine(folder, "bin", "Release", fw, $"{manifest["entry"]}"), Path.Combine(stage, $"{manifest["entry"]}"), true);

                // 操作說明的圖片跟著工具走
                var guide = Path.Combine(folder, "guide");

                Enumerable.Repeat(guide, 1).Where(Directory.Exists)
                    .SelectMany(one => Directory.EnumerateFiles(one, "*", SearchOption.AllDirectories))
                    .ToList()
                    .ForEach(one =>
                    {
                        var into = Path.Combine(stage, "guide", Path.GetRelativePath(guide, one));

                        Directory.CreateDirectory(Path.GetDirectoryName(into)!);
                        File.Copy(one, into, true);
                    });

                // 說明檔寫上這份是給哪一種 .NET 的
                manifest.Remove("runtime");
                manifest["runtime"] = Regex.Replace(fw, @"^(net\d+).*$", "$1");
                File.WriteAllText(Path.Combine(stage, "manifest.json"), manifest.ToJsonString(Indented), new UTF8Encoding(false));

                Console.WriteLine(Packer.Pack(stage, Path.Combine(build, "packages", os, $"{manifest["name"]}-{manifest["version"]}-{os}.stpkg"), secret));
            }
        }

        foreach (var e in p.Editions)
        {
            var os = Regex.Replace(e.Label, "-x(64|32)$", string.Empty);
            var dist = Path.Combine(build, "dist", e.Label);
            var obf = Path.Combine(build, "obfuscated", e.Label);
            var project = Path.Combine(root, p.Project);

            // 自帶 .NET 的發佈：.NET 執行環境跟著這一版走，不靠這台電腦裝了什麼
            Run("dotnet", "restore", project, "-r", e.Rid, "-p:SelfContained=true", "--nologo", "--source", feed);
            Run("dotnet", "publish", project, "-c", "Release", "-f", e.Fw, "-r", e.Rid, "--self-contained", "true", "--no-restore", "-o", dist, "--nologo", "-p:PrepareEnvironment=false");

            // 混淆主程式；自帶 .NET 的發佈資料夾裡有它參考的每一個組件
            var xml = XDocument.Load(Path.Combine(here, "obfuscar.xml"));
            var obfuscarRoot = xml.Root!;

            obfuscarRoot.Elements("Var").First(one => (string?)one.Attribute("name") == "InPath").SetAttributeValue("value", dist);
            obfuscarRoot.Elements("Var").First(one => (string?)one.Attribute("name") == "OutPath").SetAttributeValue("value", obf);
            obfuscarRoot.Elements("AssemblySearchPath").Skip(1).ToList().ForEach(one => one.Remove());
            obfuscarRoot.Elements("AssemblySearchPath").First().SetAttributeValue("path", dist);

            var config = Path.Combine(build, $"obfuscar-{e.Label}.xml");

            using (var writer = new StreamWriter(config, false, new UTF8Encoding(true)))
            {
                xml.Save(writer);
            }

            Run("dotnet", Path.Combine(obfuscar, "tools", "net10.0", "any", "GlobalTools.dll"), config);

            // 換掉沒混淆的 dll；pdb 會洩漏原本的名字；appsettings.json 會帶密碼，安裝檔會在那台電腦寫一份
            var distDll = Path.Combine(dist, "HelixStudio.dll");

            File.Copy(Path.Combine(obf, "HelixStudio.dll"), distDll, true);
            File.Delete(Path.Combine(dist, "HelixStudio.pdb"));
            File.Delete(Path.Combine(dist, "appsettings.json"));

            if (File.Exists(Path.Combine(dist, "appsettings.json")))
            {
                throw new InvalidOperationException($"{e.Label} 還帶著 appsettings.json");
            }

            // 第三方授權要求它們的聲明跟著軟體走；使用者授權合約跟著每一份走
            Directory.EnumerateFiles(Path.Combine(here, "legal")).ToList()
                .ForEach(one => File.Copy(one, Path.Combine(dist, Path.GetFileName(one)), true));

            // 關卡：交出去的 dll 是混淆過的那一份，受保護的型別名稱一個都沒留下，也沒有嵌進開發用的公鑰
            if (new FileInfo(distDll).Length != new FileInfo(Path.Combine(obf, "HelixStudio.dll")).Length)
            {
                throw new InvalidOperationException($"{e.Label} 的 HelixStudio.dll 不是混淆過的那一份");
            }

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(distDll));

            p.ObfuscationGate.Where(text.Contains).ToList()
                .ForEach(one => throw new InvalidOperationException($"{e.Label} 還看得到沒混淆的型別名稱「{one}」"));

            // 開發用簽章公鑰：Debug 版才嵌的那一份資源不能在，這台電腦的開發用公鑰也不能出現在 dll 裡任何地方
            using (var pe = new PEReader(File.OpenRead(distDll)))
            {
                var metadata = pe.GetMetadataReader();

                if (metadata.ManifestResources.Select(one => metadata.GetString(metadata.GetManifestResource(one).Name)).Contains(DevKey))
                {
                    throw new InvalidOperationException($"{e.Label} 的 HelixStudio.dll 嵌著開發用簽章公鑰（資源 {DevKey}）：發佈一定要用 Release 組態");
                }
            }

            var devPublicKey = Path.Combine(root, "out", "dev", "signing", "helix-public-key.txt");

            if (File.Exists(devPublicKey) && text.Contains(File.ReadAllText(devPublicKey).Trim()))
            {
                throw new InvalidOperationException($"{e.Label} 的 HelixStudio.dll 裡有開發用簽章公鑰（{devPublicKey}）：發佈出去的平台只能信正式公鑰");
            }

            // 這一版要裝的 VC++ 執行元件
            var redists = Path.Combine(build, "redist", e.Label);

            Directory.CreateDirectory(redists);
            e.Redists.ForEach(one =>
            {
                var from = Path.Combine(vendor, list.Files.First(two => two.Id == one).Path);

                File.Copy(from, Path.Combine(redists, Path.GetFileName(from)), true);
            });

            // 安裝檔
            var version = FileVersionInfo.GetVersionInfo(Path.Combine(dist, "HelixStudio.exe")).ProductVersion;
            var release = Path.Combine(releases, $"{p.Name}-{version}", e.Label);
            var tools = p.ToolsInInstaller ? Path.Combine(build, "packages", os) : string.Empty;

            Run(iscc, "/Q", $"/O{release}", $"/F{p.Name}-{version}-{e.Label}-Setup",
                $"/DAppId={p.AppId}", $"/DAppName={p.AppName}", $"/DInstallFolder={p.InstallFolder}", $"/DDataFolder={p.DataFolder}",
                $"/DService={p.Service}", $"/DPort={p.Port}", $"/DIcon={Path.Combine(Path.GetDirectoryName(project)!, "Assets", "HelixStudio_icon.ico")}",
                $"/DLegal={Path.Combine(here, "legal")}", $"/DArch={e.Arch}", $"/DMinOs={e.MinOs}", $"/DDist={dist}", $"/DPg={Path.Combine(shared, "pg", e.Pg, "pgsql")}",
                $"/DRedists={redists}", $"/DNeedKb={e.NeedKb}", $"/DTools={tools}", $"/DAfterSql={p.AfterInstallSql}", $"/DDatabase={Path.Combine(root, "deploy", "database")}",
                Path.Combine(here, "installer", "HelixStudio.iss"));

            // 不跟著安裝檔走的工具套件放在安裝檔旁邊
            if (!p.ToolsInInstaller)
            {
                Directory.EnumerateFiles(Path.Combine(build, "packages", os), "*.stpkg").ToList()
                    .ForEach(one => File.Copy(one, Path.Combine(release, Path.GetFileName(one)), true));
            }
        }

        // 交給客戶的每一個檔
        Directory.EnumerateDirectories(releases, $"{p.Name}-*")
            .Where(one => mine.IsMatch(Path.GetFileName(one)))
            .SelectMany(one => Directory.EnumerateFiles(one, "*", SearchOption.AllDirectories))
            .ToList()
            .ForEach(one => Console.WriteLine($"OK: {one}"));
    }

    #endregion

    #region 對照

    /// <summary>Debug 版嵌進開發用簽章公鑰時用的資源名稱；發佈出去的 dll 不能有它</summary>
    private const string DevKey = "DevPackageKey";

    /// <summary>讀 json：欄位名稱不分大小寫</summary>
    private static JsonSerializerOptions Json { get; } = new() { PropertyNameCaseInsensitive = true };

    /// <summary>寫說明檔：縮排，中文照原樣寫</summary>
    private static JsonSerializerOptions Indented { get; } = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Release.csproj 寫進組件的一個值</summary>
    /// <param name="key">哪一個</param>
    private static string Metadata(string key) =>
        typeof(Publisher).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(one => one.Key == key).Value!;

    /// <summary>執行一支程式並等它結束，輸出照印；結束碼不是 0 就停</summary>
    /// <param name="file">程式</param>
    /// <param name="arguments">參數，每一個各自一格，不必自己加引號</param>
    private static void Run(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false };

        arguments.ToList().ForEach(start.ArgumentList.Add);

        using var process = Process.Start(start)!;

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(file)} {arguments.FirstOrDefault()} 失敗（結束碼 {process.ExitCode}）");
        }
    }

    #endregion

    #region 模型

    /// <summary>package\products\*.json：一個產品</summary>
    public class ProductItem
    {
        /// <summary>發佈出來的資料夾與安裝檔用的名字</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>平台的專案檔</summary>
        public string Project { get; set; } = string.Empty;

        /// <summary>安裝檔的 AppId；定了就不換</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>「新增或移除程式」與開始功能表上的名字</summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>裝在 Program Files 底下的資料夾名</summary>
        public string InstallFolder { get; set; } = string.Empty;

        /// <summary>ProgramData 底下的資料夾名</summary>
        public string DataFolder { get; set; } = string.Empty;

        /// <summary>資料庫服務的名字</summary>
        public string Service { get; set; } = string.Empty;

        /// <summary>資料庫聽的埠</summary>
        public int Port { get; set; }

        /// <summary>簽工具套件的私鑰在工作區的哪裡</summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>混淆後不能再看得到的型別名稱</summary>
        public List<string> ObfuscationGate { get; set; } = [];

        /// <summary>這個產品的工具專案檔</summary>
        public List<string> Tools { get; set; } = [];

        /// <summary>工具套件是不是跟著安裝檔一起裝</summary>
        public bool ToolsInInstaller { get; set; }

        /// <summary>資料表建好或更新後再執行的 SQL</summary>
        public string AfterInstallSql { get; set; } = string.Empty;

        /// <summary>每一版</summary>
        public List<EditionItem> Editions { get; set; } = [];
    }

    /// <summary>一個產品的一版</summary>
    public class EditionItem
    {
        /// <summary>版的名字：Win10-11-x64 這類</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>目標 .NET</summary>
        public string Fw { get; set; } = string.Empty;

        /// <summary>發佈給哪一種 Windows 與 CPU</summary>
        public string Rid { get; set; } = string.Empty;

        /// <summary>安裝檔的 x64 或 x86</summary>
        public string Arch { get; set; } = string.Empty;

        /// <summary>最低 Windows 版本</summary>
        public string MinOs { get; set; } = string.Empty;

        /// <summary>帶哪一個 PostgreSQL（vendor.json 的代號）</summary>
        public string Pg { get; set; } = string.Empty;

        /// <summary>帶哪幾個 VC++ 執行元件（vendor.json 的代號）</summary>
        public List<string> Redists { get; set; } = [];

        /// <summary>Windows 7 要先有的系統更新；沒有就是空的</summary>
        public string NeedKb { get; set; } = string.Empty;
    }

    /// <summary>package\vendor.json：打包要用的外部檔案</summary>
    public class VendorList
    {
        /// <summary>每一個外部檔案</summary>
        public List<VendorFile> Files { get; set; } = [];

        /// <summary>NuGet 離線套件放在 package\vendor 底下的哪一個資料夾</summary>
        public string Nuget { get; set; } = string.Empty;
    }

    /// <summary>一個外部檔案</summary>
    public class VendorFile
    {
        /// <summary>代號</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>給人看的名字</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>放在 package\vendor 底下的哪裡</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>檢查碼</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>從哪裡下載</summary>
        public string Source { get; set; } = string.Empty;
    }

    #endregion
}
