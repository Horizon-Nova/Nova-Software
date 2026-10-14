using System.Security.Cryptography;

namespace Vaeron.Kernel.Windows;

/// <summary>本機加解密。CurrentUser 範圍：同機同使用者才能解開。</summary>
/// <remarks>
/// 換使用者、換機、機器密碼重設會使既有密文永久無法解開,
/// 故解密失敗必須是可恢復的流程,不得使程式無法啟動。
/// </remarks>
public static class DpapiProtector
{
    public static Result<byte[]> Protect(byte[] plain)
    {
        try
        {
            return Result<byte[]>.Ok(
                ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException ex)
        {
            return Result<byte[]>.Fail(ResultKind.InfrastructureError, ex.Message);
        }
    }

    public static Result<byte[]> Unprotect(byte[] cipher)
    {
        try
        {
            return Result<byte[]>.Ok(
                ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException ex)
        {
            return Result<byte[]>.Fail(ResultKind.InfrastructureError, ex.Message);
        }
    }
}
