using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarkItDownNet;

if (args.Length == 0)
{
    Console.WriteLine("usage: extract|compare [options]");
    return;
}

var command = args[0];
var argDict = ParseArgs(args.Skip(1).ToArray());

if (command == "extract")
    Extract(argDict);
else if (command == "compare")
    Compare(argDict);
else
    Console.WriteLine("unknown command");

static Dictionary<string, string> ParseArgs(string[] a)
{
    var d = new Dictionary<string, string>();
    for (int i = 0; i < a.Length - 1; i += 2)
        d[a[i]] = a[i + 1];
    return d;
}

static void Extract(Dictionary<string, string> o)
{
    var inputDir = o["--input-dir"];
    var outDir = o["--out-dir"];
    var langs = o["--langs"];
    var psm = o["--psm"];
    var threads = o["--threads"];
    var python = o.GetValueOrDefault("--python-exe", "python3");
    var refresh = o.GetValueOrDefault("--refresh", "markitdownnet");

    var refreshSet = new HashSet<string>(refresh.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", threads);

    Directory.CreateDirectory(outDir);
    if (refreshSet.Contains("markitdownnet"))
    {
        var md = Path.Combine(outDir, "markitdownnet");
        if (Directory.Exists(md)) Directory.Delete(md, true);
        Directory.CreateDirectory(md);
    }
    if (refreshSet.Contains("pytesseract"))
    {
        var py = Path.Combine(outDir, "pytesseract");
        if (Directory.Exists(py)) Directory.Delete(py, true);
        Directory.CreateDirectory(py);
    }

    var options = new MarkItDownOptions
    {
        OcrLanguages = langs,
        OcrDataPath = "/usr/share/tesseract-ocr/5/tessdata",
        OcrPsm = int.Parse(psm),
        OcrOem = Tesseract.EngineMode.LstmOnly,
        OcrThreads = int.Parse(threads),
        NormalizeMarkdown = false,
        DetectBulletLists = false,
        MergeLines = false,
        MinimumNativeWordThreshold = int.MaxValue,
        OcrForceRaster = true,
        OcrUserDpi = 300,
        OcrPreBinarize = false,
        OcrDeskewMinAngleDeg = 2.0,
        OcrColorDepth = OcrColorDepth.Grayscale8bpp
    };
    var converter = new MarkItDownConverter(options);

    var exts = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".pdf" }, StringComparer.OrdinalIgnoreCase);
    var datasets = Directory.GetDirectories(inputDir).Where(d => Path.GetFileName(d) != "_ocr");
    var timings = new Dictionary<string, Dictionary<string, long>>();
    long totalMark = 0, totalPy = 0;

    foreach (var datasetPath in datasets)
    {
        var dataset = Path.GetFileName(datasetPath);
        var files = Directory.EnumerateFiles(datasetPath, "*.*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f)));
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file) + ".txt";
            var rel = dataset + "/" + name;

            if (refreshSet.Contains("markitdownnet"))
            {
                var sw = Stopwatch.StartNew();
                var textMark = OcrMark(converter, file, out var angle, out var desk);
                sw.Stop();
                var tMark = sw.ElapsedMilliseconds;
                var outMarkDir = Path.Combine(outDir, "markitdownnet", dataset);
                Directory.CreateDirectory(outMarkDir);
                File.WriteAllText(Path.Combine(outMarkDir, name), textMark);
                totalMark += tMark;
                var deskTxt = desk ? $"{angle:F2}°" : "skipped";
                Console.WriteLine($"{dataset}/{Path.GetFileName(file)} | DPI {options.OcrUserDpi} | depth {options.OcrColorDepth} | deskew {deskTxt} | PSM {options.OcrPsm} | OEM {options.OcrOem} | {tMark} ms");
                if (!timings.TryGetValue(rel, out var dict)) timings[rel] = dict = new();
                dict["markitdownnet"] = tMark;
            }

            if (refreshSet.Contains("pytesseract"))
            {
                var images = GetImages(file).ToList();
                var sw = Stopwatch.StartNew();
                var textPy = OcrPy(images, python, langs, psm);
                sw.Stop();
                var tPy = sw.ElapsedMilliseconds;
                var outPyDir = Path.Combine(outDir, "pytesseract", dataset);
                Directory.CreateDirectory(outPyDir);
                File.WriteAllText(Path.Combine(outPyDir, name), textPy);
                totalPy += tPy;
                Console.WriteLine($"{dataset}/{Path.GetFileName(file)} | pytesseract | {tPy} ms");
                if (!timings.TryGetValue(rel, out var dict)) timings[rel] = dict = new();
                dict["pytesseract"] = tPy;
            }
        }
    }

    File.WriteAllText(Path.Combine(outDir, "timings.json"), JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }));
    if (refreshSet.Contains("markitdownnet")) Console.WriteLine($"TOTAL markitdownnet {totalMark} ms");
    if (refreshSet.Contains("pytesseract")) Console.WriteLine($"TOTAL pytesseract {totalPy} ms");
}

