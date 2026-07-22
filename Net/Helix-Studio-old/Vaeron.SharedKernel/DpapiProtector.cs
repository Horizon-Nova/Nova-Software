using System.Security.Cryptography;

namespace Vaeron.SharedKernel;

public sealed class DpapiProtector
{
    public byte[] Protect(byte[] plainBytes)
        => ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

    public (bool Success, string Message, byte[]? PlainBytes) TryUnprotect(byte[] protectedBytes)
    {
        try
        {
            return (true, "成功", ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException ex)
        {
            return (false, $"解密失敗：{ex.GetType().Name}", null);
        }
    }
}
