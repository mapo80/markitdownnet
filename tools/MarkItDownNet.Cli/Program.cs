using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MarkItDownNet;
using SkiaSharp;

if (args.Length == 0)
{
    Console.WriteLine("Usage: markitdownnet <convert|bench|ocr> [options]");
    return;
}

switch (args[0])
{
    case "convert":
        ConvertCommand(args.Skip(1).ToArray());
        break;
    case "bench":
        BenchCommand(args.Skip(1).ToArray());
        break;
    case "ocr":
        OcrCommand(args.Skip(1).ToArray());
        break;
    default:
        Console.WriteLine("Unknown command");
        break;
}

static void ConvertCommand(string[] args)
{
    string input = GetOption(args, "--input") ?? throw new ArgumentException("--input required");
    string mode = GetOption(args, "--mode") ?? "pre";
    string output = GetOption(args, "--out") ?? throw new ArgumentException("--out required");
    string? configPath = GetOption(args, "--config");

    TextModeConfig config = configPath != null ?
        JsonSerializer.Deserialize<TextModeConfig>(File.ReadAllText(configPath))! :
        new TextModeConfig();

    var text = File.ReadAllText(input);
    var md = TextModeConverter.Convert(text, mode, config);
    File.WriteAllText(output, md);
}

static void OcrCommand(string[] args)
{
    string inputDir = GetOption(args, "--input-dir") ?? throw new ArgumentException("--input-dir required");
    string outDir = GetOption(args, "--out-dir") ?? throw new ArgumentException("--out-dir required");
    string langs = GetOption(args, "--langs") ?? "eng";
    string psm = GetOption(args, "--psm") ?? "6";
    int minLong = int.Parse(GetOption(args, "--min-long") ?? "0");
    int threads = int.Parse(GetOption(args, "--threads") ?? "1");
    string outJson = GetOption(args, "--out-json") ?? "";
    string outHtml = GetOption(args, "--out-html") ?? "";
    string summaryMd = GetOption(args, "--summary-md") ?? "";

    var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".png",".tif",".tiff"};
    var files = Directory.GetFiles(inputDir, "*.*", SearchOption.AllDirectories)
        .Where(f=>exts.Contains(Path.GetExtension(f))).ToArray();
    var bag = new System.Collections.Concurrent.ConcurrentBag<OcrReportItem>();
    var po = new ParallelOptions{ MaxDegreeOfParallelism = threads };
    Parallel.ForEach(files, po, file =>
    {
        var rel = Path.GetRelativePath(inputDir, file);
        var txtPath = Path.Combine(outDir, Path.ChangeExtension(rel, ".txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(txtPath)!);
        string tempImg = file;
        try
        {
            using var bmp = SKBitmap.Decode(file);
            if (bmp != null)
            {
                SKBitmap proc = bmp;
                int longSide = Math.Max(bmp.Width, bmp.Height);
                if (minLong > 0 && longSide < minLong)
                {
                    float scale = (float)minLong / longSide;
                    int nw = (int)(bmp.Width * scale);
                    int nh = (int)(bmp.Height * scale);
                    var resized = bmp.Resize(new SKImageInfo(nw, nh), SKFilterQuality.High);
                    if (resized != null) proc = resized;
                }
                var gray = new SKBitmap(proc.Width, proc.Height, SKColorType.Gray8, SKAlphaType.Opaque);
                using (var canvas = new SKCanvas(gray))
                using (var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(new float[]{
                    0.2126f,0.2126f,0.2126f,0,0,
                    0.7152f,0.7152f,0.7152f,0,0,
                    0.0722f,0.0722f,0.0722f,0,0,
                    0,0,0,1,0
                }) })
                {
                    canvas.DrawBitmap(proc, 0, 0, paint);
                }
                using var img = SKImage.FromBitmap(gray);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                tempImg = Path.GetTempFileName();
                using var fs = File.OpenWrite(tempImg);
                data.SaveTo(fs);
            }
        }
        catch { }

        string tempBase = Path.GetTempFileName();
        File.Delete(tempBase);
        var psi = new ProcessStartInfo("tesseract", $"{tempImg} {tempBase} -l {langs} --psm {psm}") { RedirectStandardError=true, RedirectStandardOutput=true };
        var sw = Stopwatch.StartNew();
        int exit = 0; string err = string.Empty;
        using (var p = Process.Start(psi)!)
        {
            p.WaitForExit();
            sw.Stop();
            exit = p.ExitCode;
            err = p.StandardError.ReadToEnd();
        }
        string outTxtTemp = tempBase + ".txt";
        if (File.Exists(outTxtTemp))
        {
            File.Copy(outTxtTemp, txtPath, true);
            File.Delete(outTxtTemp);
        }
        if (tempImg != file && File.Exists(tempImg)) File.Delete(tempImg);
        bag.Add(new OcrReportItem{ image=file, txt=txtPath, ocr_ms=sw.Elapsed.TotalMilliseconds, exit_code=exit, error=string.IsNullOrWhiteSpace(err)?null:err.Trim() });
    });

    var report = new { root = inputDir, files = bag.OrderBy(b=>b.image).ToArray() };
    if (!string.IsNullOrEmpty(outJson))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outJson)!);
        File.WriteAllText(outJson, JsonSerializer.Serialize(report, new JsonSerializerOptions{WriteIndented=true}));
    }
    if (!string.IsNullOrEmpty(outHtml))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outHtml)!);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<html><body><table border='1'><tr><th>image</th><th>txt</th><th>ocr_ms</th><th>exit</th><th>error</th></tr>");
        foreach(var r in report.files)
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(r.image)}</td><td>{WebUtility.HtmlEncode(r.txt)}</td><td>{r.ocr_ms:F1}</td><td>{r.exit_code}</td><td>{WebUtility.HtmlEncode(r.error??"")}</td></tr>");
        sb.AppendLine("</table></body></html>");
        File.WriteAllText(outHtml, sb.ToString());
    }
    if (!string.IsNullOrEmpty(summaryMd))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(summaryMd)!);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("|image|txt|ocr_ms|exit|error|");
        sb.AppendLine("|---|---|---|---|---|");
        foreach(var r in report.files)
            sb.AppendLine($"|{r.image}|{r.txt}|{r.ocr_ms:F1}|{r.exit_code}|{r.error}|" );
        File.WriteAllText(summaryMd, sb.ToString());
    }
}

