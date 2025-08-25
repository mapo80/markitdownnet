using MarkItDownNet;
using System.Diagnostics;
using Tesseract;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/BustaPagaNet <image-path> [tesseract|rapidocr]");
    return;
}

var imagePath = args[0];
var engine = args.Length > 1 ? args[1].ToLowerInvariant() : "tesseract";
var baseName = Path.Combine(Path.GetDirectoryName(imagePath)!, Path.GetFileNameWithoutExtension(imagePath));
var tessData = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
var options = new MarkItDownOptions
{
    OcrDataPath = tessData,
    OcrLanguage = "ita",
    OcrEngine = engine == "rapidocr" ? OcrEngine.RapidOcr : OcrEngine.Tesseract,
};
var converter = new MarkItDownConverter(options);
TesseractEnviornment.CustomSearchPath = "/usr/lib/x86_64-linux-gnu";

var sw = Stopwatch.StartNew();
var result = await converter.ConvertAsync(imagePath, "image/jpeg");
sw.Stop();

await File.WriteAllTextAsync(baseName + $"_{engine}.md", result.Markdown);
Console.WriteLine($"Elapsed ms ({engine}): {sw.Elapsed.TotalMilliseconds:F2}");
