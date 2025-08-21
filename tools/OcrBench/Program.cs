using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Tesseract;
using SkiaSharp;
using System.Security.Cryptography;

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
    for (int i = 0; i < a.Length; i++)
    {
        var key = a[i];
        if (!key.StartsWith("--")) continue;
        if (i + 1 < a.Length && !a[i + 1].StartsWith("--"))
        {
            d[key] = a[i + 1];
            i++;
        }
        else
        {
            d[key] = "1";
        }
    }
    return d;
}

static void Extract(Dictionary<string, string> o)
{
    var inputDir = o["--input-dir"];
    var outDir = o["--out-dir"];
    var langs = o["--langs"];
    var psm = o["--psm"];
    var threads = o["--threads"];
    var engineOpt = o.GetValueOrDefault("--engine", "markitdownnet");
    var refresh = o.GetValueOrDefault("--refresh", engineOpt);
    var pyExe = o.GetValueOrDefault("--python-exe", "python3");
    var txtVar = o.GetValueOrDefault("--txt-variant", "both");
    bool outRaw = txtVar == "both" || txtVar == "raw";
    bool outMd = txtVar == "both" || txtVar == "mdready";
    bool Has(string opt, string val) => opt == "all" || opt.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(val);
    var doNet = Has(engineOpt, "markitdownnet");
    var doCli = Has(engineOpt, "markitdownnet-cli");
    var doPy = Has(engineOpt, "pytesseract-cli");
    var refNet = Has(refresh, "markitdownnet");
    var refCli = Has(refresh, "markitdownnet-cli");
    var refPy = Has(refresh, "pytesseract-cli");

    Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", threads);

    var netDir = Path.Combine(outDir, "markitdownnet");
    var cliDir = Path.Combine(outDir, "markitdownnet-cli");
    var pyDir = Path.Combine(outDir, "pytesseract-cli");
    Directory.CreateDirectory(outDir);
    if (refNet && Directory.Exists(netDir)) Directory.Delete(netDir, true);
    if (refCli && Directory.Exists(cliDir)) Directory.Delete(cliDir, true);
    if (refPy && Directory.Exists(pyDir)) Directory.Delete(pyDir, true);
    if (doNet) Directory.CreateDirectory(netDir);
    if (doCli) Directory.CreateDirectory(cliDir);
    if (doPy) Directory.CreateDirectory(pyDir);
    var rasterRoot = Path.Combine("artifacts/_sanity/raster");
    Directory.CreateDirectory(rasterRoot);

    TesseractEngine? engine = null;
    if (doNet)
    {
        engine = new TesseractEngine("/usr/share/tesseract-ocr/5/tessdata", langs, EngineMode.LstmOnly);
        engine.DefaultPageSegMode = (PageSegMode)6;
        engine.SetVariable("user_defined_dpi", "300");
        engine.SetVariable("preserve_interword_spaces", "1");
    }

    var exts = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".pdf" }, StringComparer.OrdinalIgnoreCase);
    var datasets = Directory.GetDirectories(inputDir).Where(d => Path.GetFileName(d) != "_ocr");
    var timingsPath = Path.Combine(outDir, "timings.json");
    var timings = File.Exists(timingsPath)
        ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, long>>>(File.ReadAllText(timingsPath))!
        : new Dictionary<string, Dictionary<string, long>>();
    long totalNet = 0, totalCli = 0, totalPy = 0;

    foreach (var datasetPath in datasets)
    {
        var dataset = Path.GetFileName(datasetPath);
        var files = Directory.EnumerateFiles(datasetPath, "*.*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f)));
        foreach (var file in files)
        {
            var baseName = Path.GetFileNameWithoutExtension(file);
            var rel = dataset + "/" + baseName + ".txt";
            var images = GetImages(file).ToList();
            var rasterDir = Path.Combine(rasterRoot, dataset);
            Directory.CreateDirectory(rasterDir);
            var rasterPath = Path.Combine(rasterDir, Path.GetFileNameWithoutExtension(file) + ".png");
            SaveRaster(images.First(), rasterPath);
            using var rpix = Pix.LoadFromFile(rasterPath);
            var depth = rpix.Depth;

            long tNet = 0, tCli = 0, tPy = 0;
            if (doNet)
            {
                var sw = Stopwatch.StartNew();
                using var page = engine!.Process(rpix);
                var text = page.GetText();
                sw.Stop();
                tNet = sw.ElapsedMilliseconds;
                var outNetDir = Path.Combine(outDir, "markitdownnet", dataset);
                Directory.CreateDirectory(outNetDir);
                var outList = new List<string>();
                if (outRaw)
                {
                    File.WriteAllText(Path.Combine(outNetDir, baseName + ".raw.txt"), text);
                    outList.Add("raw");
                }
                if (outMd)
                {
                    File.WriteAllText(Path.Combine(outNetDir, baseName + ".mdready.txt"), MdReady(text));
                    outList.Add("mdready");
                }
                totalNet += tNet;
                var entryNet = timings.ContainsKey(rel) ? timings[rel] : new Dictionary<string, long>();
                entryNet["markitdownnet"] = tNet;
                timings[rel] = entryNet;
                Console.WriteLine($"engine=markitdownnet file={dataset}/{Path.GetFileName(file)} dpi=300 depth={depth} psm=6 oem=1 preserve_spaces=1 out=[{string.Join(',', outList)}] time_ms={tNet} raster={rasterPath}");
            }

            if (doCli)
            {
                var sw = Stopwatch.StartNew();
                var psi = new ProcessStartInfo("tesseract")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add(rasterPath);
                psi.ArgumentList.Add("stdout");
                psi.ArgumentList.Add("--psm");
                psi.ArgumentList.Add(psm);
                psi.ArgumentList.Add("-l");
                psi.ArgumentList.Add(langs);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("preserve_interword_spaces=1");
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("user_defined_dpi=300");
                using var p = Process.Start(psi);
                var output = p!.StandardOutput.ReadToEnd();
                p.WaitForExit();
                sw.Stop();
                tCli = sw.ElapsedMilliseconds;
                var outCliDir = Path.Combine(outDir, "markitdownnet-cli", dataset);
                Directory.CreateDirectory(outCliDir);
                var outList = new List<string>();
                if (outRaw)
                {
                    File.WriteAllText(Path.Combine(outCliDir, baseName + ".raw.txt"), output);
                    outList.Add("raw");
                }
                if (outMd)
                {
                    File.WriteAllText(Path.Combine(outCliDir, baseName + ".mdready.txt"), MdReady(output));
                    outList.Add("mdready");
                }
                totalCli += tCli;
                var entryCli = timings.ContainsKey(rel) ? timings[rel] : new Dictionary<string, long>();
                entryCli["markitdownnet-cli"] = tCli;
                timings[rel] = entryCli;
                Console.WriteLine($"engine=markitdownnet-cli file={dataset}/{Path.GetFileName(file)} dpi=300 depth={depth} psm=6 oem=1 preserve_spaces=1 out=[{string.Join(',', outList)}] time_ms={tCli} raster={rasterPath}");
            }

            if (doPy)
            {
                var sw = Stopwatch.StartNew();
                var script = $"from PIL import Image; import pytesseract; p=r'''{rasterPath}'''; print(pytesseract.image_to_string(Image.open(p), lang='{langs}', config='--psm {psm} -c preserve_interword_spaces=1 -c user_defined_dpi=300'), end='')";
                var tmpPy = Path.GetTempFileName();
                File.WriteAllText(tmpPy, script);
                var psi = new ProcessStartInfo(pyExe)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add(tmpPy);
                using var p = Process.Start(psi);
                var output = p!.StandardOutput.ReadToEnd();
                p.WaitForExit();
                File.Delete(tmpPy);
                sw.Stop();
                tPy = sw.ElapsedMilliseconds;
                var outPyDir = Path.Combine(outDir, "pytesseract-cli", dataset);
                Directory.CreateDirectory(outPyDir);
                var outList = new List<string>();
                if (outRaw)
                {
                    File.WriteAllText(Path.Combine(outPyDir, baseName + ".raw.txt"), output);
                    outList.Add("raw");
                }
                if (outMd)
                {
                    File.WriteAllText(Path.Combine(outPyDir, baseName + ".mdready.txt"), MdReady(output));
                    outList.Add("mdready");
                }
                totalPy += tPy;
                var entryPy = timings.ContainsKey(rel) ? timings[rel] : new Dictionary<string, long>();
                entryPy["pytesseract-cli"] = tPy;
                timings[rel] = entryPy;
                Console.WriteLine($"engine=pytesseract-cli file={dataset}/{Path.GetFileName(file)} dpi=300 depth={depth} psm=6 oem=1 preserve_spaces=1 out=[{string.Join(',', outList)}] time_ms={tPy} raster={rasterPath}");
            }
        }
    }

    File.WriteAllText(timingsPath, JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }));
    if (doNet) Console.WriteLine($"TOTAL markitdownnet {totalNet} ms");
    if (doCli) Console.WriteLine($"TOTAL markitdownnet-cli {totalCli} ms");
    if (doPy) Console.WriteLine($"TOTAL pytesseract-cli {totalPy} ms");
    engine?.Dispose();
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