static void BenchCommand(string[] args)
{
    if (GetOption(args, "--input-dir") != null)
    {
        BenchDirCommand(args);
        return;
    }
    BenchSingleCommand(args);
}

static void BenchSingleCommand(string[] args)
{
    string input = GetOption(args, "--input") ?? throw new ArgumentException("--input required");
    string modes = GetOption(args, "--modes") ?? "pre,post-1S,post-2,python-cold,python-hot";
    string outJson = GetOption(args, "--out-json") ?? throw new ArgumentException("--out-json required");
    string outHtml = GetOption(args, "--out-html") ?? throw new ArgumentException("--out-html required");
    string summaryMd = GetOption(args, "--summary-md") ?? "";
    string pythonExe = GetOption(args, "--python-exe") ?? "python";
    string pythonMarkCmd = GetOption(args, "--python-markitdown-cmd") ?? "python -m markitdown";
    string pythonHotCmd = GetOption(args, "--python-hot-cmd") ?? "python tools/run_markitdown_hot.py";
    string configPath = GetOption(args, "--config");
    TextModeConfig config = configPath != null ?
        JsonSerializer.Deserialize<TextModeConfig>(File.ReadAllText(configPath))! : new TextModeConfig();

    var modeList = modes.Split(',');
    var results = new List<BenchResult>();
    foreach (var mode in modeList)
    {
        var br = RunMode(mode.Trim(), input, pythonExe, pythonMarkCmd, pythonHotCmd, config, "artifacts/outputs", Path.GetFileNameWithoutExtension(input));
        results.Add(br);
    }

    var reference = results.FirstOrDefault(r => r.Mode == "python-hot")?.Output;
    if (reference != null)
    {
        foreach (var r in results)
            r.Similarity = CompareOutputs(r.Output, reference);
    }

    var env = new {
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        dotnet = Environment.Version.ToString(),
        python = GetPythonVersion(pythonExe)
    };

    bool refHasTables = results.FirstOrDefault(r => r.Mode == "python-hot")?.Similarity?.Tables > 0;

    var modesJson = results.Select(r => {
        object? tables = null;
        if (r.Similarity != null)
        {
            tables = new {
                tables_count = r.Similarity.Tables,
                table_cell_f1 = refHasTables ? r.Similarity.TableCellF1 : null,
                pipes_lines_count = r.Similarity.PipeLines,
                median_pipes_per_line = r.Similarity.MedianPipesPerLine,
                max_pipes_per_line = r.Similarity.MaxPipesPerLine
            };
        }
        return new {
            mode = r.Mode,
            timing = new {
                trials = r.Trials.Select(t => new { md_ms = t }).ToArray(),
                avg_ms = r.AvgMs,
                std_ms = r.StdMs,
                p50_ms = r.P50Ms,
                p90_ms = r.P90Ms,
                p95_ms = r.P95Ms
            },
            quality_vs_python_hot = r.Similarity == null ? null : new {
                text = new {
                    cer_char = r.Similarity.Cer,
                    token_precision = r.Similarity.Precision,
                    token_recall = r.Similarity.Recall,
                    token_f1 = r.Similarity.F1
                },
                structure = new {
                    line_count = r.Similarity.LineCount,
                    line_f1 = r.Similarity.LineF1,
                    list_items = r.Similarity.ListItems,
                    max_list_depth = r.Similarity.MaxListDepth,
                    tables = tables
                }
            },
            paths = new { md = r.Output, md_norm = r.NormOutput }
        };
    }).ToArray();

    var jsonObj = new { file = input, modes = modesJson, env = env };
    var json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outJson, json);
    File.WriteAllText(outHtml, ReportHtml.Build(results));
    if (!string.IsNullOrEmpty(summaryMd))
        File.WriteAllText(summaryMd, ReportMarkdown.Build(results));
}