static IEnumerable<string> GetImages(string path)
{
    if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var prefix = Path.Combine(tmp, "page");
        var psi = new ProcessStartInfo("pdftoppm", $"-r 300 -png \"{path}\" \"{prefix}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using (var p = Process.Start(psi))
            p?.WaitForExit();
        var imgs = Directory.GetFiles(tmp, "page-*.png").OrderBy(f => f).ToList();
        return imgs;
    }
    else
    {
        return new[] { path };
    }
}

static string OcrMark(MarkItDownConverter conv, string file, out double angle, out bool desk)
{
    var res = conv.ConvertAsync(file, GetMime(file)).Result;
    angle = conv.LastDeskewAngle;
    desk = conv.LastDeskewApplied;
    return res.Markdown.Trim();
}

static string OcrPy(IEnumerable<string> images, string py, string lang, string psm)
{
    var sb = new StringBuilder();
    foreach (var img in images)
    {
        var psi = new ProcessStartInfo(py)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("from PIL import Image;import pytesseract,sys;print(pytesseract.image_to_string(Image.open(sys.argv[1]), lang=sys.argv[2], config='--psm '+sys.argv[3]))");
        psi.ArgumentList.Add(img);
        psi.ArgumentList.Add(lang);
        psi.ArgumentList.Add(psm);
        psi.Environment["TESSDATA_PREFIX"] = "/usr/share/tesseract-ocr/5/tessdata";
        using var p = Process.Start(psi);
        var output = p!.StandardOutput.ReadToEnd();
        p.WaitForExit();
        sb.AppendLine(output.Trim());
    }
    return sb.ToString().Trim();
}

static string GetMime(string path)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();
    return ext switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".tif" => "image/tiff",
        ".tiff" => "image/tiff",
        ".bmp" => "image/bmp",
        _ => "image/png"
    };
}