static void Compare(Dictionary<string, string> o)
{
    var ocrDir = o["--ocr-dir"];
    var outJson = o["--out-json"];
    var outMd = o["--out-md"];
    var runCliSanity = o.ContainsKey("--run-cli-sanity");
    var pyExe = o.GetValueOrDefault("--python-exe", "python3");
    var baseline = o.GetValueOrDefault("--baseline", "pytesseract-cli");

    var netDir = Path.Combine(ocrDir, "markitdownnet");
    var cliDir = Path.Combine(ocrDir, "markitdownnet-cli");
    var pyDir = Path.Combine(ocrDir, "pytesseract-cli");
    var gtDir = baseline == "pytesseract-legacy" ? Path.Combine(ocrDir, "pytesseract") : Path.Combine(ocrDir, "pytesseract-cli");
    var legacyDir = Path.Combine(ocrDir, "pytesseract");
    var timings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, long>>>(File.ReadAllText(Path.Combine(ocrDir, "timings.json")))!;

    var files = new List<FileMetrics>();
    var txtVariantUsed = new Dictionary<string, string>();
    void Note(string eng, string v)
    {
        if (!txtVariantUsed.TryGetValue(eng, out var cur)) txtVariantUsed[eng] = v; else if (cur != v) txtVariantUsed[eng] = "mixed";
    }
    var legacyByDataset = new Dictionary<string, Dictionary<string, Aggregate>>();
    var legacyGlobal = new Dictionary<string, Aggregate>();
    bool doLegacy = baseline == "pytesseract-cli" && Directory.Exists(legacyDir);

    foreach (var dataset in Directory.GetDirectories(gtDir))
    {
        var ds = Path.GetFileName(dataset);
        var fileNames = Directory.GetFiles(dataset, "*.txt").Select(f => Path.GetFileName(f))
            .Select(n => n.EndsWith(".mdready.txt") ? n[..^12] : n.EndsWith(".raw.txt") ? n[..^8] : n[..^4])
            .Distinct();
        foreach (var baseName in fileNames)
        {
            string gtPathMd = Path.Combine(gtDir, ds, baseName + ".mdready.txt");
            string gtPathRaw = Path.Combine(gtDir, ds, baseName + ".raw.txt");
            string gtPathTxt = Path.Combine(gtDir, ds, baseName + ".txt");
            string gtText; string gtVar; string gtRawRel=""; string gtMdRel="";
            if (File.Exists(gtPathMd)) { gtText = File.ReadAllText(gtPathMd); gtVar = "mdready"; gtMdRel = gtPathMd; }
            else if (File.Exists(gtPathRaw)) { gtText = File.ReadAllText(gtPathRaw); gtVar = "raw"; gtRawRel = gtPathRaw; }
            else { gtText = File.ReadAllText(gtPathTxt); gtVar = "raw"; gtRawRel = gtPathTxt; }
            Note(baseline, gtVar);
            var gt = Normalize(gtText);
            var rel = ds + "/" + baseName + ".txt";
            var runs = new Dictionary<string, FileRun>();
            var paths = new Dictionary<string, string>
            {
                ["raw"] = gtRawRel == "" ? "" : Path.GetRelativePath(Directory.GetCurrentDirectory(), gtRawRel),
                ["mdready"] = gtMdRel == "" ? "" : Path.GetRelativePath(Directory.GetCurrentDirectory(), gtMdRel)
            };

            string? legacyGt = null;
            if (doLegacy)
            {
                string lMd = Path.Combine(legacyDir, ds, baseName + ".mdready.txt");
                string lRaw = Path.Combine(legacyDir, ds, baseName + ".raw.txt");
                string lTxt = Path.Combine(legacyDir, ds, baseName + ".txt");
                if (File.Exists(lMd)) legacyGt = File.ReadAllText(lMd);
                else if (File.Exists(lRaw)) legacyGt = File.ReadAllText(lRaw);
                else if (File.Exists(lTxt)) legacyGt = File.ReadAllText(lTxt);
                if (legacyGt != null) legacyGt = Normalize(legacyGt);
            }

            if (File.Exists(Path.Combine(netDir, ds, baseName + ".mdready.txt")) || File.Exists(Path.Combine(netDir, ds, baseName + ".raw.txt")) || File.Exists(Path.Combine(netDir, ds, baseName + ".txt")))
            {
                string hypPath = File.Exists(Path.Combine(netDir, ds, baseName + ".mdready.txt"))
                    ? Path.Combine(netDir, ds, baseName + ".mdready.txt")
                    : File.Exists(Path.Combine(netDir, ds, baseName + ".raw.txt"))
                        ? Path.Combine(netDir, ds, baseName + ".raw.txt")
                        : Path.Combine(netDir, ds, baseName + ".txt");
                var variant = hypPath.EndsWith(".mdready.txt") ? "mdready" : "raw";
                Note("markitdownnet", variant);
                var hyp = Normalize(File.ReadAllText(hypPath));
                var (tp, tr, tf) = TokenScores(gt, hyp);
                var (lcRef, lcHyp, lf) = LineScores(gt, hyp);
                var cer = Cer(gt, hyp);
                timings.TryGetValue(rel, out var t);
                runs["markitdownnet"] = new FileRun
                {
                    cer_char = cer,
                    token_precision = tp,
                    token_recall = tr,
                    token_f1 = tf,
                    line_count_ref = lcRef,
                    line_count_hyp = lcHyp,
                    line_f1 = lf,
                    time_ms = t?["markitdownnet"] ?? 0
                };
                if (legacyGt != null)
                {
                    var (tp2, tr2, tf2) = TokenScores(legacyGt, hyp);
                    var (lcRef2, lcHyp2, lf2) = LineScores(legacyGt, hyp);
                    var cer2 = Cer(legacyGt, hyp);
                    if (!legacyByDataset.TryGetValue(ds, out var d)) legacyByDataset[ds] = d = new();
                    if (!d.TryGetValue("markitdownnet", out var a)) d["markitdownnet"] = a = new();
                    a.cer_sum += cer2; a.token_f1_sum += tf2; a.line_f1_sum += lf2; a.n_files++;
                    if (!legacyGlobal.TryGetValue("markitdownnet", out var g)) legacyGlobal["markitdownnet"] = g = new();
                    g.cer_sum += cer2; g.token_f1_sum += tf2; g.line_f1_sum += lf2; g.n_files++;
                }
            }

            if (File.Exists(Path.Combine(cliDir, ds, baseName + ".mdready.txt")) || File.Exists(Path.Combine(cliDir, ds, baseName + ".raw.txt")) || File.Exists(Path.Combine(cliDir, ds, baseName + ".txt")))
            {
                string hypPath = File.Exists(Path.Combine(cliDir, ds, baseName + ".mdready.txt"))
                    ? Path.Combine(cliDir, ds, baseName + ".mdready.txt")
                    : File.Exists(Path.Combine(cliDir, ds, baseName + ".raw.txt"))
                        ? Path.Combine(cliDir, ds, baseName + ".raw.txt")
                        : Path.Combine(cliDir, ds, baseName + ".txt");
                var variant = hypPath.EndsWith(".mdready.txt") ? "mdready" : "raw";
                Note("markitdownnet-cli", variant);
                var hyp = Normalize(File.ReadAllText(hypPath));
                var (tp, tr, tf) = TokenScores(gt, hyp);
                var (lcRef, lcHyp, lf) = LineScores(gt, hyp);
                var cer = Cer(gt, hyp);
                timings.TryGetValue(rel, out var t);
                runs["markitdownnet-cli"] = new FileRun
                {
                    cer_char = cer,
                    token_precision = tp,
                    token_recall = tr,
                    token_f1 = tf,
                    line_count_ref = lcRef,
                    line_count_hyp = lcHyp,
                    line_f1 = lf,
                    time_ms = t?["markitdownnet-cli"] ?? 0
                };
                if (legacyGt != null)
                {
                    var (tp2, tr2, tf2) = TokenScores(legacyGt, hyp);
                    var (lcRef2, lcHyp2, lf2) = LineScores(legacyGt, hyp);
                    var cer2 = Cer(legacyGt, hyp);
                    if (!legacyByDataset.TryGetValue(ds, out var d)) legacyByDataset[ds] = d = new();
                    if (!d.TryGetValue("markitdownnet-cli", out var a)) d["markitdownnet-cli"] = a = new();
                    a.cer_sum += cer2; a.token_f1_sum += tf2; a.line_f1_sum += lf2; a.n_files++;
                    if (!legacyGlobal.TryGetValue("markitdownnet-cli", out var g)) legacyGlobal["markitdownnet-cli"] = g = new();
                    g.cer_sum += cer2; g.token_f1_sum += tf2; g.line_f1_sum += lf2; g.n_files++;
                }
            }

            if (File.Exists(Path.Combine(pyDir, ds, baseName + ".mdready.txt")) || File.Exists(Path.Combine(pyDir, ds, baseName + ".raw.txt")) || File.Exists(Path.Combine(pyDir, ds, baseName + ".txt")))
            {
                string hypPath = File.Exists(Path.Combine(pyDir, ds, baseName + ".mdready.txt"))
                    ? Path.Combine(pyDir, ds, baseName + ".mdready.txt")
                    : File.Exists(Path.Combine(pyDir, ds, baseName + ".raw.txt"))
                        ? Path.Combine(pyDir, ds, baseName + ".raw.txt")
                        : Path.Combine(pyDir, ds, baseName + ".txt");
                var variant = hypPath.EndsWith(".mdready.txt") ? "mdready" : "raw";
                Note("pytesseract-cli", variant);
                var hyp = Normalize(File.ReadAllText(hypPath));
                var (tp, tr, tf) = TokenScores(gt, hyp);
                var (lcRef, lcHyp, lf) = LineScores(gt, hyp);
                var cer = Cer(gt, hyp);
                timings.TryGetValue(rel, out var t);
                runs["pytesseract-cli"] = new FileRun
                {
                    cer_char = cer,
                    token_precision = tp,
                    token_recall = tr,
                    token_f1 = tf,
                    line_count_ref = lcRef,
                    line_count_hyp = lcHyp,
                    line_f1 = lf,
                    time_ms = t?["pytesseract-cli"] ?? 0
                };
                if (legacyGt != null)
                {
                    var (tp2, tr2, tf2) = TokenScores(legacyGt, hyp);
                    var (lcRef2, lcHyp2, lf2) = LineScores(legacyGt, hyp);
                    var cer2 = Cer(legacyGt, hyp);
                    if (!legacyByDataset.TryGetValue(ds, out var d)) legacyByDataset[ds] = d = new();
                    if (!d.TryGetValue("pytesseract-cli", out var a)) d["pytesseract-cli"] = a = new();
                    a.cer_sum += cer2; a.token_f1_sum += tf2; a.line_f1_sum += lf2; a.n_files++;
                    if (!legacyGlobal.TryGetValue("pytesseract-cli", out var g)) legacyGlobal["pytesseract-cli"] = g = new();
                    g.cer_sum += cer2; g.token_f1_sum += tf2; g.line_f1_sum += lf2; g.n_files++;
                }
            }

            files.Add(new FileMetrics { dataset = ds, file = baseName, paths = paths, runs = runs });
        }
    }

    var byDataset = new Dictionary<string, Dictionary<string, Aggregate>>();
    var global = new Dictionary<string, Aggregate>();
    foreach (var f in files)
    {
        foreach (var kv in f.runs)
        {
            var eng = kv.Key;
            var m = kv.Value;
            if (!byDataset.TryGetValue(f.dataset, out var d))
                byDataset[f.dataset] = d = new Dictionary<string, Aggregate>();
            if (!d.TryGetValue(eng, out var a))
                d[eng] = a = new Aggregate();
            a.cer_sum += m.cer_char; a.token_f1_sum += m.token_f1; a.line_f1_sum += m.line_f1; a.n_files++;

            if (!global.TryGetValue(eng, out var g))
                global[eng] = g = new Aggregate();
            g.cer_sum += m.cer_char; g.token_f1_sum += m.token_f1; g.line_f1_sum += m.line_f1; g.n_files++;
        }
    }

    var engOrder = new[] { "markitdownnet", "markitdownnet-cli", "pytesseract-cli" };
    var runConfig = new Dictionary<string, object>
    {
        ["baseline"] = baseline,
        ["os"] = RuntimeInformation.OSDescription.Trim(),
        ["cpu"] = CpuName(),
        ["dotnet"] = Environment.Version.ToString(),
        ["langs"] = o.GetValueOrDefault("--langs", "eng"),
        ["psm"] = o.GetValueOrDefault("--psm", "6"),
        ["threads"] = o.GetValueOrDefault("--threads", "1"),
        ["timings_unit"] = "ms",
        ["engines"] = engOrder.Where(global.ContainsKey).ToArray(),
        ["txt_variant_used"] = txtVariantUsed
    };

    if (global.ContainsKey("markitdownnet"))
    {
        using var e = new TesseractEngine("/usr/share/tesseract-ocr/5/tessdata", o.GetValueOrDefault("--langs", "eng"), EngineMode.LstmOnly);
        var tv = e.Version;
        string lv;
        if (!e.TryGetStringVariable("leptonica_version", out lv))
            lv = Proc("tesseract", "--version").Split('\n').ElementAtOrDefault(1)?.Trim() ?? "";
        var engPath = Path.Combine("/usr/share/tesseract-ocr/5/tessdata", "eng.traineddata");
        runConfig["markitdownnet"] = new
        {
            tesseract_version = tv,
            leptonica_version = lv,
            tessdata_path = "/usr/share/tesseract-ocr/5/tessdata",
            eng_checksum = File.Exists(engPath) ? Checksum(engPath) : ""
        };
    }
    if (global.ContainsKey("markitdownnet-cli"))
    {
        var ver = Proc("tesseract", "--version").Split('\n');
        var tessdata = "/usr/share/tesseract-ocr/5/tessdata";
        var engPath = Path.Combine(tessdata, "eng.traineddata");
        runConfig["markitdownnet-cli"] = new
        {
            tesseract_version = ver.ElementAtOrDefault(0)?.Trim() ?? "",
            leptonica_version = ver.ElementAtOrDefault(1)?.Trim() ?? "",
            tessdata_path = tessdata,
            eng_checksum = File.Exists(engPath) ? Checksum(engPath) : ""
        };
    }
    if (global.ContainsKey("pytesseract-cli"))
    {
        var script = "import pytesseract, json; print(json.dumps({'pytesseract_version': getattr(pytesseract, '__version__', ''), 'tesseract_cmd': getattr(pytesseract, 'tesseract_cmd', '')}))";
        var psi = new ProcessStartInfo(pyExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        using var p = Process.Start(psi);
        var output = p!.StandardOutput.ReadToEnd();
        p.WaitForExit();
        var info = new Dictionary<string, string>();
        try { info = JsonSerializer.Deserialize<Dictionary<string, string>>(output.Trim()) ?? new(); } catch { }
        runConfig["pytesseract-cli"] = new
        {
            pytesseract_version = info.GetValueOrDefault("pytesseract_version", ""),
            tesseract_cmd = info.GetValueOrDefault("tesseract_cmd", "")
        };
    }

    var bench = new
    {
        task = "OCR-BENCH",
        run_config = runConfig,
        files,
        aggregate = new
        {
            by_dataset = byDataset.ToDictionary(k => k.Key, v => v.Value.ToDictionary(e => e.Key, a => new { cer_avg = a.Value.cer_sum / a.Value.n_files, token_f1_avg = a.Value.token_f1_sum / a.Value.n_files, line_f1_avg = a.Value.line_f1_sum / a.Value.n_files, n_files = a.Value.n_files })),
            global = global.ToDictionary(e => e.Key, a => new { cer_avg = a.Value.cer_sum / a.Value.n_files, token_f1_avg = a.Value.token_f1_sum / a.Value.n_files, line_f1_avg = a.Value.line_f1_sum / a.Value.n_files, n_files = a.Value.n_files })
        }
    };
    Directory.CreateDirectory(Path.GetDirectoryName(outJson)!);
    File.WriteAllText(outJson, JsonSerializer.Serialize(bench, new JsonSerializerOptions { WriteIndented = true }));

    var sb = new StringBuilder();
    sb.AppendLine("# OCR Benchmark\n");
    sb.AppendLine(baseline == "pytesseract-cli" ? "**Baseline: GT-shared (pytesseract-cli)**" : "**Baseline: GT storica**");
    sb.AppendLine();
    sb.AppendLine("## Global");
    sb.AppendLine("| engine | CER | Token-F1 | line_F1 | n_files |");
    foreach (var eng in engOrder)
        if (bench.aggregate.global.TryGetValue(eng, out var g))
            sb.AppendLine($"| {eng} | {g.cer_avg:F4} | {g.token_f1_avg:F4} | {g.line_f1_avg:F4} | {g.n_files} |");
    sb.AppendLine();
    sb.AppendLine("## By dataset");
    sb.AppendLine("| dataset | engine | CER | Token-F1 | line_F1 | n_files |");
    foreach (var ds in bench.aggregate.by_dataset)
        foreach (var eng in engOrder)
            if (ds.Value.TryGetValue(eng, out var m))
                sb.AppendLine($"| {ds.Key} | {eng} | {m.cer_avg:F4} | {m.token_f1_avg:F4} | {m.line_f1_avg:F4} | {m.n_files} |");
    if (baseline == "pytesseract-cli" && legacyGlobal.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("### Legacy comparison (informativa)");
        sb.AppendLine();
        sb.AppendLine("#### Global");
        sb.AppendLine("| engine | CER | Token-F1 | line_F1 | n_files |");
        foreach (var eng in engOrder)
            if (legacyGlobal.TryGetValue(eng, out var g))
                sb.AppendLine($"| {eng} | {g.cer_sum / g.n_files:F4} | {g.token_f1_sum / g.n_files:F4} | {g.line_f1_sum / g.n_files:F4} | {g.n_files} |");
        sb.AppendLine();
        sb.AppendLine("#### By dataset");
        sb.AppendLine("| dataset | engine | CER | Token-F1 | line_F1 | n_files |");
        foreach (var ds in legacyByDataset)
            foreach (var eng in engOrder)
                if (ds.Value.TryGetValue(eng, out var m))
                    sb.AppendLine($"| {ds.Key} | {eng} | {m.cer_sum / m.n_files:F4} | {m.token_f1_sum / m.n_files:F4} | {m.line_f1_sum / m.n_files:F4} | {m.n_files} |");
    }

    sb.AppendLine();
    sb.AppendLine("## Run config");
    foreach (var kv in runConfig)
        sb.AppendLine($"- {kv.Key}: {JsonSerializer.Serialize(kv.Value)}");
    Directory.CreateDirectory(Path.GetDirectoryName(outMd)!);
    File.WriteAllText(outMd, sb.ToString());

    var fails = new List<string>();
    foreach (var kv in bench.aggregate.global)
        if (kv.Value.token_f1_avg < 0.90 || kv.Value.line_f1_avg < 0.70)
            fails.Add($"GLOBAL {kv.Key} Token-F1={kv.Value.token_f1_avg:F2} line_F1={kv.Value.line_f1_avg:F2}");
    foreach (var dsName in new[] { "ICDAR", "PUBTABLES" })
        if (bench.aggregate.by_dataset.TryGetValue(dsName, out var engs))
            foreach (var kv in engs)
                if (kv.Value.token_f1_avg < 0.85 || kv.Value.line_f1_avg < 0.55)
                    fails.Add($"{dsName} {kv.Key} Token-F1={kv.Value.token_f1_avg:F2} line_F1={kv.Value.line_f1_avg:F2}");
    if (fails.Count > 0)
    {
        Console.WriteLine("Gate check failed:");
        foreach (var f in fails) Console.WriteLine(f);
        Environment.ExitCode = 1;
    }

    if (runCliSanity)
    {
        var worst = files.Where(f => f.runs.ContainsKey("markitdownnet")).OrderByDescending(f => f.runs["markitdownnet"].cer_char).Take(2).ToList();
        Directory.CreateDirectory("artifacts/_sanity");
        foreach (var f in worst)
        {
            var img = Path.Combine("artifacts/_sanity/raster", f.dataset, f.file + ".png");
            var baseName = f.dataset + "_" + f.file;
            var out6 = Path.Combine("artifacts/_sanity", baseName + ".psm6.cli.txt");
            var out11 = Path.Combine("artifacts/_sanity", baseName + ".psm11.cli.txt");
            var e6 = RunTessCli(img, out6, "6");
            var e11 = RunTessCli(img, out11, "11");
            var b6 = new FileInfo(out6).Length;
            var b11 = new FileInfo(out11).Length;
            var exit = e6 == 0 && e11 == 0 ? 0 : 1;
            Console.WriteLine($"cli sanity: file={f.dataset}/{f.file} psm6_bytes={b6} psm11_bytes={b11} exit={exit}");
        }
    }
}

static void SaveRaster(string src, string dest)
{
    if (File.Exists(dest)) return;
    using var bmp = SKBitmap.Decode(src);
    var gray = new SKBitmap(bmp.Width, bmp.Height, SKColorType.Gray8, SKAlphaType.Opaque);
    using (var canvas = new SKCanvas(gray))
        canvas.DrawBitmap(bmp, 0, 0);
    using var img = SKImage.FromBitmap(gray);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(dest, data.ToArray());
}

static int RunTessCli(string img, string outFile, string psm)
{
    var psi = new ProcessStartInfo("tesseract")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    psi.ArgumentList.Add(img);
    psi.ArgumentList.Add("stdout");
    psi.ArgumentList.Add("--psm");
    psi.ArgumentList.Add(psm);
    psi.ArgumentList.Add("-l");
    psi.ArgumentList.Add("eng");
    psi.ArgumentList.Add("-c");
    psi.ArgumentList.Add("preserve_interword_spaces=1");
    using var p = Process.Start(psi);
    var output = p!.StandardOutput.ReadToEnd();
    p.WaitForExit();
    File.WriteAllText(outFile, output);
    return p.ExitCode;
}

static string MdReady(string text)
{
    text = text.Replace("\uFEFF", "").Normalize(NormalizationForm.FormC);
    text = text.Replace("\r\n", "\n").Replace("\r", "\n");
    var pages = text.Split('\f');
    var sb = new StringBuilder();
    for (int p = 0; p < pages.Length; p++)
    {
        if (p > 0) sb.Append('\f');
        var lines = pages[p].Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = Regex.Replace(lines[i], "[ \t]+$", "");
            line = new string(line.Where(ch => ch == '\t' || !char.IsControl(ch)).ToArray());
            sb.Append(line);
            if (i < lines.Length - 1) sb.Append('\n');
        }
    }
    var res = sb.ToString();
    res = new string(res.Where(ch => ch == '\n' || ch == '\f' || ch == '\t' || !char.IsControl(ch)).ToArray());
    return res;
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

static string Checksum(string path)
{
    using var sha = SHA256.Create();
    using var fs = File.OpenRead(path);
    var hash = sha.ComputeHash(fs);
    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
}

class FileMetrics
{
    public string dataset { get; set; } = "";
    public string file { get; set; } = "";
    public Dictionary<string, string> paths { get; set; } = new();
    public Dictionary<string, FileRun> runs { get; set; } = new();
}

class FileRun
{
    public double cer_char { get; set; }
    public double token_precision { get; set; }
    public double token_recall { get; set; }
    public double token_f1 { get; set; }
    public int line_count_ref { get; set; }
    public int line_count_hyp { get; set; }
    public double line_f1 { get; set; }
    public long time_ms { get; set; }
}

class Aggregate
{
    public double cer_sum { get; set; }
    public double token_f1_sum { get; set; }
    public double line_f1_sum { get; set; }
    public int n_files { get; set; }
}