static void BenchDirCommand(string[] args)
{
    string inputDir = GetOption(args, "--input-dir") ?? throw new ArgumentException("--input-dir required");
    string modes = GetOption(args, "--modes") ?? "pre,post-1S,post-2,python-hot";
    string outJson = GetOption(args, "--out-json") ?? throw new ArgumentException("--out-json required");
    string outHtml = GetOption(args, "--out-html") ?? throw new ArgumentException("--out-html required");
    string summaryMd = GetOption(args, "--summary-md") ?? "";
    string pythonExe = GetOption(args, "--python-exe") ?? "python";
    string pythonMarkCmd = GetOption(args, "--python-markitdown-cmd") ?? "python -m markitdown";
    string pythonHotCmd = GetOption(args, "--python-hot-cmd") ?? "python tools/run_markitdown_hot.py";
    int threads = int.Parse(GetOption(args, "--threads") ?? "1");
    string tessLangs = GetOption(args, "--tesseract-langs") ?? "";
    string tessPsm = GetOption(args, "--tesseract-psm") ?? "";
    string configPath = GetOption(args, "--config");
    TextModeConfig config = configPath != null ?
        JsonSerializer.Deserialize<TextModeConfig>(File.ReadAllText(configPath))! : new TextModeConfig();

    var modeList = modes.Split(',').Select(m=>m.Trim()).ToArray();
    var txtFiles = Directory.GetFiles(inputDir, "*.txt", SearchOption.AllDirectories);
    var env = new {
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        dotnet = Environment.Version.ToString(),
        python = GetPythonVersion(pythonExe),
        markitdown = GetMarkitdownVersion(pythonExe)
    };
    var runConfig = new RunConfig
    {
        os = env.os,
        hw = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        dotnet = env.dotnet,
        python = env.python,
        markitdown = env.markitdown,
        threads = threads,
        tesseract_langs = tessLangs,
        tesseract_psm = tessPsm
    };

    string outputsRoot = Path.Combine("artifacts", "validation", "outputs");
    var bag = new System.Collections.Concurrent.ConcurrentBag<BenchFileData>();
    var po = new ParallelOptions { MaxDegreeOfParallelism = threads };
    Parallel.ForEach(txtFiles, po, file =>
    {
        var rel = Path.GetRelativePath(inputDir, file);
        var baseName = Path.ChangeExtension(rel, null);
        var results = new List<BenchResult>();
        foreach (var mode in modeList)
        {
            var br = RunMode(mode, file, pythonExe, pythonMarkCmd, pythonHotCmd, config, outputsRoot, baseName);
            results.Add(br);
        }
        var reference = results.FirstOrDefault(r => r.Mode == "python-hot")?.Output;
        if (reference != null)
        {
            foreach (var r in results)
                r.Similarity = CompareOutputs(r.Output, reference);
        }
        bag.Add(new BenchFileData { Txt = file, Results = results });
    });

    var filesData = bag.OrderBy(f => f.Txt).ToList();
    var filesJson = new List<object>();
    foreach (var f in filesData)
    {
        var runsObj = f.Results.ToDictionary(r => r.Mode, r => new {
            trials = r.Trials.Select(t => new { md_ms = t }).ToArray(),
            avg_ms = r.AvgMs,
            std_ms = r.StdMs,
            p50_ms = r.P50Ms,
            p90_ms = r.P90Ms,
            p95_ms = r.P95Ms
        });
        var pythonHot = f.Results.First(r => r.Mode == "python-hot");
        var post2 = f.Results.FirstOrDefault(r => r.Mode == "post-2");
        object? qualityObj = null;
        if (post2?.Similarity != null)
        {
            bool refHasTables = pythonHot.Similarity?.Tables > 0;
            object tables = new {
                tables_count = post2.Similarity.Tables,
                table_cell_f1 = refHasTables ? post2.Similarity.TableCellF1 : null,
                pipes_lines_count = post2.Similarity.PipeLines,
                median_pipes_per_line = post2.Similarity.MedianPipesPerLine,
                max_pipes_per_line = post2.Similarity.MaxPipesPerLine
            };
            qualityObj = new {
                text = new {
                    cer_char = post2.Similarity.Cer,
                    token_precision = post2.Similarity.Precision,
                    token_recall = post2.Similarity.Recall,
                    token_f1 = post2.Similarity.F1
                },
                structure = new {
                    line_count = post2.Similarity.LineCount,
                    line_f1 = post2.Similarity.LineF1,
                    list_items = post2.Similarity.ListItems,
                    max_list_depth = post2.Similarity.MaxListDepth,
                    tables = tables
                }
            };
        }
        var pathsObj = new {
            python_hot_md = pythonHot.Output,
            post2_md = post2?.Output
        };
        filesJson.Add(new { txt = f.Txt, env = env, runs = runsObj, quality_vs_python_hot = qualityObj, paths = pathsObj });
    }

    var aggregate = BuildAggregateData(filesData, modeList, inputDir);

    var rootObj = new { root = inputDir, modes = modeList, run_config = runConfig, files = filesJson, aggregate = aggregate };
    var json = JsonSerializer.Serialize(rootObj, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outJson, json);
    File.WriteAllText(outHtml, BuildBenchHtml(aggregate, runConfig, filesData));
    if (!string.IsNullOrEmpty(summaryMd))
        File.WriteAllText(summaryMd, BuildSummaryMarkdown(aggregate, runConfig));
}

