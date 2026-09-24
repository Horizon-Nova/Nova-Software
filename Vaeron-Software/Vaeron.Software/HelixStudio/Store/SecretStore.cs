using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HelixStudio.Contract;

namespace HelixStudio.Store;

/// <summary>這個軟體唯一碰加解密的型別</summary>
/// <remarks>
/// 用 Windows 的 DPAPI，範圍是 CurrentUser：密文只有這台機器的這個帳號解得開，
/// 換一台機器、換一個帳號都解不開。金鑰由 Windows 保管，程式裡沒有金鑰。
/// </remarks>
public class SecretStore
{
    #region 拿這個軟體要用的一個秘密

    /// <summary>拿一個秘密的明文：保險箱裡有就從裡面拿，第一次啟動才從設定檔搬進去</summary>
    /// <param name="name">這個秘密叫什麼</param>
    public string Secret(string name)
    {
        // 保險箱裡現在有哪幾個秘密
        var safe = Sealed();

        // 裡面有就解開它；DPAPI 解不開（換了機器或帳號）就當作沒有
        var here = safe
            .Where(one => one.Key == name)
            .Select(one => Try(() => ProtectedData.Unprotect(Convert.FromBase64String(one.Value), null, Scope)))
            .Where(one => one is not null)
            .Select(one => Encoding.UTF8.GetString(one!))
            .ToList();

        // 保險箱裡沒有，就從設定檔拿這一次的明文
        var plain = here
            .Concat(Enumerable
                .Repeat(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), 1)
                .Where(File.Exists)
                .Select(one => Try(() => JsonDocument.Parse(File.ReadAllText(one))))
                .Where(one => one is not null)
                .SelectMany(one => one!.RootElement.GetProperty("ConnectionStrings").EnumerateObject())
                .Where(one => one.Name == name)
                .Select(one => $"{one.Value.GetString()}"))
            .DefaultIfEmpty(string.Empty)
            .First();

        // 從設定檔搬進來的，順手鎖進保險箱，下一次就不必再碰設定檔
        Enumerable
            .Repeat(plain, 1)
            .Where(one => one.Length > 0)
            .Where(_ => here.Count == 0)
            .ToList()
            .ForEach(one => Keep(name, one));

        return plain;
    }

    #endregion

    #region 把一個秘密鎖進保險箱

    /// <summary>把一個秘密加密之後寫進保險箱</summary>
    /// <param name="name">這個秘密叫什麼</param>
    /// <param name="plain">要鎖起來的明文</param>
    public void Keep(string name, string plain)
    {
        // 保險箱現在有的，加上這一個
        var safe = Sealed().ToDictionary(one => one.Key, one => one.Value);

        safe[name] = Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, Scope));

        Directory.CreateDirectory(Path.GetDirectoryName(Safe)!);
        File.WriteAllText(Safe, JsonSerializer.Serialize(safe));
    }

    #endregion

    #region 為一支工具開一個保險箱

    /// <summary>為一支工具開一個保險箱：它存的每一筆都掛在它自己的代碼底下，讀不到別人的</summary>
    /// <param name="toolNo">哪一支工具</param>
    public ISecrets For(string toolNo) =>
        new ToolSecrets { Host = this, ToolNo = toolNo };

    #endregion

    #region 對照

    /// <summary>保險箱在哪裡</summary>
    private static string Safe { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HelixStudio", "secrets.bin");

    /// <summary>只有這台機器的這個帳號解得開；規範不允許 LocalMachine</summary>
    private const DataProtectionScope Scope = DataProtectionScope.CurrentUser;

    /// <summary>保險箱裡現在鎖著的每一個秘密，名字對到密文</summary>
    private static IReadOnlyList<KeyValuePair<string, string>> Sealed() =>
        Enumerable
            .Repeat(Safe, 1)
            .Where(File.Exists)
            .Select(one => Try(() => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(one))))
            .Where(one => one is not null)
            .SelectMany(one => one!)
            .ToList();

    /// <summary>做一件可能失敗的事，失敗就當作沒有結果</summary>
    /// <param name="work">要做的那一件事</param>
    private static T? Try<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (CryptographicException)
        {
            return default;
        }
        catch (Exception fault) when (fault is IOException or JsonException or FormatException)
        {
            return default;
        }
    }

    #endregion

    #region 模型

    /// <summary>一支工具手上那個保險箱</summary>
    private class ToolSecrets : ISecrets
    {
        /// <summary>平台的保險箱</summary>
        public SecretStore Host { get; set; } = new();

        /// <summary>這是哪一支工具的</summary>
        public string ToolNo { get; set; } = string.Empty;

        /// <summary>把一個秘密鎖起來</summary>
        /// <param name="name">這個秘密叫什麼</param>
        /// <param name="plain">要鎖起來的明文</param>
        public void Keep(string name, string plain) =>
            Host.Keep($"tool:{ToolNo}:{name}", plain);

        /// <summary>把一個秘密解開</summary>
        /// <param name="name">這個秘密叫什麼</param>
        public string Secret(string name) =>
            Host.Secret($"tool:{ToolNo}:{name}");
    }

    #endregion
}
