using RapidLayoutNet;
using RapidTableNet;
using SkiaSharp;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

record TableInfo(float[] Box, double RecognitionTimeMs, int CellCount, int TokenCount);
record ImageInfo(string FileName, double LayoutTimeMs, List<TableInfo> Tables);

class Program
{
    static void Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var dataDir = Path.Combine(repoRoot, "dataset", "training");
        var layoutModel = Path.Combine(repoRoot, "src", "RapidLayoutNet", "models", "PP-DocLayout-S_infer.onnx");
        var tableModelDir = Path.Combine(repoRoot, "src", "RapidTableNet", "models");
        var reportPath = Path.Combine(repoRoot, "docs", "rapidtable_training_report.md");

        using var layout = new LayoutDetector();
        layout.InitModel(layoutModel);
        using var tableRec = new TableRecognizer();
        tableRec.InitModel(TableModel.SlanetPlus, tableModelDir);

        var imageFiles = Directory.EnumerateFiles(dataDir)
            .Where(f => {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
            })
            .OrderBy(f => f)
            .ToList();

        var images = new List<ImageInfo>();

        foreach (var path in imageFiles)
        {
            var fileName = Path.GetFileName(path);
            using var src = SKBitmap.Decode(path);
            var layoutSw = Stopwatch.StartNew();
            var boxes = layout.Detect(src);
            layoutSw.Stop();
            var tableBoxes = boxes.Where(b => b.Label == LayoutLabel.Table).ToArray();
            var tables = new List<TableInfo>();
            foreach (var box in tableBoxes)
            {
                var rect = new SKRectI((int)box.X1, (int)box.Y1, (int)box.X2, (int)box.Y2);
                using var tableBmp = new SKBitmap(rect.Width, rect.Height);
                using (var canvas = new SKCanvas(tableBmp))
                {
                    canvas.DrawBitmap(src, rect, new SKRectI(0, 0, rect.Width, rect.Height));
                }
                var recogSw = Stopwatch.StartNew();
                var result = tableRec.Detect(tableBmp);
                recogSw.Stop();
                tables.Add(new TableInfo(
                    Box: [box.X1, box.Y1, box.X2, box.Y2],
                    RecognitionTimeMs: recogSw.Elapsed.TotalMilliseconds,
                    CellCount: result.CellBoxes.Count,
                    TokenCount: result.Structure.Count));
            }
            images.Add(new ImageInfo(fileName, layoutSw.Elapsed.TotalMilliseconds, tables));

            var json = new
            {
                layout_time_ms = layoutSw.Elapsed.TotalMilliseconds,
                tables = tables.Select(t => new
                {
                    box = t.Box,
                    recognition_time_ms = t.RecognitionTimeMs,
                    cell_count = t.CellCount,
                    token_count = t.TokenCount
                })
            };
            var jsonPath = Path.Combine(dataDir, fileName + ".rapidtable.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        }

        var sb = new StringBuilder();
        sb.AppendLine("# RapidTableNet training dataset analysis");
        sb.AppendLine();
        sb.AppendLine("Model: SlanetPlus");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Image | Tables | Layout ms |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var img in images)
        {
            sb.AppendLine($"| {img.FileName} | {img.Tables.Count} | {img.LayoutTimeMs:F1} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Detailed results");
        sb.AppendLine();
        foreach (var img in images)
        {
            sb.AppendLine($"### {img.FileName}");
            sb.AppendLine($"Layout time: {img.LayoutTimeMs:F1} ms");
            sb.AppendLine($"Detected tables: {img.Tables.Count}");
            sb.AppendLine();
            if (img.Tables.Count > 0)
            {
                sb.AppendLine("| Table | Bounding Box [x1,y1,x2,y2] | Recognition ms | Cells | Tokens |");
                sb.AppendLine("| --- | --- | --- | --- | --- |");
                for (int i = 0; i < img.Tables.Count; i++)
                {
                    var t = img.Tables[i];
                    sb.AppendLine($"| {i + 1} | [{t.Box[0]:F0},{t.Box[1]:F0},{t.Box[2]:F0},{t.Box[3]:F0}] | {t.RecognitionTimeMs:F1} | {t.CellCount} | {t.TokenCount} |");
                }
                sb.AppendLine();
                sb.AppendLine($"Output file: `dataset/training/{img.FileName}.rapidtable.json`");
            }
            sb.AppendLine();
        }

        File.WriteAllText(reportPath, sb.ToString());
    }
}
