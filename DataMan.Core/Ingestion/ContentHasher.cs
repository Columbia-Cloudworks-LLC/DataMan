using System.Security.Cryptography;
using System.Text;

namespace DataMan.Core.Ingestion;

public static class ContentHasher
{
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return ToHex(SHA256.HashData(stream));
    }

    public static string Sha256Text(string text) => ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
