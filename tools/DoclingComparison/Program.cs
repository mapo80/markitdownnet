using BitMiracle.LibTiff.Classic;
using MarkItDownNet;
using SkiaSharp;
using System.Text;
using System.Text.Json;
using TesseractOCR.InteropDotNet;

namespace DoclingComparison;

record WordRecord(int Page, string Text, double X, double Y, double W, double H);

class Program
{
    static async Task Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var dataRoot = Path.Combine(repoRoot, "docling", "tests", "data");
        var groundRoot = Path.Combine(dataRoot, "groundtruth", "docling_v2");
        var reportPath = Path.Combine(repoRoot, "docs", "docling_comparison.md");

        LibraryLoader.Instance.CustomSearchPath = "/lib/x86_64-linux-gnu";
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", "/usr/share/tesseract-ocr/5/tessdata");

        var files = args.Length > 0 ? args : Directory.GetFiles(Path.Combine(dataRoot, "pdf"), "*.pdf")
            .Concat(Directory.GetFiles(Path.Combine(dataRoot, "tiff"), "*.tif"))
            .OrderBy(f => f)
            .ToArray();

        var converter = new MarkItDownConverter(new MarkItDownOptions { NormalizeMarkdown = false });

        var sb = new StringBuilder();
        sb.AppendLine("# Docling comparison report");
        sb.AppendLine();
        sb.AppendLine("| File | Markdown similarity | Docling words | MarkItDown words | Matched | Match % | BBox MAE |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        double mdSimTotal = 0;
        int fileCount = 0;
        int totalGtWords = 0;
        int totalMkWords = 0;
        int totalMatch = 0;
        double totalBboxSum = 0;

        foreach (var file in files)
        {
            var baseName = Path.GetFileNameWithoutExtension(file);
            var isPdf = Path.GetExtension(file).ToLower() == ".pdf";
            var mime = isPdf ? "application/pdf" : "image/jpeg";
            var inputFile = isPdf ? file : ConvertTiffToJpeg(file);

            var gtMarkdownPath = Path.Combine(groundRoot, baseName + ".md");
            var gtPagesPath = Path.Combine(groundRoot, baseName + ".pages.json");
            if (!File.Exists(gtMarkdownPath) || !File.Exists(gtPagesPath))
            {
                continue;
            }

            try
            {
                var gtMarkdown = await File.ReadAllTextAsync(gtMarkdownPath);
                var result = await converter.ConvertAsync(inputFile, mime);
                double mdSim = Similarity(gtMarkdown, result.Markdown);
                var mkWords = result.Words;

                double bboxSum = 0;
                int matchCount = 0;
                int gtWordCount = 0;
                using (var stream = File.OpenRead(gtPagesPath))
                using (var doc = await JsonDocument.ParseAsync(stream))
                {
                    foreach (var page in doc.RootElement.EnumerateArray())
                    {
                        int pageNo = page.GetProperty("page_no").GetInt32();
                        double width = page.GetProperty("size").GetProperty("width").GetDouble();
                        double height = page.GetProperty("size").GetProperty("height").GetDouble();
                        var cells = page.GetProperty("parsed_page").GetProperty("word_cells").EnumerateArray();
                        var mPage = mkWords.Where(w => w.Page == pageNo + 1).ToList();
                        int i = 0;
                        foreach (var cell in cells)
                        {
                            var rect = cell.GetProperty("rect");
                            double x0 = rect.GetProperty("r_x0").GetDouble();
                            double y0 = rect.GetProperty("r_y0").GetDouble();
                            double x1 = rect.GetProperty("r_x1").GetDouble();
                            double y2 = rect.GetProperty("r_y2").GetDouble();
                            double x = x0 / width;
                            double y = y0 / height;
                            double w = (x1 - x0) / width;
                            double h = Math.Abs(y2 - y0) / height;
                            if (i < mPage.Count)
                            {
                                var m = mPage[i].BBox;
                                bboxSum += Math.Abs(x - m.X) + Math.Abs(y - m.Y) + Math.Abs(w - m.Width) + Math.Abs(h - m.Height);
                                matchCount++;
                            }
                            i++;
                        }
                        gtWordCount += i;
                    }
                }

                double bboxMae = matchCount > 0 ? bboxSum / (matchCount * 4) : double.NaN;
                double matchRate = gtWordCount > 0 ? (double)matchCount / gtWordCount : double.NaN;
                sb.AppendLine($"| {baseName} | {mdSim:F2} | {gtWordCount} | {mkWords.Count} | {matchCount} | {matchRate:P2} | {bboxMae:F4} |");

                mdSimTotal += mdSim;
                fileCount++;
                totalGtWords += gtWordCount;
                totalMkWords += mkWords.Count;
                totalMatch += matchCount;
                totalBboxSum += bboxSum;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {file}: {ex}");
                sb.AppendLine($"| {baseName} | ERROR: {ex.GetType().Name} | - | - | - | - | - |");
            }
        }

        double overallMdSim = fileCount > 0 ? mdSimTotal / fileCount : double.NaN;
        double overallMatchRate = totalGtWords > 0 ? (double)totalMatch / totalGtWords : double.NaN;
        double overallBboxMae = totalMatch > 0 ? totalBboxSum / (totalMatch * 4) : double.NaN;

        sb.AppendLine();
        sb.AppendLine($"| **Overall** | {overallMdSim:F2} | {totalGtWords} | {totalMkWords} | {totalMatch} | {overallMatchRate:P2} | {overallBboxMae:F4} |");

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, sb.ToString());
    }

    static string ConvertTiffToJpeg(string path)
    {
        using var tiff = Tiff.Open(path, "r");
        int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var raster = new int[width * height];
        if (!tiff.ReadRGBAImage(width, height, raster))
            throw new InvalidOperationException("Could not read TIFF");

        using var bitmap = new SKBitmap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int rgba = raster[(height - y - 1) * width + x];
                var color = new SKColor((byte)Tiff.GetR(rgba), (byte)Tiff.GetG(rgba), (byte)Tiff.GetB(rgba), (byte)Tiff.GetA(rgba));
                bitmap.SetPixel(x, y, color);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 90))
        using (var fs = File.OpenWrite(tmp))
        {
            data.SaveTo(fs);
        }
        return tmp;
    }

    static double Similarity(string a, string b)
    {
        var tokensA = new HashSet<string>(a.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var tokensB = new HashSet<string>(b.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (tokensA.Count == 0 && tokensB.Count == 0)
            return 1.0;

        int intersection = 0;
        foreach (var token in tokensA)
            if (tokensB.Contains(token))
                intersection++;

        int union = tokensA.Count + tokensB.Count - intersection;
        return union > 0 ? (double)intersection / union : 0.0;
    }
}

