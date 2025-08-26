using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RapidOcrNet;

if (args.Length != 1)
{
    Console.WriteLine("usage: <dataset-directory>");
    return;
}

var datasetDir = args[0];
var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };
var images = Directory.EnumerateFiles(datasetDir)
    .Where(f => exts.Contains(Path.GetExtension(f)))
    .OrderBy(f => f)
    .ToList();

var versions = new[] { OcrVersion.V3, OcrVersion.V4, OcrVersion.V5 };
var timingTable = new Dictionary<string, Dictionary<OcrVersion, long>>();

foreach (var image in images)
{
    var baseName = Path.GetFileName(image);
    var timings = new Dictionary<OcrVersion, long>();

    foreach (var version in versions)
    {
        using var ocr = new RapidOcr();
        ocr.InitModels(OcrLanguage.Latin, version, Environment.ProcessorCount);

        // warm up
        ocr.Detect(image, RapidOcrOptions.Default);

        long sum = 0;
        string? text = null;
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = ocr.Detect(image, RapidOcrOptions.Default);
            sw.Stop();
            if (i == 0)
            {
                text = res.StrRes.Trim();
            }
            sum += sw.ElapsedMilliseconds;
        }
        var avg = sum / 5;
        timings[version] = avg;

        var outPath = $"{image}.rapidocr.{version.ToString().ToLower()}.md";
        File.WriteAllText(outPath, text ?? string.Empty);
    }

    timingTable[baseName] = timings;
}

// write markdown table
var mdPath = Path.Combine(datasetDir, "rapidocr_timings.md");
using var swMd = new StreamWriter(mdPath);
swMd.WriteLine("|image|v3_ms|v4_ms|v5_ms|");
swMd.WriteLine("|---|---|---|---|");
foreach (var kv in timingTable)
{
    var row = $"|{kv.Key}|{kv.Value[OcrVersion.V3]}|{kv.Value[OcrVersion.V4]}|{kv.Value[OcrVersion.V5]}|";
    swMd.WriteLine(row);
}
