namespace HelixStudio.Contract;

/// <summary>工具用平台的保險箱存自己的秘密</summary>
/// <remarks>
/// 平台在載入工具之後把它塞進 <see cref="ITool.Secrets"/>。
/// 存進去的每一筆自動掛在這一支工具的代碼底下，一支工具讀不到另一支的。
/// </remarks>
public interface ISecrets
{
    /// <summary>把一個秘密鎖起來</summary>
    /// <param name="name">這個秘密叫什麼</param>
    /// <param name="plain">要鎖起來的明文</param>
    void Keep(string name, string plain);

    /// <summary>把一個秘密解開；沒有這一筆就是空字串</summary>
    /// <param name="name">這個秘密叫什麼</param>
    string Secret(string name);
}
