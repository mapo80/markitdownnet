using System;
using System.IO;
using System.Security.Cryptography;

namespace MarkItDownNet.Cli;

public static class Sha256
{
    public static string FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