static void Compare(Dictionary<string, string> o)
{
    var ocrDir = o["--ocr-dir"];
    var outJson = o["--out-json"];
    var outMd = o["--out-md"];

    var markDir = Path.Combine(ocrDir, "markitdownnet");
    var pyDir = Path.Combine(ocrDir, "pytesseract");
    var timings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, long>>>(File.ReadAllText(Path.Combine(ocrDir, "timings.json")))!;

    var files = new List<FileMetrics>();
    foreach (var dataset in Directory.GetDirectories(pyDir))
    {
        var ds = Path.GetFileName(dataset);
        foreach (var file in Directory.GetFiles(dataset, "*.txt"))
        {
            var name = Path.GetFileName(file);
            var rel = ds + "/" + name;
            var gt = Normalize(File.ReadAllText(file));
            var hyp = Normalize(File.ReadAllText(Path.Combine(markDir, ds, name)));

            var cer = Cer(gt, hyp);
            var (tp, tr, tf) = TokenScores(gt, hyp);
            var (lcRef, lcHyp, lf) = LineScores(gt, hyp);
            timings.TryGetValue(rel, out var t);
            long tm = 0, tpyt = 0;
            if (t != null)
            {
                t.TryGetValue("markitdownnet", out tm);
                t.TryGetValue("pytesseract", out tpyt);
            }
            files.Add(new FileMetrics
            {
                dataset = ds,
                file = Path.GetFileNameWithoutExtension(name),
                cer_char = cer,
                token_precision = tp,
                token_recall = tr,
                token_f1 = tf,
                line_count_ref = lcRef,
                line_count_hyp = lcHyp,
                line_f1 = lf,
                timing_markitdownnet = tm,
                timing_pytesseract = tpyt
            });
        }
    }

    var byDataset = files.GroupBy(f => f.dataset).ToDictionary(g => g.Key, g => new Aggregate
    {
        cer_avg = g.Average(x => x.cer_char),
        token_f1_avg = g.Average(x => x.token_f1),
        line_f1_avg = g.Average(x => x.line_f1),
        n_files = g.Count()
    });
    var global = new Aggregate
    {
        cer_avg = files.Average(x => x.cer_char),
        token_f1_avg = files.Average(x => x.token_f1),
        line_f1_avg = files.Average(x => x.line_f1),
        n_files = files.Count
    };

    var icdarSmoke = new HashSet<string> { "cTDaR_t00014", "cTDaR_t00015", "cTDaR_t00016" };
    var pubSmoke = new HashSet<string> { "PMC1064078_table_0", "PMC1064078_table_2", "PMC1064078_table_6" };
    static void CheckSmoke(List<FileMetrics> files, string ds, HashSet<string> set)
    {
        var sub = files.Where(f => f.dataset == ds && set.Contains(f.file)).ToList();
        if (sub.Count == 0) return;
        var tf = sub.Average(f => f.token_f1);
        var lf = sub.Average(f => f.line_f1);
        if (tf < 0.80 || lf < 0.50)
        {
            Console.Error.WriteLine($"Smoke test failed for {ds}: token_f1={tf:F2} line_f1={lf:F2}");
            Environment.Exit(1);
        }
    }

    var runConfig = new Dictionary<string, string>
    {
        ["os"] = RuntimeInformation.OSDescription.Trim(),
        ["cpu"] = CpuName(),
        ["dotnet"] = Environment.Version.ToString(),
        ["python"] = Proc(o.GetValueOrDefault("--python-exe", "python3"), "--version").Trim(),
        ["tesseract"] = Proc("tesseract", "--version").Split('\n')[0].Trim(),
        ["langs"] = o.GetValueOrDefault("--langs", "eng"),
        ["psm"] = o.GetValueOrDefault("--psm", "6"),
        ["threads"] = o.GetValueOrDefault("--threads", "1"),
        ["timings_unit"] = "ms"
    };

    var bench = new
    {
        task = "OCR-BENCH",
        run_config = runConfig,
        files,
        aggregate = new { by_dataset = byDataset, global }
    };
    Directory.CreateDirectory(Path.GetDirectoryName(outJson)!);
    File.WriteAllText(outJson, JsonSerializer.Serialize(bench, new JsonSerializerOptions { WriteIndented = true }));

    var sb = new StringBuilder();
    sb.AppendLine("# OCR Benchmark (markitdownnet vs pytesseract)\n");
    sb.AppendLine("## Global");
    sb.AppendLine("| scope | CER | Token-F1 | line_F1 | n_files |");
    sb.AppendLine($"| Global | {global.cer_avg:F4} | {global.token_f1_avg:F4} | {global.line_f1_avg:F4} | {global.n_files} |");
    sb.AppendLine();
    sb.AppendLine("## By dataset");
    sb.AppendLine("| scope | CER | Token-F1 | line_F1 | n_files |");
    foreach (var kv in byDataset)
        sb.AppendLine($"| {kv.Key} | {kv.Value.cer_avg:F4} | {kv.Value.token_f1_avg:F4} | {kv.Value.line_f1_avg:F4} | {kv.Value.n_files} |");
    sb.AppendLine();
    sb.AppendLine("## Top-5 worst files");
    sb.AppendLine("| dataset/file | cer_char | token_f1 | line_f1 | note |");
    foreach (var f in files.OrderByDescending(x => x.cer_char).Take(5))
        sb.AppendLine($"| {f.dataset}/{f.file} | {f.cer_char:F4} | {f.token_f1:F4} | {f.line_f1:F4} | |");
    sb.AppendLine();
    sb.AppendLine("## Run config");
    foreach (var kv in runConfig)
        sb.AppendLine($"- {kv.Key}: {kv.Value}");
    Directory.CreateDirectory(Path.GetDirectoryName(outMd)!);
    File.WriteAllText(outMd, sb.ToString());

    foreach (var kv in byDataset)
        Console.WriteLine($"{kv.Key} | token_F1 {kv.Value.token_f1_avg:F4} | line_F1 {kv.Value.line_f1_avg:F4}");
    Console.WriteLine($"Global | token_F1 {global.token_f1_avg:F4} | line_F1 {global.line_f1_avg:F4}");
    CheckSmoke(files, "ICDAR", icdarSmoke);
    CheckSmoke(files, "PUBTABLES", pubSmoke);
    if (global.token_f1_avg < 0.80 || global.line_f1_avg < 0.50)
    {
        Console.Error.WriteLine("Global metrics below threshold");
        Environment.Exit(1);
    }
}

