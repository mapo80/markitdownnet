using MarkItDownNet;
using System.Diagnostics;
using Tesseract;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/BustaPagaNet <image-path>");
    return;
}

var imagePath = args[0];
var baseName = Path.Combine(Path.GetDirectoryName(imagePath)!, Path.GetFileNameWithoutExtension(imagePath));
var tessData = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
var options = new MarkItDownOptions
{
    OcrDataPath = tessData,
    OcrLanguages = "ita"
};
var converter = new MarkItDownConverter(options);
TesseractEnviornment.CustomSearchPath = "/usr/lib/x86_64-linux-gnu";

var sw = Stopwatch.StartNew();
var result = await converter.ConvertAsync(imagePath, "image/jpeg");
sw.Stop();

using var pix = Pix.LoadFromFile(imagePath);
using var engine = new TesseractEngine(tessData ?? string.Empty, "ita", EngineMode.LstmOnly);
engine.DefaultPageSegMode = PageSegMode.Auto;
using var page = engine.Process(pix);
var ocrText = page.GetText();

await File.WriteAllTextAsync(baseName + "_dotnet.txt", ocrText);
await File.WriteAllTextAsync(baseName + "_markitdownnet.md", result.Markdown);

Console.WriteLine($"Elapsed ms: {sw.Elapsed.TotalMilliseconds:F2}");
