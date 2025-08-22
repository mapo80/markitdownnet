using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace MarkItDownNet.Cli;

public static class MdCompareCommand
{
    public static void Run(string[] args)
    {
        string mdDir = GetOption(args, "--md-dir") ?? throw new ArgumentException("--md-dir required");
        string baseline = GetOption(args, "--baseline") ?? "markitdown";
        string outJson = GetOption(args, "--out-json") ?? throw new ArgumentException("--out-json required");
        string outHtml = GetOption(args, "--out-html") ?? throw new ArgumentException("--out-html required");
        string summaryMd = GetOption(args, "--summary-md") ?? throw new ArgumentException("--summary-md required");
        bool strict = bool.Parse(GetOption(args, "--strict") ?? "true");
        var runConfig = new { os = RuntimeInformation.OSDescription, dotnet = Environment.Version.ToString(), python = GetPythonVersion("python"), timings_unit = "ms", strict = strict };
        var files = new List<MdFileResult>();
        var baseDir = Path.Combine(mdDir, baseline);
        foreach (var refFile in Directory.GetFiles(baseDir, "*.md", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(baseDir, refFile);
            var hypFile = Path.Combine(mdDir, "markitdownnet", rel);
            if (!File.Exists(hypFile)) continue;
            var txtPath = Path.Combine(Path.GetDirectoryName(mdDir)!, "_ocr", "pytesseract-cli", rel.Replace(".md", ".mdready.txt"));
            var dataset = rel.Split(Path.DirectorySeparatorChar)[0];
            var sw = Stopwatch.StartNew();
            var refMd = File.ReadAllText(refFile);
            var hypMd = File.ReadAllText(hypFile);
            var refNorm = TextNormalizer.Normalize(refMd);
            var hypNorm = TextNormalizer.Normalize(hypMd);
            var textM = ComputeTextMetrics(refNorm, hypNorm);
            var lineM = ComputeLineMetrics(refNorm, hypNorm);
            var structRef = AnalyzeStructure(refMd);
            var structHyp = AnalyzeStructure(hypMd);
            var refHash = Sha256.FromFile(refFile);
            var hypHash = Sha256.FromFile(hypFile);
            sw.Stop();
            files.Add(new MdFileResult
            {
                dataset = dataset,
                paths = new FilePaths { ref_md = refFile, hyp_md = hypFile, src_txt = txtPath },
                metrics = new Metrics { text = textM, lines = lineM, structure = new StructurePair { ref_struct = structRef, hyp_struct = structHyp } },
                timing_ms = sw.ElapsedMilliseconds,
                hash = new HashResult { ref_sha256 = refHash, hyp_sha256 = hypHash, equal = refHash == hypHash }
            });
        }
        var byDataset = new Dictionary<string, DatasetAgg>();
        var global = new DatasetAgg();
        foreach (var f in files)
        {
            if (!byDataset.TryGetValue(f.dataset, out var agg)) { agg = new DatasetAgg(); byDataset[f.dataset] = agg; }
            agg.Add(f); global.Add(f);
        }
        var aggregate = new { by_dataset = byDataset.ToDictionary(k => k.Key, v => v.Value.ToOutput()), global = global.ToOutput() };
        Directory.CreateDirectory(Path.GetDirectoryName(outJson)!);
        File.WriteAllText(outJson, JsonSerializer.Serialize(new { task = "MD-PARITY", run_config = runConfig, files = files, aggregate = aggregate }, new JsonSerializerOptions { WriteIndented = true }));
        Directory.CreateDirectory(Path.GetDirectoryName(outHtml)!);
        File.WriteAllText(outHtml, HtmlRenderer.Build(runConfig, byDataset, global, files));
        Directory.CreateDirectory(Path.GetDirectoryName(summaryMd)!);
        bool hashesIdentical = files.All(f => f.hash.equal);
        bool attention = false;
        var violations = new List<string>();
        bool fail = false;
        if (strict)
        {
            if (Math.Abs(global.TokenF1 - 1.0) > 1e-6 || Math.Abs(global.LineF1 - 1.0) > 1e-6 || Math.Abs(global.Cer - 0.0) > 1e-6)
                { fail = true; violations.Add("global metrics"); }
            foreach (var kv in byDataset)
            {
                var ds = kv.Key; var a = kv.Value;
                var refM = a.RefStruct.ToMetrics(a.n_files);
                var hypM = a.HypStruct.ToMetrics(a.n_files);
                if (refM.h1 != hypM.h1) { fail = true; violations.Add($"{ds} h1"); }
                if (refM.h2 != hypM.h2) { fail = true; violations.Add($"{ds} h2"); }
                if (refM.h3 != hypM.h3) { fail = true; violations.Add($"{ds} h3"); }
                if (refM.list_items != hypM.list_items) { fail = true; violations.Add($"{ds} list_items"); }
                if (refM.tables_count != hypM.tables_count) { fail = true; violations.Add($"{ds} tables_count"); }
                if (refM.pipes_lines_count != hypM.pipes_lines_count) { fail = true; violations.Add($"{ds} pipes_lines_count"); }
                if (refM.median_pipes_per_line != hypM.median_pipes_per_line) { fail = true; violations.Add($"{ds} median_pipes_per_line"); }
                if (refM.max_pipes_per_line != hypM.max_pipes_per_line) { fail = true; violations.Add($"{ds} max_pipes_per_line"); }
            }
            if (!hashesIdentical) { fail = true; violations.Add("hash mismatch"); }
        }
        else
        {
            attention = global.TokenF1 < 0.97 || global.LineF1 < 0.94;
            fail = global.TokenF1 < 0.95 || global.LineF1 < 0.92 || DatasetViolations(byDataset, violations);
        }
        File.WriteAllText(summaryMd, BuildSummary(runConfig, byDataset, global, files, attention, fail, violations, hashesIdentical));
        if (fail)
        {
            foreach (var v in violations) Console.WriteLine(v);
            Environment.ExitCode = 1;
        }
    }

    static TextMetrics ComputeTextMetrics(string refText, string hypText)
    {
        int refChars = refText.Length;
        int charDist = Levenshtein(refText, hypText);
        var refTokens = Tokenize(refText);
        var hypTokens = Tokenize(hypText);
        int matches = CountMatches(refTokens, hypTokens);
        double precision = hypTokens.Length == 0 ? 1.0 : (double)matches / hypTokens.Length;
        double recall = refTokens.Length == 0 ? 1.0 : (double)matches / refTokens.Length;
        double f1 = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall);
        double cer = refChars == 0 ? 0 : (double)charDist / refChars;
        return new TextMetrics { cer_char = cer, token_precision = precision, token_recall = recall, token_f1 = f1, ref_tokens = refTokens.Length, hyp_tokens = hypTokens.Length, token_matches = matches, ref_chars = refChars, char_distance = charDist };
    }

