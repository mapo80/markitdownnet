using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace MarkItDownNet.Cli;

public static class MdManifestCommand
{
    public static void Run(string[] args)
    {
        string txtDir = GetOption(args, "--txt-dir") ?? throw new ArgumentException("--txt-dir required");
        string outPath = GetOption(args, "--out") ?? throw new ArgumentException("--out required");
        if (!Directory.Exists(txtDir)) throw new DirectoryNotFoundException(txtDir);
        var files = Directory.GetFiles(txtDir, "*.mdready.txt", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            Console.Error.WriteLine("no files found");
            Environment.ExitCode = 1;
            return;
        }
        var list = new List<ManifestFile>();
        var seen = new HashSet<string>();
        long totalBytes = 0;
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(txtDir, file).Replace('\\', '/');
            if (!seen.Add(rel))
            {
                Console.Error.WriteLine($"duplicate rel={rel}");
                Environment.ExitCode = 1;
                return;
            }
            long bytes = new FileInfo(file).Length;
            string sha = Sha256.FromFile(file);
            totalBytes += bytes;
            list.Add(new ManifestFile { rel = rel, sha256 = sha, bytes = bytes });
        }
        list = list.OrderBy(f => f.rel).ToList();
        var manifest = new ManifestRoot
        {
            schema = "mdready-manifest@1",
            generated_utc = Clock.Iso8601(),
            files = list,
            total_files = list.Count,
            total_bytes = totalBytes
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }

    public class ManifestRoot
    {
        public string schema { get; set; } = string.Empty;
        public string generated_utc { get; set; } = string.Empty;
        public List<ManifestFile> files { get; set; } = new();
        public int total_files { get; set; }
        public long total_bytes { get; set; }
    }

    public class ManifestFile
    {
        public string rel { get; set; } = string.Empty;
        public string sha256 { get; set; } = string.Empty;
        public long bytes { get; set; }
    }
}

