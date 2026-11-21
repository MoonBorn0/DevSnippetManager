using System;
using System.Security.Cryptography;
using System.Text;

namespace DevSnippetManager.Services;

public static class SecretProtector
{
    private static readonly byte[] Entropy =
    {
        0x44, 0x65, 0x76, 0x56, 0x61, 0x75, 0x6C, 0x74,
        0x2D, 0x53, 0x65, 0x63, 0x72, 0x65, 0x74, 0x2D,
        0x50, 0x72, 0x6F, 0x74, 0x65, 0x63, 0x74, 0x6F, 0x72
    };

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(plain);
        byte[] protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
        {
            return string.Empty;
        }

        try
        {
            byte[] protectedBytes = Convert.FromBase64String(protectedBase64);
            byte[] bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