    static LineMetrics ComputeLineMetrics(string refText, string hypText)
    {
        var refLines = refText.Split('\n');
        var hypLines = hypText.Split('\n');
        int matches = CountLineMatches(refLines, hypLines);
        double precision = hypLines.Length == 0 ? 1.0 : (double)matches / hypLines.Length;
        double recall = refLines.Length == 0 ? 1.0 : (double)matches / refLines.Length;
        double f1 = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall);
        return new LineMetrics { line_count_ref = refLines.Length, line_count_hyp = hypLines.Length, line_f1 = f1, line_matches = matches };
    }

    static MdStructureMetrics AnalyzeStructure(string md)
    {
        var lines = md.Replace("\r\n", "\n").Split('\n');
        int h1 = 0, h2 = 0, h3 = 0, listItems = 0, maxDepth = 0, tables = 0, pipeLines = 0, maxPipes = 0;
        var pipesPerLine = new List<int>();
        bool inTable = false;
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.StartsWith("### ")) h3++; else if (t.StartsWith("## ")) h2++; else if (t.StartsWith("# ")) h1++;
            if (Regex.IsMatch(t, @"^(\*|-|\+|\d+\.)\s+"))
            {
                listItems++;
                int depth = 0; int i = 0; while (i < line.Length && line[i] == ' ') { depth++; i++; }
                depth = depth / 2 + 1; if (depth > maxDepth) maxDepth = depth;
            }
            int pipes = t.Count(c => c == '|');
            if (pipes > 0)
            {
                pipeLines++; pipesPerLine.Add(pipes); if (pipes > maxPipes) maxPipes = pipes; if (!inTable) { tables++; inTable = true; }
            }
            else
            {
                inTable = false;
            }
        }
        double median = 0;
        if (pipesPerLine.Count > 0)
        {
            var sorted = pipesPerLine.OrderBy(x => x).ToArray();
            median = sorted[sorted.Length / 2];
        }
        return new MdStructureMetrics { h1 = h1, h2 = h2, h3 = h3, list_items = listItems, max_list_depth = maxDepth, tables_count = tables, pipes_lines_count = pipeLines, median_pipes_per_line = median, max_pipes_per_line = maxPipes };
    }

    static string[] Tokenize(string text) => Regex.Split(text.Trim(), "\\s+").Where(t => t.Length > 0).ToArray();

    static int CountMatches(string[] refTokens, string[] hypTokens)
    {
        var dict = new Dictionary<string, int>();
        foreach (var t in refTokens)
            dict[t] = dict.TryGetValue(t, out var c) ? c + 1 : 1;
        int m = 0;
        foreach (var t in hypTokens)
        {
            if (dict.TryGetValue(t, out var c) && c > 0) { m++; dict[t] = c - 1; }
        }
        return m;
    }

    static int CountLineMatches(string[] refLines, string[] hypLines)
    {
        var dict = new Dictionary<string, int>();
        foreach (var t in refLines)
            dict[t] = dict.TryGetValue(t, out var c) ? c + 1 : 1;
        int m = 0;
        foreach (var t in hypLines)
        {
            if (dict.TryGetValue(t, out var c) && c > 0) { m++; dict[t] = c - 1; }
        }
        return m;
    }

    static int Levenshtein(string s, string t)
    {
        var dp = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) dp[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        return dp[s.Length, t.Length];
    }

    static bool DatasetViolations(Dictionary<string, DatasetAgg> byDataset, List<string> violations)
    {
        bool fail = false;
        foreach (var kv in byDataset)
        {
            var ds = kv.Key; var a = kv.Value;
            if (a.RefStruct.tables_count > 0)
            {
                double delta = Math.Abs(a.HypStruct.tables_count - a.RefStruct.tables_count) / (double)a.RefStruct.tables_count;
                if (delta > 0.2) { violations.Add($"{ds} tables_count Δ={delta:F2}"); fail = true; }
            }
            if (a.RefStruct.list_items > 0)
            {
                double delta = Math.Abs(a.HypStruct.list_items - a.RefStruct.list_items) / (double)a.RefStruct.list_items;
                if (delta > 0.2) { violations.Add($"{ds} list_items Δ={delta:F2}"); fail = true; }
            }
        }
        return fail;
    }

    static string BuildSummary(object runConfig, Dictionary<string, DatasetAgg> byDataset, DatasetAgg global, List<MdFileResult> files, bool attention, bool fail, List<string> violations, bool hashesIdentical)
    {
        string badge = fail ? "**FAIL**" : attention ? "**ATTN**" : "**PASS**";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Hashes identical: {(hashesIdentical ? "YES" : "NO")}");
        sb.AppendLine(badge);
        sb.AppendLine("## Text");
        sb.AppendLine("|scope|CER|Token-F1|Line-F1|n_files|");
        sb.AppendLine("|---|---|---|---|---|");
        sb.AppendLine($"|global|{global.Cer:F3}|{global.TokenF1:F3}|{global.LineF1:F3}|{global.n_files}|");
        foreach (var kv in byDataset)
            sb.AppendLine($"|{kv.Key}|{kv.Value.Cer:F3}|{kv.Value.TokenF1:F3}|{kv.Value.LineF1:F3}|{kv.Value.n_files}|");
        sb.AppendLine("## Structure");
        sb.AppendLine("|scope|H1|H2|H3|list_items|max_list_depth|tables_count|pipes_lines_avg|");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        sb.AppendLine($"|global|{global.H1}|{global.H2}|{global.H3}|{global.ListItems}|{global.MaxListDepth}|{global.Tables}|{global.PipeLinesAvg:F1}|");
        foreach (var kv in byDataset)
        {
            var a = kv.Value;
            sb.AppendLine($"|{kv.Key}|{a.H1}|{a.H2}|{a.H3}|{a.ListItems}|{a.MaxListDepth}|{a.Tables}|{a.PipeLinesAvg:F1}|");
        }
        sb.AppendLine("## Top-5 worst files");
        sb.AppendLine("|file|token_f1|");
        sb.AppendLine("|---|---|");
        foreach (var f in files.OrderBy(x => x.metrics.text.token_f1).Take(5))
            sb.AppendLine($"|{f.dataset}/{Path.GetFileName(f.paths.ref_md)}|{f.metrics.text.token_f1:F3}|");
        sb.AppendLine("## Run config");
        sb.AppendLine(JsonSerializer.Serialize(runConfig));
        if (attention) sb.AppendLine("[ATTN] thresholds close");
        foreach (var v in violations) sb.AppendLine(v);
        return sb.ToString();
    }

    static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }

    static string GetPythonVersion(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, "-c \"import sys;print(sys.version.split()[0])\"") { RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0 ? p.StandardOutput.ReadToEnd().Trim() : "";
        }
        catch { return ""; }
    }

}
    public record MdFileResult
    {
        public string dataset { get; set; } = "";
        public FilePaths paths { get; set; } = new();
        public Metrics metrics { get; set; } = new();
        public double timing_ms { get; set; }
        public HashResult hash { get; set; } = new();
    }

    public record HashResult
    {
        public string ref_sha256 { get; set; } = "";
        public string hyp_sha256 { get; set; } = "";
        public bool equal { get; set; }
    }

    public record FilePaths { public string ref_md { get; set; } = ""; public string hyp_md { get; set; } = ""; public string src_txt { get; set; } = ""; }

    public record Metrics { public TextMetrics text { get; set; } = new(); public LineMetrics lines { get; set; } = new(); public StructurePair structure { get; set; } = new(); }

    public record TextMetrics
    {
        public double cer_char { get; set; }
        public double token_precision { get; set; }
        public double token_recall { get; set; }
        public double token_f1 { get; set; }
        public int ref_tokens { get; set; }
        public int hyp_tokens { get; set; }
        public int token_matches { get; set; }
        public int ref_chars { get; set; }
        public int char_distance { get; set; }
    }

    public record LineMetrics
    {
        public int line_count_ref { get; set; }
        public int line_count_hyp { get; set; }
        public double line_f1 { get; set; }
        public int line_matches { get; set; }
    }

    public record StructurePair { public MdStructureMetrics ref_struct { get; set; } = new(); public MdStructureMetrics hyp_struct { get; set; } = new(); }

    public record MdStructureMetrics
    {
        public int h1 { get; set; }
        public int h2 { get; set; }
        public int h3 { get; set; }
        public int list_items { get; set; }
        public int max_list_depth { get; set; }
        public int tables_count { get; set; }
        public int pipes_lines_count { get; set; }
        public double median_pipes_per_line { get; set; }
        public int max_pipes_per_line { get; set; }
    }

    public class DatasetAgg
    {
        public int n_files;
        public int refChars, charDist;
        public int tokenMatches, refTokens, hypTokens;
        public int lineMatches, refLines, hypLines;
        public StructureTotals RefStruct = new();
        public StructureTotals HypStruct = new();
        public void Add(MdFileResult f)
        {
            n_files++;
            refChars += f.metrics.text.ref_chars;
            charDist += f.metrics.text.char_distance;
            tokenMatches += f.metrics.text.token_matches;
            refTokens += f.metrics.text.ref_tokens;
            hypTokens += f.metrics.text.hyp_tokens;
            lineMatches += f.metrics.lines.line_matches;
            refLines += f.metrics.lines.line_count_ref;
            hypLines += f.metrics.lines.line_count_hyp;
            RefStruct.Add(f.metrics.structure.ref_struct);
            HypStruct.Add(f.metrics.structure.hyp_struct);
        }
        public double Cer => refChars == 0 ? 0 : (double)charDist / refChars;
        public double TokenPrecision => hypTokens == 0 ? 1.0 : (double)tokenMatches / hypTokens;
        public double TokenRecall => refTokens == 0 ? 1.0 : (double)tokenMatches / refTokens;
        public double TokenF1 => (TokenPrecision + TokenRecall) == 0 ? 0 : 2 * TokenPrecision * TokenRecall / (TokenPrecision + TokenRecall);
        public double LineF1 { get { double p = hypLines == 0 ? 1.0 : (double)lineMatches / hypLines; double r = refLines == 0 ? 1.0 : (double)lineMatches / refLines; return (p + r) == 0 ? 0 : 2 * p * r / (p + r); } }
        public int H1 => HypStruct.h1;
        public int H2 => HypStruct.h2;
        public int H3 => HypStruct.h3;
        public int ListItems => HypStruct.list_items;
        public int MaxListDepth => HypStruct.max_list_depth;
        public int Tables => HypStruct.tables_count;
        public double PipeLinesAvg => n_files == 0 ? 0 : (double)HypStruct.pipes_lines_count / n_files;
        public AggregateMetrics ToOutput() => new AggregateMetrics
        {
            n_files = n_files,
            text = new TextAgg { cer_char = Cer, token_precision = TokenPrecision, token_recall = TokenRecall, token_f1 = TokenF1 },
            lines = new LineAgg { line_count_ref = refLines, line_count_hyp = hypLines, line_f1 = LineF1 },
            structure = new StructureAgg { ref_struct = RefStruct.ToMetrics(n_files), hyp_struct = HypStruct.ToMetrics(n_files) }
        };
    }

    public class StructureTotals
    {
        public int h1, h2, h3, list_items, max_list_depth, tables_count, pipes_lines_count, max_pipes_per_line;
        public double median_pipes_per_line_sum;
        public void Add(MdStructureMetrics m)
        {
            h1 += m.h1; h2 += m.h2; h3 += m.h3;
            list_items += m.list_items;
            tables_count += m.tables_count;
            pipes_lines_count += m.pipes_lines_count;
            median_pipes_per_line_sum += m.median_pipes_per_line;
            if (m.max_list_depth > max_list_depth) max_list_depth = m.max_list_depth;
            if (m.max_pipes_per_line > max_pipes_per_line) max_pipes_per_line = m.max_pipes_per_line;
        }
        public MdStructureMetrics ToMetrics(int n)
        {
            return new MdStructureMetrics
            {
                h1 = h1,
                h2 = h2,
                h3 = h3,
                list_items = list_items,
                max_list_depth = max_list_depth,
                tables_count = tables_count,
                pipes_lines_count = pipes_lines_count,
                median_pipes_per_line = n == 0 ? 0 : median_pipes_per_line_sum / n,
                max_pipes_per_line = max_pipes_per_line
            };
        }
    }

    public record AggregateMetrics
    {
        public int n_files { get; set; }
        public TextAgg text { get; set; } = new();
        public LineAgg lines { get; set; } = new();
        public StructureAgg structure { get; set; } = new();
    }

    public record TextAgg { public double cer_char { get; set; } public double token_precision { get; set; } public double token_recall { get; set; } public double token_f1 { get; set; } }

    public record LineAgg { public int line_count_ref { get; set; } public int line_count_hyp { get; set; } public double line_f1 { get; set; } }

    public record StructureAgg { public MdStructureMetrics ref_struct { get; set; } = new(); public MdStructureMetrics hyp_struct { get; set; } = new(); }
