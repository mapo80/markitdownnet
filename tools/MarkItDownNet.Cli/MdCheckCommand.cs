using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace MarkItDownNet.Cli;

public static class MdCheckCommand
{
    public static void Run(string[] args)
    {
        string txtDir = GetOption(args, "--txt-dir") ?? throw new ArgumentException("--txt-dir required");
        string manifestPath = GetOption(args, "--manifest") ?? throw new ArgumentException("--manifest required");
        if (!Directory.Exists(txtDir)) throw new DirectoryNotFoundException(txtDir);
        var manifest = JsonSerializer.Deserialize<MdManifestCommand.ManifestRoot>(File.ReadAllText(manifestPath))!;
        var dict = manifest.files.ToDictionary(f => f.rel);
        bool fail = false;
        foreach (var kv in dict)
        {
            var rel = kv.Key; var info = kv.Value;
            var path = Path.Combine(txtDir, rel);
            if (!File.Exists(path))
            {
                Console.WriteLine($"MISMATCH rel={rel} expected_sha256={info.sha256} found_sha256=missing");
                fail = true; continue;
            }
            var sha = Sha256.FromFile(path);
            long bytes = new FileInfo(path).Length;
            if (!sha.Equals(info.sha256, StringComparison.OrdinalIgnoreCase) || bytes != info.bytes)
            {
                Console.WriteLine($"MISMATCH rel={rel} expected_sha256={info.sha256} found_sha256={sha}");
                fail = true;
            }
        }
        var actual = Directory.GetFiles(txtDir, "*.mdready.txt", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(txtDir, f).Replace('\\', '/'));
        foreach (var rel in actual)
        {
            if (!dict.ContainsKey(rel))
            {
                var sha = Sha256.FromFile(Path.Combine(txtDir, rel));
                Console.WriteLine($"MISMATCH rel={rel} expected_sha256=missing found_sha256={sha}");
                fail = true;
            }
        }
        if (fail) Environment.ExitCode = 1;
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}

