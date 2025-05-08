using System.Security.Cryptography;
using System.Text;

namespace FleetManager.Helpers;

public static class ETagGenerator
{
    public static string GetETag(string key, byte[] contentBytes)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var combinedBytes = Combine(keyBytes, contentBytes);

        return GenerateETag(combinedBytes);
    }

    private static byte[] Combine(byte[] a, byte[] b)
    {
        var combined = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, combined, 0, a.Length);
        Buffer.BlockCopy(b, 0, combined, a.Length, b.Length);

        return combined;
    }

    private static string GenerateETag(byte[] combinedBytes)
    {
        using var md5 = MD5.Create();
        var md5Hash = md5.ComputeHash(combinedBytes);
        return $"\"{Convert.ToBase64String(md5Hash)}\"";
    }
}
