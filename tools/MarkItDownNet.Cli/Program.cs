using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Globalization;
using System.Linq;
using MarkItDownNet;

if (args.Length == 0)
{
    Console.WriteLine("Usage: markitdownnet <convert|bench> [options]");
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

static void BenchCommand(string[] args)
{
    string input = GetOption(args, "--input") ?? throw new ArgumentException("--input required");
    string modes = GetOption(args, "--modes") ?? "pre,post-1R,python-cold,python-hot";
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
        var br = RunMode(mode.Trim(), input, pythonExe, pythonMarkCmd, pythonHotCmd, config);
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

    var modesJson = results.Select(r => new {
        mode = r.Mode,
        timing = new {
            trials = r.Trials.Select(t => new { md_ms = t }).ToArray(),
            avg_ms = r.AvgMs,
            std_ms = r.StdMs
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
                list_items = r.Similarity.ListItems,
                max_list_depth = r.Similarity.MaxListDepth
            }
        },
        paths = new { md = r.Output, md_norm = r.NormOutput }
    }).ToArray();

    var jsonObj = new { file = input, modes = modesJson, env = env };
    var json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outJson, json);
    File.WriteAllText(outHtml, HtmlReport(results));
    if (!string.IsNullOrEmpty(summaryMd))
        File.WriteAllText(summaryMd, SummaryMarkdown(results));
}

static BenchResult RunMode(string mode, string input, string pythonExe, string pythonMarkCmd, string pythonHotCmd, TextModeConfig config)
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

    string outputPath = $"artifacts/outputs/{Path.GetFileNameWithoutExtension(input)}.{mode}.md";
    File.Copy(tempOut, outputPath, true);
    var norm = Normalize(File.ReadAllText(tempOut));
    string normPath = $"artifacts/outputs/{Path.GetFileNameWithoutExtension(input)}.{mode}.norm.md";
    File.WriteAllText(normPath, norm);
    var avg = times.Average();
    var std = Math.Sqrt(times.Select(t => Math.Pow(t - avg, 2)).Average());
    return new BenchResult { Mode = mode, Trials = times, AvgMs = avg, StdMs = std, Output = outputPath, NormOutput = normPath };
}

