using MarkItDownNet;
using System.Diagnostics;
using TesseractOCR.InteropDotNet;

record TimingRecord(string FileName, string Type, double MarkdownMs, double BBoxMs);

class Program
{
    static async Task Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var dataRoot = Path.Combine(repoRoot, "docling", "tests", "data");

        LibraryLoader.Instance.CustomSearchPath = "/usr/lib/x86_64-linux-gnu";
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", "/usr/share/tesseract-ocr/5/tessdata");

        var pdfFiles = Directory.GetFiles(Path.Combine(dataRoot, "pdf"), "*.pdf");
        var tiffFiles = Directory.GetFiles(Path.Combine(dataRoot, "tiff"), "*.tif");
        var pngFiles = Directory.GetFiles(dataRoot, "*.png");

        var files = pdfFiles.Concat(tiffFiles).Concat(pngFiles).OrderBy(f => f).ToArray();

        var converter = new MarkItDownConverter(new MarkItDownOptions { NormalizeMarkdown = false });
        var records = new List<TimingRecord>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var type = ext switch
            {
                ".pdf" => "pdf",
                ".tif" or ".tiff" => "tiff",
                ".png" => "png",
                _ => "unknown"
            };
            var mime = type == "pdf" ? "application/pdf" : type == "png" ? "image/png" : "image/tiff";

            var swMd = Stopwatch.StartNew();
            var result = await converter.ConvertAsync(file, mime);
            swMd.Stop();

            var swBb = Stopwatch.StartNew();
            var _ = System.Text.Json.JsonSerializer.Serialize(result.Words);
            swBb.Stop();

            records.Add(new TimingRecord(Path.GetFileName(file), type, swMd.Elapsed.TotalMilliseconds, swBb.Elapsed.TotalMilliseconds));
        }

        Console.WriteLine("| File | Type | Markdown ms | BBox ms |");
        Console.WriteLine("| --- | --- | --- | --- |");
        foreach (var r in records)
        {
            Console.WriteLine($"| {r.FileName} | {r.Type} | {r.MarkdownMs:F2} | {r.BBoxMs:F2} |");
        }

        Console.WriteLine();
        Console.WriteLine("| Type | Avg Markdown ms | Avg BBox ms |");
        Console.WriteLine("| --- | --- | --- |");
        foreach (var grp in records.GroupBy(r => r.Type))
        {
            Console.WriteLine($"| {grp.Key} | {grp.Average(r => r.MarkdownMs):F2} | {grp.Average(r => r.BBoxMs):F2} |");
        }
        Console.WriteLine($"| **Overall** | {records.Average(r => r.MarkdownMs):F2} | {records.Average(r => r.BBoxMs):F2} |");
    }
}