static BenchResult RunMode(string mode, string input, string pythonExe, string pythonMarkCmd, string pythonHotCmd, TextModeConfig config, string outputsDir, string baseName)
{
    string tempOut = Path.GetTempFileName();
    var times = new List<double>();
    var text = File.ReadAllText(input);

    if (mode == "python-cold")
    {
        var (file, argsBase) = SplitCmd(pythonMarkCmd);
        var warmPsi = new ProcessStartInfo(file, $"{argsBase} {input} -o {tempOut}") { RedirectStandardOutput=true, RedirectStandardError=true };
        using (var warm = Process.Start(warmPsi)!) { warm.WaitForExit(); }
        for (int i=0;i<5;i++)
        {
            var psi = new ProcessStartInfo(file, $"{argsBase} {input} -o {tempOut}") { RedirectStandardOutput=true, RedirectStandardError=true };
            var sw = Stopwatch.StartNew();
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
    }
    else if (mode == "python-hot")
    {
        var (file, argsBase) = SplitCmd(pythonHotCmd);
        var psi = new ProcessStartInfo(file, $"{argsBase} {input} {tempOut}") { RedirectStandardOutput=true, RedirectStandardError=true };
        using var p = Process.Start(psi)!;
        string outText = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        var doc = JsonDocument.Parse(outText);
        times = doc.RootElement.GetProperty("trials").EnumerateArray().Select(e=>e.GetDouble()).ToList();
    }
    else
    {
        // warm-up
        TextModeConverter.Convert(text, mode, config);
        string last = string.Empty;
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            last = TextModeConverter.Convert(text, mode, config);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        File.WriteAllText(tempOut, last);
    }

    string fileBase = Path.Combine(outputsDir, baseName);
    Directory.CreateDirectory(Path.GetDirectoryName(fileBase)!);
    string outputPath = $"{fileBase}.{mode}.md";
    File.Copy(tempOut, outputPath, true);
    var norm = Normalize(File.ReadAllText(tempOut));
    string normPath = $"{fileBase}.{mode}.norm.md";
    File.WriteAllText(normPath, norm);
    var avg = times.Average();
    var std = Math.Sqrt(times.Select(t => Math.Pow(t - avg, 2)).Average());
    times.Sort();
    double Percentile(double p)
    {
        if (times.Count == 0) return 0;
        double idx = (times.Count - 1) * p;
        int i = (int)idx;
        double frac = idx - i;
        return times[i] + (i + 1 < times.Count ? (times[i + 1] - times[i]) * frac : 0);
    }
    var p50 = Percentile(0.5);
    var p90 = Percentile(0.9);
    var p95 = Percentile(0.95);
    return new BenchResult { Mode = mode, Trials = times, AvgMs = avg, StdMs = std, P50Ms = p50, P90Ms = p90, P95Ms = p95, Output = outputPath, NormOutput = normPath };
}


static string GetPythonVersion(string pythonExe)
{
    try
    {
        var psi = new ProcessStartInfo(pythonExe, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        var ver = (output + err).Trim();
        return ver;
    }
    catch
    {
        return "";
    }
}

static string GetMarkitdownVersion(string pythonExe)
{
    try
    {
        var psi = new ProcessStartInfo(pythonExe, "-c \"import markitdown, importlib.metadata as m; print(getattr(markitdown,'__version__',''))\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return output.Trim();
    }
    catch { return ""; }
}

static AggregateData BuildAggregateData(List<BenchFileData> files, string[] modes, string rootDir)
{
    var data = new AggregateData();
    var groups = files.GroupBy(f => GetDatasetName(rootDir, f.Txt));
    foreach (var g in groups)
    {
        var ds = new DatasetAggregate();
        ds.files = g.Count();
        foreach (var m in modes)
        {
            var times = g.SelectMany(f => f.Results.First(r => r.Mode == m).Trials).ToList();
            ds.timing[m] = BuildTimingStats(times);
            var sims = g.Select(f => f.Results.First(r => r.Mode == m).Similarity).Where(s => s != null).Select(s=>s!).ToList();
            ds.quality[m] = BuildQualityStats(sims);
        }
        data.by_dataset[g.Key] = ds;
    }
    var global = new GlobalAggregate();
    global.n_files = files.Count;
    foreach (var m in modes)
    {
        var times = files.SelectMany(f => f.Results.First(r => r.Mode == m).Trials).ToList();
        global.timing[m] = BuildTimingStats(times);
        var sims = files.Select(f => f.Results.First(r => r.Mode == m).Similarity).Where(s => s != null).Select(s=>s!).ToList();
        global.quality[m] = BuildQualityStats(sims);
    }
    data.global = global;
    return data;
}

static TimingStats BuildTimingStats(List<double> times)
{
    times.Sort();
    double avg = times.Count > 0 ? times.Average() : 0;
    double std = times.Count > 0 ? Math.Sqrt(times.Select(t => Math.Pow(t - avg, 2)).Average()) : 0;
    double Percentile(double p)
    {
        if (times.Count == 0) return 0;
        double idx = (times.Count - 1) * p;
        int i = (int)idx;
        double frac = idx - i;
        return times[i] + (i + 1 < times.Count ? (times[i + 1] - times[i]) * frac : 0);
    }
    return new TimingStats
    {
        avg_ms = avg,
        std_ms = std,
        p50_ms = Percentile(0.5),
        p90_ms = Percentile(0.9),
        p95_ms = Percentile(0.95)
    };
}

static QualityStats BuildQualityStats(List<Similarity> sims)
{
    if (sims.Count == 0) return new QualityStats();
    var tables = new TablesStats();
    tables.tables_count = sims.Average(s => s.Tables);
    var tf = sims.Where(s => s.TableCellF1.HasValue).Select(s => s.TableCellF1!.Value).ToList();
    tables.table_cell_f1 = tf.Count > 0 ? tf.Average() : null;
    tables.pipes_lines_count = sims.Average(s => s.PipeLines);
    tables.median_pipes_per_line = sims.Average(s => s.MedianPipesPerLine);
    tables.max_pipes_per_line = sims.Average(s => s.MaxPipesPerLine);
    return new QualityStats
    {
        cer_char = sims.Average(s => s.Cer),
        token_precision = sims.Average(s => s.Precision),
        token_recall = sims.Average(s => s.Recall),
        token_f1 = sims.Average(s => s.F1),
        line_f1 = sims.Average(s => s.LineF1),
        line_count = sims.Average(s => s.LineCount),
        list_items = sims.Average(s => s.ListItems),
        max_list_depth = sims.Average(s => s.MaxListDepth),
        tables = tables
    };
}

static string GetDatasetName(string rootDir, string filePath)
{
    var rel = Path.GetRelativePath(rootDir, filePath);
    var parts = rel.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
    return parts.Length > 0 ? parts[0] : string.Empty;
}

static string BuildBenchHtml(AggregateData agg, RunConfig cfg, List<BenchFileData> files)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<html><body>");
    sb.AppendLine("<h1>Benchmark</h1>");
    sb.AppendLine("<h2>Run config</h2><ul>");
    sb.AppendLine($"<li>OS: {WebUtility.HtmlEncode(cfg.os)} ({WebUtility.HtmlEncode(cfg.hw)})</li>");
    sb.AppendLine($"<li>.NET: {WebUtility.HtmlEncode(cfg.dotnet)}</li>");
    sb.AppendLine($"<li>Python: {WebUtility.HtmlEncode(cfg.python)} markitdown {WebUtility.HtmlEncode(cfg.markitdown)}</li>");
    sb.AppendLine($"<li>threads bench: {cfg.threads}</li>");
    sb.AppendLine($"<li>Tesseract langs: {WebUtility.HtmlEncode(cfg.tesseract_langs)} psm: {WebUtility.HtmlEncode(cfg.tesseract_psm)}</li>");
    sb.AppendLine("</ul>");

    sb.AppendLine("<h2>Timing (global)</h2><table border='1'><tr><th>Mode</th><th>avg±std (ms)</th><th>p50</th><th>p90</th><th>p95</th></tr>");
    foreach (var kv in agg.global.timing)
        sb.AppendLine($"<tr><td>{kv.Key}</td><td>{kv.Value.avg_ms:F1}±{kv.Value.std_ms:F1}</td><td>{kv.Value.p50_ms:F1}</td><td>{kv.Value.p90_ms:F1}</td><td>{kv.Value.p95_ms:F1}</td></tr>");
    sb.AppendLine("</table>");
    double deltaPost = agg.global.timing.ContainsKey("post-1S") && agg.global.timing.ContainsKey("post-2") && agg.global.timing["post-1S"].avg_ms>0
        ? (agg.global.timing["post-2"].avg_ms - agg.global.timing["post-1S"].avg_ms) / agg.global.timing["post-1S"].avg_ms * 100.0 : 0;
    double deltaNetPy = agg.global.timing.ContainsKey("python-hot") && agg.global.timing.ContainsKey("post-2") && agg.global.timing["python-hot"].avg_ms>0
        ? (agg.global.timing["post-2"].avg_ms - agg.global.timing["python-hot"].avg_ms) / agg.global.timing["python-hot"].avg_ms * 100.0 : 0;
    sb.AppendLine($"<p>Δ post-2 vs post-1S: {deltaPost:F1}%</p>");
    sb.AppendLine($"<p>Δ .NET vs python-hot: {deltaNetPy:F1}%</p>");

    sb.AppendLine("<h2>Timing by dataset</h2>");
    foreach (var ds in agg.by_dataset)
    {
        sb.AppendLine($"<h3>{ds.Key}</h3><table border='1'><tr><th>Mode</th><th>avg±std (ms)</th><th>p50</th><th>p90</th><th>p95</th></tr>");
        foreach (var kv in ds.Value.timing)
            sb.AppendLine($"<tr><td>{kv.Key}</td><td>{kv.Value.avg_ms:F1}±{kv.Value.std_ms:F1}</td><td>{kv.Value.p50_ms:F1}</td><td>{kv.Value.p90_ms:F1}</td><td>{kv.Value.p95_ms:F1}</td></tr>");
        sb.AppendLine("</table>");
        double dPost = ds.Value.timing.ContainsKey("post-1S") && ds.Value.timing.ContainsKey("post-2") && ds.Value.timing["post-1S"].avg_ms>0
            ? (ds.Value.timing["post-2"].avg_ms - ds.Value.timing["post-1S"].avg_ms) / ds.Value.timing["post-1S"].avg_ms * 100.0 : 0;
        double dNetPy = ds.Value.timing.ContainsKey("python-hot") && ds.Value.timing.ContainsKey("post-2") && ds.Value.timing["python-hot"].avg_ms>0
            ? (ds.Value.timing["post-2"].avg_ms - ds.Value.timing["python-hot"].avg_ms) / ds.Value.timing["python-hot"].avg_ms * 100.0 : 0;
        sb.AppendLine($"<p>Δ post-2 vs post-1S: {dPost:F1}% &nbsp; Δ .NET vs python-hot: {dNetPy:F1}%</p>");
    }

    sb.AppendLine("<h2>Quality vs python-hot (global)</h2><table border='1'><tr><th>Mode</th><th>CER</th><th>Token-F1</th><th>line_F1</th></tr>");
    foreach (var kv in agg.global.quality)
        sb.AppendLine($"<tr><td>{kv.Key}</td><td>{kv.Value.cer_char:F3}</td><td>{kv.Value.token_f1:F3}</td><td>{kv.Value.line_f1:F3}</td></tr>");
    sb.AppendLine("</table>");

    sb.AppendLine("<h2>Quality by dataset</h2>");
    foreach (var ds in agg.by_dataset)
    {
        sb.AppendLine($"<h3>{ds.Key}</h3><table border='1'><tr><th>Mode</th><th>CER</th><th>Token-F1</th><th>line_F1</th></tr>");
        foreach (var kv in ds.Value.quality)
            sb.AppendLine($"<tr><td>{kv.Key}</td><td>{kv.Value.cer_char:F3}</td><td>{kv.Value.token_f1:F3}</td><td>{kv.Value.line_f1:F3}</td></tr>");
        sb.AppendLine("</table>");
    }

    sb.AppendLine("<h2>Tables</h2>");
    sb.AppendLine("<h3>Global</h3><table border='1'><tr><th>Mode</th><th>tables_count</th><th>table_cell_F1</th><th>pipes_lines_count</th><th>median_pipes_per_line</th><th>max_pipes_per_line</th></tr>");
    foreach (var kv in agg.global.quality)
    {
        var t = kv.Value.tables;
        var f1 = t.table_cell_f1.HasValue ? t.table_cell_f1.Value.ToString("F3") : "null";
        sb.AppendLine($"<tr><td>{kv.Key}</td><td>{t.tables_count:F1}</td><td>{f1}</td><td>{t.pipes_lines_count:F1}</td><td>{t.median_pipes_per_line:F1}</td><td>{t.max_pipes_per_line:F1}</td></tr>");
    }
    sb.AppendLine("</table>");
    foreach (var ds in agg.by_dataset)
    {
        sb.AppendLine($"<h3>{ds.Key}</h3><table border='1'><tr><th>Mode</th><th>tables_count</th><th>table_cell_F1</th><th>pipes_lines_count</th><th>median_pipes_per_line</th><th>max_pipes_per_line</th></tr>");
        foreach (var kv in ds.Value.quality)
        {
            var t = kv.Value.tables;
            var f1 = t.table_cell_f1.HasValue ? t.table_cell_f1.Value.ToString("F3") : "null";
            sb.AppendLine($"<tr><td>{kv.Key}</td><td>{t.tables_count:F1}</td><td>{f1}</td><td>{t.pipes_lines_count:F1}</td><td>{t.median_pipes_per_line:F1}</td><td>{t.max_pipes_per_line:F1}</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    sb.AppendLine("<h2>Sample diffs</h2>");
    var diffs = files.Select(f => new { file = f, sim = f.Results.First(r => r.Mode == "post-2").Similarity, post = f.Results.First(r => r.Mode == "post-2"), py = f.Results.First(r => r.Mode == "python-hot") })
        .Where(x => x.sim != null).OrderBy(x => x.sim!.F1).ToList();
    foreach (var item in diffs.Take(3))
    {
        sb.AppendLine($"<h3>Worst: {WebUtility.HtmlEncode(Path.GetFileName(item.file.Txt))}</h3><table border='1'><tr><td><pre>");
        sb.Append(WebUtility.HtmlEncode(File.ReadAllText(item.post.NormOutput)));
        sb.AppendLine("</pre></td><td><pre>");
        sb.Append(WebUtility.HtmlEncode(File.ReadAllText(item.py.NormOutput)));
        sb.AppendLine("</pre></td></tr></table>");
    }
    foreach (var item in diffs.TakeLast(3))
    {
        sb.AppendLine($"<h3>Best: {WebUtility.HtmlEncode(Path.GetFileName(item.file.Txt))}</h3><table border='1'><tr><td><pre>");
        sb.Append(WebUtility.HtmlEncode(File.ReadAllText(item.post.NormOutput)));
        sb.AppendLine("</pre></td><td><pre>");
        sb.Append(WebUtility.HtmlEncode(File.ReadAllText(item.py.NormOutput)));
        sb.AppendLine("</pre></td></tr></table>");
    }

    sb.AppendLine("</body></html>");
    return sb.ToString();
}

static string BuildSummaryMarkdown(AggregateData agg, RunConfig cfg)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("## Run config");
    sb.AppendLine($"- OS: {cfg.os} ({cfg.hw})");
    sb.AppendLine($"- .NET: {cfg.dotnet}");
    sb.AppendLine($"- Python: {cfg.python} markitdown {cfg.markitdown}");
    sb.AppendLine($"- threads bench: {cfg.threads}");
    sb.AppendLine($"- Tesseract: {cfg.tesseract_langs} psm {cfg.tesseract_psm}");
    sb.AppendLine();

    sb.AppendLine("## Timing");
    sb.AppendLine("### Global");
    sb.AppendLine("|Mode|avg±std (ms)|p50|p90|p95|");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var kv in agg.global.timing)
        sb.AppendLine($"|{kv.Key}|{kv.Value.avg_ms:F1}±{kv.Value.std_ms:F1}|{kv.Value.p50_ms:F1}|{kv.Value.p90_ms:F1}|{kv.Value.p95_ms:F1}|");
    double gDeltaPost = agg.global.timing.ContainsKey("post-1S") && agg.global.timing.ContainsKey("post-2") && agg.global.timing["post-1S"].avg_ms>0
        ? (agg.global.timing["post-2"].avg_ms - agg.global.timing["post-1S"].avg_ms) / agg.global.timing["post-1S"].avg_ms * 100.0 : 0;
    double gDeltaNetPy = agg.global.timing.ContainsKey("python-hot") && agg.global.timing.ContainsKey("post-2") && agg.global.timing["python-hot"].avg_ms>0
        ? (agg.global.timing["post-2"].avg_ms - agg.global.timing["python-hot"].avg_ms) / agg.global.timing["python-hot"].avg_ms * 100.0 : 0;
    sb.AppendLine($"Δ post-2 vs post-1S: {gDeltaPost:F1}%");
    sb.AppendLine($"Δ .NET vs python-hot: {gDeltaNetPy:F1}%");
    sb.AppendLine();
    foreach (var ds in agg.by_dataset)
    {
        sb.AppendLine($"### {ds.Key}");
        sb.AppendLine("|Mode|avg±std (ms)|p50|p90|p95|");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var kv in ds.Value.timing)
            sb.AppendLine($"|{kv.Key}|{kv.Value.avg_ms:F1}±{kv.Value.std_ms:F1}|{kv.Value.p50_ms:F1}|{kv.Value.p90_ms:F1}|{kv.Value.p95_ms:F1}|");
        double dPost = ds.Value.timing.ContainsKey("post-1S") && ds.Value.timing.ContainsKey("post-2") && ds.Value.timing["post-1S"].avg_ms>0
            ? (ds.Value.timing["post-2"].avg_ms - ds.Value.timing["post-1S"].avg_ms) / ds.Value.timing["post-1S"].avg_ms * 100.0 : 0;
        double dNetPy = ds.Value.timing.ContainsKey("python-hot") && ds.Value.timing.ContainsKey("post-2") && ds.Value.timing["python-hot"].avg_ms>0
            ? (ds.Value.timing["post-2"].avg_ms - ds.Value.timing["python-hot"].avg_ms) / ds.Value.timing["python-hot"].avg_ms * 100.0 : 0;
        sb.AppendLine($"Δ post-2 vs post-1S: {dPost:F1}%  Δ .NET vs python-hot: {dNetPy:F1}%");
        sb.AppendLine();
    }

    sb.AppendLine("## Quality");
    sb.AppendLine("### Global");
    sb.AppendLine("|Mode|CER|Token-F1|line_F1|");
    sb.AppendLine("|---|---|---|---|");
    foreach (var kv in agg.global.quality)
        sb.AppendLine($"|{kv.Key}|{kv.Value.cer_char:F3}|{kv.Value.token_f1:F3}|{kv.Value.line_f1:F3}|");
    sb.AppendLine();
    foreach (var ds in agg.by_dataset)
    {
        sb.AppendLine($"### {ds.Key}");
        sb.AppendLine("|Mode|CER|Token-F1|line_F1|");
        sb.AppendLine("|---|---|---|---|");
        foreach (var kv in ds.Value.quality)
            sb.AppendLine($"|{kv.Key}|{kv.Value.cer_char:F3}|{kv.Value.token_f1:F3}|{kv.Value.line_f1:F3}|");
        sb.AppendLine();
    }

    sb.AppendLine("## Tables");
    bool anyTables = agg.global.quality.Values.Any(q => q.tables.tables_count > 0);
    if (!anyTables)
    {
        sb.AppendLine("Tables: none detected in this validation set.");
    }
    else
    {
        sb.AppendLine("|Mode|tables_count|table_cell_F1 (post-2)|");
        sb.AppendLine("|---|---|---|");
        foreach (var kv in agg.global.quality)
        {
            var t = kv.Value.tables;
            string f1 = kv.Key == "post-2" && t.table_cell_f1.HasValue ? t.table_cell_f1.Value.ToString("F3") : "";
            sb.AppendLine($"|{kv.Key}|{t.tables_count:F1}|{f1}|");
        }
    }
    sb.AppendLine();

    sb.AppendLine("## Key findings");
    sb.AppendLine($"- Δ post-2 vs post-1S: {gDeltaPost:F1}%");
    sb.AppendLine($"- Δ .NET vs python-hot: {gDeltaNetPy:F1}%");
    var post2Qual = agg.global.quality.ContainsKey("post-2") ? agg.global.quality["post-2"] : new QualityStats();
    sb.AppendLine($"- Global Token-F1 (post-2): {post2Qual.token_f1:F3}");
    sb.AppendLine($"- Global CER (post-2): {post2Qual.cer_char:F3}");
    sb.AppendLine($"- Tables detected (post-2): {post2Qual.tables.tables_count:F1}");
    return sb.ToString();
}

static string? GetOption(string[] args, string name)
{
    for (int i=0;i<args.Length;i++)
        if (args[i]==name && i+1<args.Length)
            return args[i+1];
    return null;
}

static (string file, string args) SplitCmd(string cmd)
{
    var parts = cmd.Split(' ',2,StringSplitOptions.RemoveEmptyEntries);
    var file = parts[0];
    var args = parts.Length>1?parts[1]:string.Empty;
    return (file,args);
}

static Similarity CompareOutputs(string candidatePath, string referencePath)
{
    var cand = Normalize(File.ReadAllText(candidatePath));
    var refText = Normalize(File.ReadAllText(referencePath));
    double cer = refText.Length==0?0:(double)Levenshtein(cand, refText) / refText.Length;
    var candTokens = cand.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var refTokens = refText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var candCounts = candTokens.GroupBy(t=>t).ToDictionary(g=>g.Key,g=>g.Count());
    var refCounts = refTokens.GroupBy(t=>t).ToDictionary(g=>g.Key,g=>g.Count());
    double tp = 0;
    foreach (var kv in candCounts)
        if (refCounts.TryGetValue(kv.Key, out var rc)) tp += Math.Min(kv.Value, rc);
    double precision = tp / Math.Max(candTokens.Length,1);
    double recall = tp / Math.Max(refTokens.Length,1);
    double f1 = tp==0?0:2*precision*recall/(precision+recall);
    var candLines = cand.Split('\n');
    var refLines = refText.Split('\n');
    var lineF1 = F1Scores(candLines, refLines);
    var structure = StructureCounts(cand);
    var refStructure = StructureCounts(refText);
    double headingMatch = HeadingMatchRatio(cand, refText);
    double? tableCellF1 = TableCellF1(cand, refText);
    double lineRatio = refStructure.LineCount==0?1.0:(double)structure.LineCount/refStructure.LineCount;
    return new Similarity{Cer=cer, Precision=precision, Recall=recall, F1=f1,
        HeadingLevels=structure.HeadingLevels, ListItems=structure.ListItems, MaxListDepth=structure.MaxListDepth,
        CodeBlocks=structure.CodeBlocks, HorizontalRules=structure.HorizontalRules, Tables=structure.Tables,
        HeadingMatch=headingMatch, TableCellF1=tableCellF1, LineCount=structure.LineCount, LineRatio=lineRatio,
        LineF1=lineF1, PipeLines=structure.PipeLines, MedianPipesPerLine=structure.MedianPipesPerLine, MaxPipesPerLine=structure.MaxPipesPerLine};
}

static string Normalize(string text)
{
    text = text.Replace("\r\n", "\n");
    var lines = text.Split('\n');
    for (int i=0;i<lines.Length;i++) lines[i]=lines[i].TrimEnd();
    text = string.Join("\n", lines);
    text = Regex.Replace(text, "\n{3,}", "\n\n");
    return text;
}

static int Levenshtein(string s, string t)
{
    var dp = new int[s.Length+1, t.Length+1];
    for (int i=0;i<=s.Length;i++) dp[i,0]=i;
    for (int j=0;j<=t.Length;j++) dp[0,j]=j;
    for (int i=1;i<=s.Length;i++)
        for (int j=1;j<=t.Length;j++)
        {
            int cost = s[i-1]==t[j-1]?0:1;
            dp[i,j]=Math.Min(Math.Min(dp[i-1,j]+1, dp[i,j-1]+1), dp[i-1,j-1]+cost);
        }
    return dp[s.Length,t.Length];
}

static double F1Scores(string[] cand, string[] reference)
{
    var candCounts = cand.GroupBy(l=>l).ToDictionary(g=>g.Key,g=>g.Count());
    var refCounts = reference.GroupBy(l=>l).ToDictionary(g=>g.Key,g=>g.Count());
    double tp=0;
    foreach(var kv in candCounts)
        if (refCounts.TryGetValue(kv.Key, out var rc)) tp+=Math.Min(kv.Value, rc);
    double prec = tp / Math.Max(cand.Length,1);
    double rec = tp / Math.Max(reference.Length,1);
    return tp==0?0:2*prec*rec/(prec+rec);
}

static StructureMetrics StructureCounts(string text)
{
    int[] headLevels = new int[6];
    int listItems=0,maxDepth=0,codeBlocks=0,hrs=0; bool inCode=false;
    var pipeCounts = new List<int>();
    var lines = text.Split('\n');
    int lineCount = lines.Length;
    for (int idx=0; idx<lines.Length; idx++)
    {
        var line = lines[idx];
        if (line.StartsWith("```")) { inCode = !inCode; if(inCode) codeBlocks++; continue; }
        if (inCode) continue;
        var headingMatch = Regex.Match(line, @"^(#{1,6}) ");
        if (headingMatch.Success)
        {
            int level = headingMatch.Groups[1].Value.Length;
            headLevels[level-1]++;
            continue;
        }
        var listMatch = Regex.Match(line, @"^( *)(-|\d+\.) ");
        if (listMatch.Success)
        {
            listItems++;
            int depth = listMatch.Groups[1].Value.Length/2 + 1;
            if (depth>maxDepth) maxDepth=depth;
        }
        if (line.Trim() == "---") hrs++;
        int pipes = line.Count(c=>c=='|');
        if (pipes>0) pipeCounts.Add(pipes);
    }
    int pipeLines = pipeCounts.Count;
    double median = pipeLines==0?0:pipeCounts.OrderBy(x=>x).ElementAt(pipeLines/2);
    if (pipeLines>0 && pipeLines%2==0)
    {
        var ordered = pipeCounts.OrderBy(x=>x).ToArray();
        median = (ordered[pipeLines/2-1]+ordered[pipeLines/2])/2.0;
    }
    int maxPipes = pipeLines==0?0:pipeCounts.Max();
    int tables = ExtractTables(text).Count;
    return new StructureMetrics{HeadingLevels=headLevels,ListItems=listItems,MaxListDepth=maxDepth,CodeBlocks=codeBlocks,HorizontalRules=hrs,Tables=tables,LineCount=lineCount,PipeLines=pipeLines,MedianPipesPerLine=median,MaxPipesPerLine=maxPipes};
}

static List<List<List<string>>> ExtractTables(string text)
{
    var lines = text.Split('\n');
    var tables = new List<List<List<string>>>();
    int i = 0;
    while (i < lines.Length)
    {
        var header = lines[i];
        if (Regex.IsMatch(header, @"^\s*\|") && i + 1 < lines.Length && IsSeparatorLine(lines[i + 1]))
        {
            var table = new List<List<string>> { ParseTableRow(header) };
            i += 2; // skip header and separator
            while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*\|.*\|\s*$"))
            {
                table.Add(ParseTableRow(lines[i]));
                i++;
            }
            tables.Add(table);
            continue;
        }
        i++;
    }
    return tables;
}

static bool IsSeparatorLine(string line)
    => Regex.IsMatch(line.Trim(), @"^\|(?:\s*:?-+:?\s*\|)+\s*$");

static List<string> ParseTableRow(string line)
    => line.Trim().Trim('|')
        .Split('|')
        .Select(c => Regex.Replace(c.Trim(), @"\s+", " "))
        .ToList();

static double? TableCellF1(string cand, string reference)
{
    var ct = ExtractTables(cand);
    var rt = ExtractTables(reference);
    if (rt.Count == 0)
        return null;
    int total = 0;
    foreach (var rTable in rt)
        total += rTable.Count * rTable[0].Count;
    int match = 0;
    int count = Math.Min(ct.Count, rt.Count);
    for(int t=0;t<count;t++)
    {
        var cTable=ct[t];
        var rTable=rt[t];
        int rows = Math.Min(cTable.Count, rTable.Count);
        int cols = Math.Min(cTable[0].Count, rTable[0].Count);
        for(int i=0;i<rows;i++)
            for(int j=0;j<cols;j++)
                if (cTable[i][j].Trim()==rTable[i][j].Trim()) match++;
    }
    return total==0? null : (double)match/total;
}

static double HeadingMatchRatio(string cand, string reference)
{
    var candHeads = ExtractHeadings(cand);
    var refHeads = ExtractHeadings(reference);
    int matched=0;
    foreach(var h in refHeads)
    {
        if(candHeads.Any(ch => Jaccard(ch,h) >= 0.7)) matched++;
    }
    return refHeads.Count==0?1.0:(double)matched/refHeads.Count;
}

static List<string> ExtractHeadings(string text)
    => text.Split('\n').Where(l=>Regex.IsMatch(l,"^#+ ")).Select(l=>Regex.Replace(l,"^#+ ","").Trim()).ToList();

static double Jaccard(string a, string b)
{
    var at = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var bt = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var inter = at.Intersect(bt).Count();
    var union = at.Union(bt).Count();
    return union==0?0:(double)inter/union;
}

record BenchResult
{
    public string Mode { get; set; } = "";
    public List<double> Trials { get; set; } = new();
    public double AvgMs { get; set; }
    public double StdMs { get; set; }
    public double P50Ms { get; set; }
    public double P90Ms { get; set; }
    public double P95Ms { get; set; }
    public string Output { get; set; } = "";
    public string NormOutput { get; set; } = "";
    public Similarity? Similarity { get; set; }
}

record Similarity
{
    public double Cer { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
    public int[] HeadingLevels { get; set; } = Array.Empty<int>();
    public int ListItems { get; set; }
    public int MaxListDepth { get; set; }
    public int CodeBlocks { get; set; }
    public int HorizontalRules { get; set; }
    public int Tables { get; set; }
    public double HeadingMatch { get; set; }
    public double? TableCellF1 { get; set; }
    public int LineCount { get; set; }
    public double LineRatio { get; set; }
    public double LineF1 { get; set; }
    public int PipeLines { get; set; }
    public double MedianPipesPerLine { get; set; }
    public int MaxPipesPerLine { get; set; }
}

record StructureMetrics
{
    public int[] HeadingLevels { get; set; } = new int[6];
    public int ListItems { get; set; }
    public int MaxListDepth { get; set; }
    public int CodeBlocks { get; set; }
    public int HorizontalRules { get; set; }
    public int Tables { get; set; }
    public int LineCount { get; set; }
    public int PipeLines { get; set; }
    public double MedianPipesPerLine { get; set; }
    public int MaxPipesPerLine { get; set; }
}

record OcrReportItem
{
    public string image { get; set; } = "";
    public string txt { get; set; } = "";
    public double ocr_ms { get; set; }
    public int exit_code { get; set; }
    public string? error { get; set; }
}

record BenchFileData
{
    public string Txt { get; set; } = "";
    public List<BenchResult> Results { get; set; } = new();
}

record RunConfig
{
    public string os { get; set; } = "";
    public string hw { get; set; } = "";
    public string dotnet { get; set; } = "";
    public string python { get; set; } = "";
    public string markitdown { get; set; } = "";
    public int threads { get; set; }
    public string tesseract_langs { get; set; } = "";
    public string tesseract_psm { get; set; } = "";
}

record AggregateData
{
    public Dictionary<string, DatasetAggregate> by_dataset { get; set; } = new();
    public GlobalAggregate global { get; set; } = new();
}

record DatasetAggregate
{
    public int files { get; set; }
    public Dictionary<string, TimingStats> timing { get; set; } = new();
    public Dictionary<string, QualityStats> quality { get; set; } = new();
}

record GlobalAggregate
{
    public int n_files { get; set; }
    public Dictionary<string, TimingStats> timing { get; set; } = new();
    public Dictionary<string, QualityStats> quality { get; set; } = new();
}

record TimingStats
{
    public double avg_ms { get; set; }
    public double std_ms { get; set; }
    public double p50_ms { get; set; }
    public double p90_ms { get; set; }
    public double p95_ms { get; set; }
}

record QualityStats
{
    public double cer_char { get; set; }
    public double token_precision { get; set; }
    public double token_recall { get; set; }
    public double token_f1 { get; set; }
    public double line_f1 { get; set; }
    public double line_count { get; set; }
    public double list_items { get; set; }
    public double max_list_depth { get; set; }
    public TablesStats tables { get; set; } = new();
}

record TablesStats
{
    public double tables_count { get; set; }
    public double? table_cell_f1 { get; set; }
    public double pipes_lines_count { get; set; }
    public double median_pipes_per_line { get; set; }
    public double max_pipes_per_line { get; set; }
}
