using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelixStudio.Pack;

namespace HelixStudio.Setup;

/// <summary>這個工作區要能建置、能跑、能發佈所需要的環境</summary>
/// <remarks>
/// 只有第一次（或外部檔案、專案的套件換過）要連網；之後每一樣都在，一下就跳過。
/// 正式版與 Demo 版的簽章私鑰不從這裡來：那兩把只在 keys\，由 RuanLuojie 給。
/// </remarks>
public class Workspace
{
    #region 準備環境

    /// <summary>把環境準備好：外部檔案、NuGet 離線套件、開發用簽章金鑰、開發資料庫；已經在的跳過</summary>
    /// <param name="root">工作區根目錄</param>
    /// <param name="sdk">這次建置用的 .NET SDK 版本；SDK 換了，自帶 .NET 的發佈要的執行環境套件也跟著換</param>
    public void Prepare(string root, string sdk)
    {
        var clock = Stopwatch.StartNew();

        // 同一台電腦同時兩個建置（Visual Studio 與命令列）：後到的等前一個做完，不會兩個一起下載同一個檔
        using var gate = new Mutex(false, $@"Global\HelixSetup-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToLowerInvariant())))[..16]}");

        try
        {
            gate.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // 前一個建置被中斷時沒放手：鎖已經是這一個的，照常往下做
        }

        try
        {
            // 要準備的每一樣：外部檔案一個一個、NuGet 離線套件、開發用簽章金鑰、開發資料庫
            var list = JsonSerializer.Deserialize<VendorList>(File.ReadAllText(Path.Combine(root, "package", "vendor.json")), Json)!;
            var vendor = Path.Combine(root, "package", "vendor");
            var total = list.Files.Count + 3;
            var worked = 0;

            // 驗過的外部檔案記在這裡：大小與修改時間沒變就不再算一次檢查碼
            var stampFile = Path.Combine(vendor, ".verified.json");
            var stamps = File.Exists(stampFile)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(stampFile)) ?? []
                : [];

            // 外部檔案（PostgreSQL、VC++）：在而且驗過就跳過；不在就從 vendor.json 記的來源下載，邊下載邊算檢查碼，對不上就停
            foreach (var (file, index) in list.Files.Select((one, at) => (one, at + 1)))
            {
                var path = Path.Combine(vendor, file.Path);
                var info = new FileInfo(path);
                var mark = info.Exists ? $"{file.Sha256}|{info.Length}|{info.LastWriteTimeUtc.Ticks}" : string.Empty;

                if (info.Exists && stamps.GetValueOrDefault(file.Path) == mark)
                {
                    continue;
                }

                worked++;

                // 檔案在但還沒驗過（或換過）：算一次檢查碼；對得上就記下來，對不上就重新下載
                if (info.Exists)
                {
                    Console.WriteLine($"環境準備中 {index}/{total}：驗證 {file.Name}（{info.Length / Mb} MB）");

                    using (var reading = File.OpenRead(path))
                    {
                        if (Convert.ToHexString(SHA256.HashData(reading)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            stamps[file.Path] = mark;
                            File.WriteAllText(stampFile, JsonSerializer.Serialize(stamps, Indented));
                            continue;
                        }
                    }

                    Console.WriteLine($"環境準備中 {index}/{total}：{file.Name} 跟 vendor.json 記的檢查碼不同，重新下載");
                    File.Delete(path);
                }

                // 下載到 .part，邊寫邊算檢查碼；每兩秒印一次進度
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var part = path + ".part";
                var hash = string.Empty;

                try
                {
                    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                    using var response = http.Send(new HttpRequestMessage(HttpMethod.Get, file.Source), HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    var size = response.Content.Headers.ContentLength ?? 0;
                    var buffer = new byte[1 << 20];
                    var done = 0L;
                    var tick = Stopwatch.StartNew();

                    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    using var from = response.Content.ReadAsStream();
                    using (var into = File.Create(part))
                    {
                        Console.WriteLine($"環境準備中 {index}/{total}：{file.Name}（0/{size / Mb} MB）");

                        for (var read = from.Read(buffer); read > 0; read = from.Read(buffer))
                        {
                            into.Write(buffer, 0, read);
                            digest.AppendData(buffer, 0, read);
                            done += read;

                            if (tick.ElapsedMilliseconds >= 2000)
                            {
                                Console.WriteLine($"環境準備中 {index}/{total}：{file.Name}（{done / Mb}/{size / Mb} MB）");
                                tick.Restart();
                            }
                        }
                    }

                    Console.WriteLine($"環境準備中 {index}/{total}：{file.Name}（{done / Mb}/{size / Mb} MB）下載完成");
                    hash = Convert.ToHexString(digest.GetHashAndReset());
                }
                catch (HttpRequestException error)
                {
                    File.Delete(part);
                    throw new InvalidOperationException(
                        $"下載 {file.Name} 失敗（{file.Source}）：{error.Message}。第一次準備要連網，連得上之後再建置一次。");
                }

                // 檢查碼對不上：來源換了檔案，或下載被改過；不留這個檔，停下來
                if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(part);
                    throw new InvalidOperationException(
                        $"{file.Name} 下載下來的檢查碼 {hash.ToLowerInvariant()} 跟 package\\vendor.json 記的 {file.Sha256} 不同：{file.Source} 換過檔案，或下載被改過。"
                        + $"確認新檔可信後改 vendor.json 的 sha256，或把對的檔案放到 package\\vendor\\{file.Path.Replace('/', '\\')}，再建置一次。");
                }

                File.Move(part, path, true);

                info = new FileInfo(path);
                stamps[file.Path] = $"{file.Sha256}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                File.WriteAllText(stampFile, JsonSerializer.Serialize(stamps, Indented));
            }

            // NuGet 離線套件：發佈只從 package\vendor\nuget 拿套件，不連網。
            // 專案檔、產品、SDK 版本跟上次收集時一樣就跳過；換過就重新收集一次（要連網，或這台電腦的快取裡已經有）
            var step = list.Files.Count + 1;
            var feed = Path.Combine(vendor, list.Nuget);
            var inputs = new[] { "global.json", "Directory.Build.props", "Directory.Build.targets" }
                .Select(one => Path.Combine(root, one))
                .Where(File.Exists)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "package", "products"), "*.json"))
                .Concat(new[] { "src", "tools", Path.Combine("dev", "Release"), Path.Combine("dev", "HelixStudio.Pack") }
                    .Select(one => Path.Combine(root, one))
                    .Where(Directory.Exists)
                    .SelectMany(one => Directory.EnumerateFiles(one, "*.csproj", SearchOption.AllDirectories)))
                .OrderBy(one => one, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using var inputDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            inputDigest.AppendData(Encoding.UTF8.GetBytes(sdk + "\n"));
            inputs.ForEach(one =>
            {
                inputDigest.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, one).ToLowerInvariant() + "\n"));
                inputDigest.AppendData(File.ReadAllBytes(one));
            });

            var inputHash = Convert.ToHexString(inputDigest.GetHashAndReset());
            var collected = Path.Combine(feed, ".collected");

            if (!File.Exists(collected) || File.ReadAllText(collected).Trim() != inputHash)
            {
                worked++;
                Console.WriteLine($"環境準備中 {step}/{total}：NuGet 離線套件（專案的套件或 SDK 換過，重新收集）");

                // 在另一份專案檔上還原：不動這次建置正在用的 obj（還原到別的套件資料夾，正在建置的專案會找不到套件）
                var work = Path.Combine(root, "out", "build", "collect");
                var tree = Path.Combine(work, "tree");
                var cache = Path.Combine(work, "packages");
                var fresh = Path.Combine(work, "feed");

                if (Directory.Exists(work))
                {
                    Directory.Delete(work, true);
                }

                inputs.ForEach(one =>
                {
                    var copy = Path.Combine(tree, Path.GetRelativePath(root, one));

                    Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                    File.Copy(one, copy);
                });

                // 套件來源：這台電腦設定的來源（nuget.org）之外，再加上舊的離線倉庫與這台電腦的 NuGet 快取，那裡有的就不必下載
                var locals = Run(Dotnet, ["nuget", "locals", "global-packages", "--list"]);
                var global = locals.Output.Split('\n').Select(one => one.Trim())
                    .Where(one => one.StartsWith("global-packages:", StringComparison.OrdinalIgnoreCase))
                    .Select(one => one["global-packages:".Length..].Trim())
                    .FirstOrDefault() ?? string.Empty;
                var sources = new[] { feed, global }
                    .Select(one => one.TrimEnd('\\', '/'))
                    .Where(one => one.Length > 0 && Directory.Exists(one))
                    .ToList();
                var extra = sources.Count > 0 ? [$"-p:RestoreAdditionalProjectSources={string.Join("%3B", sources)}"] : Array.Empty<string>();

                // 每一個產品、每一種 CPU 的自帶 .NET 發佈，每一支工具，以及發佈程式自己要的混淆器與 Inno Setup：
                // 還原進一個空的套件資料夾，裡面有的就剛好是離線發佈要的
                var products = Directory.EnumerateFiles(Path.Combine(root, "package", "products"), "*.json")
                    .Select(one => JsonSerializer.Deserialize<ProductItem>(File.ReadAllText(one), Json)!)
                    .ToList();
                var jobs = products
                    .SelectMany(one => one.Editions.Select(two => two.Rid).Distinct()
                        .Select(rid => (Project: one.Project, Label: $"{Path.GetFileNameWithoutExtension(one.Project)} {rid}", Extra: new[] { "-r", rid, "-p:SelfContained=true" })))
                    .Concat(products.SelectMany(one => one.Tools).Distinct()
                        .Select(one => (Project: one, Label: Path.GetFileNameWithoutExtension(one), Extra: Array.Empty<string>())))
                    .Append((Project: "dev/Release/Release.csproj", Label: "混淆器與 Inno Setup", Extra: Array.Empty<string>()))
                    .ToList();

                foreach (var (job, index) in jobs.Select((one, at) => (one, at + 1)))
                {
                    Console.WriteLine($"環境準備中 {step}/{total}：NuGet 離線套件（{index}/{jobs.Count} {job.Label}）");

                    var restored = Run(Dotnet,
                        new[] { "restore", Path.Combine(tree, job.Project), "--nologo", "--ignore-failed-sources", "-nodeReuse:false" }
                            .Concat(job.Extra).Concat(extra),
                        new Dictionary<string, string> { ["NUGET_PACKAGES"] = cache });

                    if (restored.Code != 0)
                    {
                        throw new InvalidOperationException(
                            $"收集 NuGet 離線套件失敗（{job.Label}）：{string.Join(" ", restored.Text.Split('\n').Where(one => one.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(5).Select(one => one.Trim()))}");
                    }
                }

                // 每一個套件檔平放進 vendor\nuget（一個本機套件來源）；還原用的資料夾與專案檔副本丟掉
                Directory.CreateDirectory(fresh);
                Directory.EnumerateFiles(cache, "*.nupkg", SearchOption.AllDirectories).ToList()
                    .ForEach(one => File.Copy(one, Path.Combine(fresh, Path.GetFileName(one)), true));
                File.WriteAllText(Path.Combine(fresh, ".collected"), inputHash);

                if (Directory.Exists(feed))
                {
                    Directory.Delete(feed, true);
                }

                Directory.Move(fresh, feed);
                Directory.Delete(work, true);

                Console.WriteLine($"環境準備中 {step}/{total}：NuGet 離線套件（{Directory.EnumerateFiles(feed, "*.nupkg").Count()} 個套件）");
            }

            // 開發用簽章金鑰：每台開發電腦自己產生一把；Debug 版的平台只信這一把，自己簽的工具套件裝得起來。
            // Release 版信的仍然只有 Anchors 裡的正式公鑰，發佈只用 keys\ 裡的正式私鑰
            step++;

            var signing = Path.Combine(root, "out", "dev", "signing");

            if (!File.Exists(Path.Combine(signing, "helix-signing-key.pem")) || !File.Exists(Path.Combine(signing, "helix-public-key.txt")))
            {
                worked++;
                Console.WriteLine($"環境準備中 {step}/{total}：產生開發用簽章金鑰（{signing}）");
                Packer.Key(signing);
            }

            // 開發資料庫：照 deploy\database\docker-compose.yml 用 docker compose up -d 啟動；容器已經在就只啟動它（--no-recreate：不重建、不清資料）。
            // 同名的容器若不是這一份 compose 建的（compose 專案或服務對不上），compose 接不過來，會撞名或另建一個：不啟動，寫一個警告。
            // 沒有 Docker 不擋建置：寫一個警告，平台開起來自己會說連不上資料庫
            step++;

            var compose = Path.Combine(root, "deploy", "database", "docker-compose.yml");
            var docker = Run("docker", ["version", "--format", "{{.Server.Version}}"]);
            var config = docker.Code == 0 ? Run("docker", ["compose", "-f", compose, "config", "--format", "json"]) : docker;

            if (docker.Code == Missing)
            {
                Console.WriteLine("Setup : warning HX0002 : 這台電腦找不到 Docker，開發資料庫沒有啟動。裝好 Docker Desktop 再建置一次；沒有資料庫時平台會說連不上資料庫。");
            }
            else if (docker.Code != 0)
            {
                Console.WriteLine("Setup : warning HX0002 : Docker 沒有在執行，開發資料庫沒有啟動。打開 Docker Desktop 再建置一次；沒有資料庫時平台會說連不上資料庫。");
            }
            else if (config.Code != 0)
            {
                Console.WriteLine($"Setup : warning HX0003 : docker compose 讀不了 {compose}，開發資料庫沒有啟動：{config.Error.Trim()}");
            }
            else
            {
                // compose 要的每一個容器，跟這台電腦上同名的容器比：沒有、或是同一個 compose 專案的同一個服務，compose 才接得過來
                var definition = JsonSerializer.Deserialize<ComposeItem>(config.Output, Json)!;
                var containers = definition.Services
                    .Select(one => (Service: one.Key, Name: one.Value.ContainerName.Length > 0 ? one.Value.ContainerName : $"{definition.Name}-{one.Key}-1"))
                    .Select(one => (one.Service, one.Name, Found: Run("docker", ["inspect", one.Name, "--format", "{{.State.Status}} {{json .Config.Labels}}"])))
                    .Select(one => (one.Service, one.Name, Exists: one.Found.Code == 0, State: one.Found.Output.Split(' ', 2)[0].Trim(),
                        Labels: one.Found.Code == 0 ? JsonSerializer.Deserialize<Dictionary<string, string>>(one.Found.Output.Split(' ', 2)[1]) ?? [] : []))
                    .ToList();
                var foreign = containers
                    .Where(one => one.Exists
                        && (one.Labels.GetValueOrDefault("com.docker.compose.project") != definition.Name
                            || one.Labels.GetValueOrDefault("com.docker.compose.service") != one.Service))
                    .ToList();

                if (foreign.Count > 0)
                {
                    foreign.ForEach(one => Console.WriteLine(
                        $"Setup : warning HX0004 : 容器 {one.Name}（{one.State}）不是 deploy\\database\\docker-compose.yml 建的"
                        + $"（它的 compose 專案「{one.Labels.GetValueOrDefault("com.docker.compose.project")}」、服務「{one.Labels.GetValueOrDefault("com.docker.compose.service")}」，"
                        + $"這份要的是「{definition.Name}」、「{one.Service}」），compose 接不過來：不啟動、不重建，照原樣留著。"));
                }
                else if (containers.Any(one => one.State != "running"))
                {
                    worked++;
                    Console.WriteLine($"環境準備中 {step}/{total}：啟動開發資料庫 {string.Join("、", containers.Select(one => one.Name))}（第一次會下載映像檔）");

                    var up = Run("docker", ["compose", "-f", compose, "up", "-d", "--no-recreate"], echo: true);

                    if (up.Code != 0)
                    {
                        Console.WriteLine($"Setup : warning HX0003 : 開發資料庫沒有啟動（docker compose 結束碼 {up.Code}），平台會說連不上資料庫。");
                    }
                }
            }

            // 每一樣都在就只印一行
            Console.WriteLine(worked == 0
                ? $"環境已就緒（{clock.ElapsedMilliseconds} ms）"
                : $"環境準備好了（{clock.Elapsed.TotalSeconds:0.0} 秒）");
        }
        finally
        {
            gate.ReleaseMutex();
        }
    }

    #endregion

    #region 對照

    /// <summary>一 MB 幾個位元組</summary>
    private const long Mb = 1024 * 1024;

    /// <summary>程式根本跑不起來（這台電腦沒有裝）時的結束碼</summary>
    private const int Missing = -1;

    /// <summary>執行建置的那一個 dotnet；不是從建置叫起來的就用 PATH 上的</summary>
    private static string Dotnet { get; } = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

    /// <summary>讀 json：欄位名稱不分大小寫</summary>
    private static JsonSerializerOptions Json { get; } = new() { PropertyNameCaseInsensitive = true };

    /// <summary>寫 json：縮排</summary>
    private static JsonSerializerOptions Indented { get; } = new() { WriteIndented = true };

    /// <summary>執行一支程式並等它結束，帶回結束碼與輸出；程式不存在時結束碼是 <see cref="Missing"/></summary>
    /// <param name="file">程式</param>
    /// <param name="arguments">參數，每一個各自一格，不必自己加引號</param>
    /// <param name="variables">另外要設的環境變數</param>
    /// <param name="echo">輸出要不要邊跑邊印</param>
    private static RunItem Run(string file, IEnumerable<string> arguments, IDictionary<string, string>? variables = null, bool echo = false)
    {
        var start = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        arguments.ToList().ForEach(start.ArgumentList.Add);

        // 外層建置留下的 MSBuild 環境變數會讓子程序的 dotnet 用錯 MSBuild：拿掉
        start.Environment.Keys.Where(one => one.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList()
            .ForEach(one => start.Environment.Remove(one));
        (variables ?? new Dictionary<string, string>()).ToList().ForEach(one => start.Environment[one.Key] = one.Value);

        var text = new StringBuilder();
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = start };

            process.OutputDataReceived += (_, line) => Keep(line.Data, output);
            process.ErrorDataReceived += (_, line) => Keep(line.Data, error);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new RunItem { Code = process.ExitCode, Text = text.ToString(), Output = output.ToString(), Error = error.ToString() };
        }
        catch (Win32Exception)
        {
            return new RunItem { Code = Missing };
        }

        void Keep(string? line, StringBuilder into)
        {
            if (line is null)
            {
                return;
            }

            lock (text)
            {
                text.AppendLine(line);
                into.AppendLine(line);
            }

            if (echo)
            {
                Console.WriteLine($"  {line}");
            }
        }
    }

    #endregion

    #region 模型

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
        /// <summary>代號，產品的版本用它指名</summary>
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

    /// <summary>package\products\*.json：一個產品（這裡只用得到收集套件要的那幾欄）</summary>
    public class ProductItem
    {
        /// <summary>平台的專案檔</summary>
        public string Project { get; set; } = string.Empty;

        /// <summary>這個產品的工具專案檔</summary>
        public List<string> Tools { get; set; } = [];

        /// <summary>每一版</summary>
        public List<EditionItem> Editions { get; set; } = [];
    }

    /// <summary>一個產品的一版</summary>
    public class EditionItem
    {
        /// <summary>發佈給哪一種 Windows 與 CPU</summary>
        public string Rid { get; set; } = string.Empty;
    }

    /// <summary>執行一支程式的結果</summary>
    private class RunItem
    {
        /// <summary>結束碼</summary>
        public int Code { get; set; }

        /// <summary>它印出來的每一行（標準輸出與錯誤輸出照順序）</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>只有標準輸出</summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>只有錯誤輸出</summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>docker compose config --format json：這裡只用得到專案名稱與每一個服務的容器名稱</summary>
    public class ComposeItem
    {
        /// <summary>compose 專案名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>每一個服務：服務名稱對到它的設定</summary>
        public Dictionary<string, ServiceItem> Services { get; set; } = [];
    }

    /// <summary>compose 的一個服務</summary>
    public class ServiceItem
    {
        /// <summary>容器名稱；沒寫的話 compose 用「專案-服務-1」</summary>
        [JsonPropertyName("container_name")]
        public string ContainerName { get; set; } = string.Empty;
    }

    #endregion
}
