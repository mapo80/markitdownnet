using MarkItDownNet;
using SkiaSharp;
using System.Text;
using System.Text.Json;
using TesseractOCR.InteropDotNet;

namespace FunsdComparison;

record BBox(double X, double Y, double W, double H);
record WordRecord(int Page, string Text, BBox BBox);

class Program
{
    static async Task Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var dataRoot = Path.Combine(repoRoot, "data", "dataset", "testing_data");
        var imageRoot = Path.Combine(dataRoot, "images");
        var annotRoot = Path.Combine(dataRoot, "annotations");
        var outputRoot = Path.Combine(repoRoot, "docs", "funsd_markitdown");
        var reportPath = Path.Combine(repoRoot, "docs", "funsd_comparison.md");

        Directory.CreateDirectory(outputRoot);

        // TesseractOCR relies on system libraries; no custom search path required on Linux
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", "/usr/share/tesseract-ocr/5/tessdata");

        var converter = new MarkItDownConverter(new MarkItDownOptions { NormalizeMarkdown = false });

        var sb = new StringBuilder();
        sb.AppendLine("# FUNSD test set comparison");
        sb.AppendLine();
        sb.AppendLine("Confronto tra le bounding box delle parole annotate nel dataset di test FUNSD e quelle prodotte da MarkItDownNet. `GT` indica il numero di parole nel ground truth, `MK` il numero di parole estratte, `Matched` il numero di parole confrontate e `Δ%` la media dell'errore assoluto per ciascuna coordinata normalizzata.");
        sb.AppendLine();
        sb.AppendLine("| File | GT words | MK words | Matched | Δx% | Δy% | Δw% | Δh% |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        double totalDx = 0, totalDy = 0, totalDw = 0, totalDh = 0;
        int totalGt = 0, totalMk = 0, totalMatch = 0;

        var annotFiles = Directory.GetFiles(annotRoot, "*.json").OrderBy(f => f).ToArray();
        foreach (var annotFile in annotFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(annotFile);
            var imageFile = Path.Combine(imageRoot, baseName + ".png");
            if (!File.Exists(imageFile))
                continue;

            using var sk = SKBitmap.Decode(imageFile);
            double width = sk.Width;
            double height = sk.Height;

            var gtWords = new List<WordRecord>();
            using (var stream = File.OpenRead(annotFile))
            using (var doc = await JsonDocument.ParseAsync(stream))
            {
                foreach (var block in doc.RootElement.GetProperty("form").EnumerateArray())
                {
                    foreach (var word in block.GetProperty("words").EnumerateArray())
                    {
                        var box = word.GetProperty("box");
                        double x0 = box[0].GetDouble();
                        double y0 = box[1].GetDouble();
                        double x1 = box[2].GetDouble();
                        double y1 = box[3].GetDouble();
                        double x = x0 / width;
                        double y = y0 / height;
                        double w = (x1 - x0) / width;
                        double h = (y1 - y0) / height;
                        string text = word.GetProperty("text").GetString() ?? string.Empty;
                        gtWords.Add(new WordRecord(1, text, new BBox(x, y, w, h)));
                    }
                }
            }

            var result = await converter.ConvertAsync(imageFile, "image/png");
            await File.WriteAllTextAsync(Path.Combine(outputRoot, baseName + ".md"), result.Markdown);
            await File.WriteAllTextAsync(Path.Combine(outputRoot, baseName + ".words.json"), JsonSerializer.Serialize(result.Words, new JsonSerializerOptions { WriteIndented = true }));

            var mkWords = result.Words.Where(w => w.Page == 1).ToList();
            int match = Math.Min(gtWords.Count, mkWords.Count);
            double sumDx = 0, sumDy = 0, sumDw = 0, sumDh = 0;
            for (int i = 0; i < match; i++)
            {
                var gt = gtWords[i].BBox;
                var mk = mkWords[i].BBox;
                sumDx += Math.Abs(gt.X - mk.X);
                sumDy += Math.Abs(gt.Y - mk.Y);
                sumDw += Math.Abs(gt.W - mk.Width);
                sumDh += Math.Abs(gt.H - mk.Height);
            }
            double avgDx = match > 0 ? (sumDx / match) * 100.0 : double.NaN;
            double avgDy = match > 0 ? (sumDy / match) * 100.0 : double.NaN;
            double avgDw = match > 0 ? (sumDw / match) * 100.0 : double.NaN;
            double avgDh = match > 0 ? (sumDh / match) * 100.0 : double.NaN;

            sb.AppendLine($"| {baseName} | {gtWords.Count} | {mkWords.Count} | {match} | {avgDx:F2} | {avgDy:F2} | {avgDw:F2} | {avgDh:F2} |");

            totalGt += gtWords.Count;
            totalMk += mkWords.Count;
            totalMatch += match;
            totalDx += sumDx;
            totalDy += sumDy;
            totalDw += sumDw;
            totalDh += sumDh;
        }

        double overallDx = totalMatch > 0 ? (totalDx / totalMatch) * 100.0 : double.NaN;
        double overallDy = totalMatch > 0 ? (totalDy / totalMatch) * 100.0 : double.NaN;
        double overallDw = totalMatch > 0 ? (totalDw / totalMatch) * 100.0 : double.NaN;
        double overallDh = totalMatch > 0 ? (totalDh / totalMatch) * 100.0 : double.NaN;

        sb.AppendLine();
        sb.AppendLine($"| **Overall** | {totalGt} | {totalMk} | {totalMatch} | {overallDx:F2} | {overallDy:F2} | {overallDw:F2} | {overallDh:F2} |");

        sb.AppendLine();
        sb.AppendLine("## Riproduzione");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("export PATH=$HOME/.dotnet:$PATH");
        sb.AppendLine("export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$PWD/tools/FunsdComparison/bin/Debug/net9.0/runtimes/linux-x64/native");
        sb.AppendLine("dotnet run --project tools/FunsdComparison");
        sb.AppendLine("```");

        await File.WriteAllTextAsync(reportPath, sb.ToString());
    }
}