static string Normalize(string text)
{
    var lines = Regex.Split(text.Replace("\r", ""), "\n");
    var normLines = lines.Select(l => Regex.Replace(l.Trim().ToLowerInvariant(), @"\s+", " "));
    return string.Join("\n", normLines);
}

static double Cer(string r, string h)
{
    int m = r.Length, n = h.Length;
    var dp = new int[m + 1, n + 1];
    for (int i = 0; i <= m; i++) dp[i, 0] = i;
    for (int j = 0; j <= n; j++) dp[0, j] = j;
    for (int i = 1; i <= m; i++)
        for (int j = 1; j <= n; j++)
        {
            int cost = r[i - 1] == h[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
        }
    return m == 0 ? (n == 0 ? 0 : 1) : (double)dp[m, n] / m;
}

static (double, double, double) TokenScores(string r, string h)
{
    var rt = r.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var ht = h.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var rc = rt.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    var hc = ht.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    int inter = 0;
    foreach (var kv in hc)
        if (rc.TryGetValue(kv.Key, out var c)) inter += Math.Min(c, kv.Value);
    double prec = ht.Length == 0 ? 0 : (double)inter / ht.Length;
    double rec = rt.Length == 0 ? 0 : (double)inter / rt.Length;
    double f1 = prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
    return (prec, rec, f1);
}

static (int, int, double) LineScores(string r, string h)
{
    var rl = new HashSet<string>(r.Split('\n').Where(x => x.Length > 0));
    var hl = new HashSet<string>(h.Split('\n').Where(x => x.Length > 0));
    int inter = rl.Intersect(hl).Count();
    double prec = hl.Count == 0 ? 0 : (double)inter / hl.Count;
    double rec = rl.Count == 0 ? 0 : (double)inter / rl.Count;
    double f1 = prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
    return (rl.Count, hl.Count, f1);
}

static string CpuName()
{
    try
    {
        return File.ReadLines("/proc/cpuinfo").First(l => l.StartsWith("model name")).Split(':')[1].Trim();
    }
    catch
    {
        return RuntimeInformation.ProcessArchitecture.ToString();
    }
}

static string Proc(string file, string args)
{
    try
    {
        var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi);
        return p!.StandardOutput.ReadToEnd();
    }
    catch { return string.Empty; }
}

class FileMetrics
{
    public string dataset { get; set; } = "";
    public string file { get; set; } = "";
    public double cer_char { get; set; }
    public double token_precision { get; set; }
    public double token_recall { get; set; }
    public double token_f1 { get; set; }
    public int line_count_ref { get; set; }
    public int line_count_hyp { get; set; }
    public double line_f1 { get; set; }
    public long timing_markitdownnet { get; set; }
    public long timing_pytesseract { get; set; }
}

class Aggregate
{
    public double cer_avg { get; set; }
    public double token_f1_avg { get; set; }
    public double line_f1_avg { get; set; }
    public int n_files { get; set; }
}