static string HtmlReport(List<BenchResult> results)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<html><body>");
    sb.AppendLine("<h2>Timing</h2><table border='1'><tr><th>Mode</th><th>md_ms avg</th><th>md_ms std</th></tr>");
    foreach (var r in results)
        sb.AppendLine($"<tr><td>{r.Mode}</td><td>{r.AvgMs:F1}</td><td>{r.StdMs:F1}</td></tr>");
    sb.AppendLine("</table>");
    var pre = results.FirstOrDefault(r=>r.Mode=="pre");
    var post = results.FirstOrDefault(r=>r.Mode=="post-1R");
    var pyHot = results.FirstOrDefault(r=>r.Mode=="python-hot");
    var pyCold = results.FirstOrDefault(r=>r.Mode=="python-cold");
    if (pre!=null && post!=null)
    {
        var delta = (post.AvgMs - pre.AvgMs)/pre.AvgMs*100.0;
        sb.AppendLine($"<p>post-1R vs pre: {delta:F1}%</p>");
    }
    if (pyHot!=null && pyCold!=null)
    {
        var delta = (pyHot.AvgMs - pyCold.AvgMs)/pyCold.AvgMs*100.0;
        sb.AppendLine($"<p>python-hot vs python-cold: {delta:F1}%</p>");
    }
    if (pyHot!=null && post!=null)
    {
        var delta = (post.AvgMs - pyHot.AvgMs)/pyHot.AvgMs*100.0;
        sb.AppendLine($"<p>post-1R vs python-hot: {delta:F1}%</p>");
    }
    if (pyHot!=null)
    {
        sb.AppendLine("<h2>Quality vs python-hot</h2><table border='1'><tr><th>Mode</th><th>CER</th><th>Token-F1</th><th>line_count</th><th>list_items</th></tr>");
        foreach(var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1R"))
        {
            var s=r.Similarity;
            if (s!=null)
                sb.AppendLine($"<tr><td>{r.Mode}</td><td>{s.Cer:F3}</td><td>{s.F1:F3}</td><td>{s.LineCount}</td><td>{s.ListItems}</td></tr>");
        }
        sb.AppendLine("</table>");
        var pyNorm = File.ReadAllText(pyHot.NormOutput);
        var preNorm = results.FirstOrDefault(r=>r.Mode=="pre")?.NormOutput;
        var postNorm = results.FirstOrDefault(r=>r.Mode=="post-1R")?.NormOutput;
        if (preNorm!=null)
        {
            sb.AppendLine("<h3>pre vs python-hot</h3><table border='1'><tr><td><pre>");
            sb.Append(WebUtility.HtmlEncode(File.ReadAllText(preNorm)));
            sb.AppendLine("</pre></td><td><pre>");
            sb.Append(WebUtility.HtmlEncode(pyNorm));
            sb.AppendLine("</pre></td></tr></table>");
        }
        if (postNorm!=null)
        {
            sb.AppendLine("<h3>post-1R vs python-hot</h3><table border='1'><tr><td><pre>");
            sb.Append(WebUtility.HtmlEncode(File.ReadAllText(postNorm)));
            sb.AppendLine("</pre></td><td><pre>");
            sb.Append(WebUtility.HtmlEncode(pyNorm));
            sb.AppendLine("</pre></td></tr></table>");
        }
    }
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

static string SummaryMarkdown(List<BenchResult> results)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("## Timing summary (md_ms)");
    sb.AppendLine("| mode | avg md_ms | std md_ms |");
    sb.AppendLine("| --- | --- | --- |");
    foreach (var r in results)
        sb.AppendLine($"| {r.Mode} | {r.AvgMs:F1} | {r.StdMs:F1} |");

    var pre = results.FirstOrDefault(r=>r.Mode=="pre");
    var post = results.FirstOrDefault(r=>r.Mode=="post-1R");
    var pyHot = results.FirstOrDefault(r=>r.Mode=="python-hot");
    if (pre!=null && post!=null)
    {
        var delta = (post.AvgMs - pre.AvgMs) / pre.AvgMs * 100.0;
        sb.AppendLine($"\npost-1R vs pre: {delta:F1}%");
    }
    if (pyHot!=null && post!=null)
    {
        var delta = (post.AvgMs - pyHot.AvgMs) / pyHot.AvgMs * 100.0;
        sb.AppendLine($"\npost-1R vs python-hot: {delta:F1}%");
    }

    sb.AppendLine("\n## Quality vs python-hot");
    sb.AppendLine("| mode | CER | Token-F1 | line_count | list_items |");
    sb.AppendLine("| --- | --- | --- | --- | --- |");
    if (pyHot!=null)
    {
        foreach (var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1R"))
        {
            var s = r.Similarity;
            if (s != null)
                sb.AppendLine($"| {r.Mode} | {s.Cer:F3} | {s.F1:F3} | {s.LineCount} | {s.ListItems} |");
        }
    }
    if (pyHot!=null)
    {
        sb.AppendLine("\n### Observations");
        var preS = results.FirstOrDefault(r=>r.Mode=="pre")?.Similarity;
        var postS = results.FirstOrDefault(r=>r.Mode=="post-1R")?.Similarity;
        if (preS!=null && postS!=null)
        {
            sb.AppendLine($"- CER pre {preS.Cer:F3} vs post-1R {postS.Cer:F3}");
            sb.AppendLine($"- line_count pre {preS.LineCount}, post-1R {postS.LineCount}, python {pyHot.Similarity?.LineCount}");
            var preAvg = results.First(r=>r.Mode=="pre").AvgMs;
            var postAvg = results.First(r=>r.Mode=="post-1R").AvgMs;
            sb.AppendLine($"- post-1R overhead vs pre {((postAvg-preAvg)/preAvg*100):F1}%");
        }
    }
    return sb.ToString();
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
    var structure = StructureCounts(cand);
    var refStructure = StructureCounts(refText);
    double headingMatch = HeadingMatchRatio(cand, refText);
    double tableCellF1 = TableCellF1(cand, refText);
    double lineRatio = refStructure.LineCount==0?1.0:(double)structure.LineCount/refStructure.LineCount;
    return new Similarity{Cer=cer, Precision=precision, Recall=recall, F1=f1,
        HeadingLevels=structure.HeadingLevels, ListItems=structure.ListItems, MaxListDepth=structure.MaxListDepth,
        CodeBlocks=structure.CodeBlocks, HorizontalRules=structure.HorizontalRules, Tables=structure.Tables,
        HeadingMatch=headingMatch, TableCellF1=tableCellF1, LineCount=structure.LineCount, LineRatio=lineRatio};
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

static StructureMetrics StructureCounts(string text)
{
    int[] headLevels = new int[6];
    int listItems=0,maxDepth=0,codeBlocks=0,hrs=0,tables=0; bool inCode=false;
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
        if (line.TrimStart().StartsWith("|") && (idx==0 || !lines[idx-1].TrimStart().StartsWith("|")))
            tables++;
    }
    return new StructureMetrics{HeadingLevels=headLevels,ListItems=listItems,MaxListDepth=maxDepth,CodeBlocks=codeBlocks,HorizontalRules=hrs,Tables=tables,LineCount=lineCount};
}

static List<List<List<string>>> ExtractTables(string text)
{
    var lines = text.Split('\n');
    var tables = new List<List<List<string>>>();
    int i=0;
    while(i<lines.Length)
    {
        if(lines[i].TrimStart().StartsWith("|"))
        {
            var table = new List<List<string>>();
            while(i<lines.Length && lines[i].TrimStart().StartsWith("|"))
            {
                var row = lines[i].Trim().Trim('|');
                var cells = row.Split('|').Select(c=>c.Trim()).ToList();
                table.Add(cells);
                i++;
            }
            tables.Add(table);
        }
        else i++;
    }
    return tables;
}

static double TableCellF1(string cand, string reference)
{
    var ct = ExtractTables(cand);
    var rt = ExtractTables(reference);
    int match=0,total=0;
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
        total += rTable.Count * rTable[0].Count;
    }
    return total==0?1.0:(double)match/total;
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
    public double TableCellF1 { get; set; }
    public int LineCount { get; set; }
    public double LineRatio { get; set; }
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
}
