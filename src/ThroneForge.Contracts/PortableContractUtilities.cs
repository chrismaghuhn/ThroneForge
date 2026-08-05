using System.Security.Cryptography;

namespace ThroneForge.Contracts;

internal static class PortableContractUtilities
{
    public static byte[] ComputeSha256(byte[] bytes)
    {
#if NETSTANDARD2_1
        using var algorithm = SHA256.Create();
        return algorithm.ComputeHash(bytes);
#else
        return SHA256.HashData(bytes);
#endif
    }

    public static string ToLowerHex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        var result = new char[bytes.Length * 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
        }

        return new string(result);
    }
}
