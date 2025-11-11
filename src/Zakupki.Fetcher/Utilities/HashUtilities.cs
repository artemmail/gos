using System;
using System.Security.Cryptography;

namespace Zakupki.Fetcher.Utilities;

public static class HashUtilities
{
    public static string ComputeSha256Hex(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash);
    }
}
