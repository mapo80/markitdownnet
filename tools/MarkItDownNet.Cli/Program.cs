using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
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
    string modes = GetOption(args, "--modes") ?? "pre,post-v0,post-v01,post-v02,python";
    string outJson = GetOption(args, "--out-json") ?? throw new ArgumentException("--out-json required");
    string outHtml = GetOption(args, "--out-html") ?? throw new ArgumentException("--out-html required");
    string summaryMd = GetOption(args, "--summary-md") ?? "";
    string pythonExe = GetOption(args, "--python-exe") ?? "python";
    string configPath = GetOption(args, "--config");
    TextModeConfig config = configPath != null ?
        JsonSerializer.Deserialize<TextModeConfig>(File.ReadAllText(configPath))! : new TextModeConfig();

    var modeList = modes.Split(',');
    var results = new List<BenchResult>();
    foreach (var mode in modeList)
    {
        var br = RunMode(mode.Trim(), input, pythonExe, config);
        results.Add(br);
    }

    var python = results.FirstOrDefault(r => r.Mode == "python");
    if (python != null)
    {
        foreach (var r in results.Where(r => r.Mode != "python"))
            r.Similarity = CompareOutputs(r.Output, python.Output);
    }

    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions{WriteIndented=true});
    File.WriteAllText(outJson, json);
    File.WriteAllText(outHtml, HtmlReport(results));
    if (!string.IsNullOrEmpty(summaryMd))
        File.WriteAllText(summaryMd, SummaryMarkdown(results));
}

static BenchResult RunMode(string mode, string input, string pythonExe, TextModeConfig config)
{
    string tempOut = Path.GetTempFileName();
    var times = new List<double>();
    for (int i=0;i<5;i++)
    {
        var sw = Stopwatch.StartNew();
        if (mode == "python")
        {
            var psi = new ProcessStartInfo(pythonExe, $"tools/markitdown_ocr.py {input} -o {tempOut}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
        }
        else
        {
            var text = File.ReadAllText(input);
            var md = TextModeConverter.Convert(text, mode, config);
            File.WriteAllText(tempOut, md);
        }
        sw.Stop();
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    string outputPath = $"artifacts/outputs/{Path.GetFileNameWithoutExtension(input)}.{mode}.md";
    File.Copy(tempOut, outputPath, true);
    var avg = times.Average();
    var std = Math.Sqrt(times.Select(t => Math.Pow(t-avg,2)).Average());
    return new BenchResult{Mode=mode, Trials=times, AvgMs=avg, StdMs=std, Output=outputPath};
}

static string HtmlReport(List<BenchResult> results)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<html><body><table border='1'><tr><th>Mode</th><th>avg ms</th><th>std ms</th><th>F1</th><th>CER</th></tr>");
    foreach (var r in results)
    {
        var f1 = r.Similarity?.F1.ToString("F2") ?? "-";
        var cer = r.Similarity?.Cer.ToString("F2") ?? "-";
        sb.AppendLine($"<tr><td>{r.Mode}</td><td>{r.AvgMs:F1}</td><td>{r.StdMs:F1}</td><td>{f1}</td><td>{cer}</td></tr>");
    }
    sb.AppendLine("</table>");
    var python = results.FirstOrDefault(r=>r.Mode=="python");
    if (python != null)
    {
        var pyText = WebUtility.HtmlEncode(File.ReadAllText(python.Output));
        foreach (var r in results.Where(r=>r.Mode!="python"))
        {
            var candText = WebUtility.HtmlEncode(File.ReadAllText(r.Output));
            sb.AppendLine($"<h2>Python vs {r.Mode}</h2><table border='1'><tr><th>Python</th><th>{r.Mode}</th></tr><tr><td><pre>{pyText}</pre></td><td><pre>{candText}</pre></td></tr></table>");
        }
    }
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

static string SummaryMarkdown(List<BenchResult> results)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("## Executive summary");
    foreach (var mode in new[]{"pre","post-v0","post-v01","post-v02"})
    {
        var r = results.FirstOrDefault(x=>x.Mode==mode);
        if (r?.Similarity!=null)
            sb.AppendLine($"- {mode}: token-F1 {r.Similarity.F1:F2}, CER {r.Similarity.Cer:F2}, {r.AvgMs:F1}±{r.StdMs:F1} ms");
    }
    var py = results.FirstOrDefault(x=>x.Mode=="python");
    if(py!=null)
        sb.AppendLine($"- python: {py.AvgMs:F1}±{py.StdMs:F1} ms");
    return sb.ToString();
}

static string? GetOption(string[] args, string name)
{
    for (int i=0;i<args.Length;i++)
        if (args[i]==name && i+1<args.Length)
            return args[i+1];
    return null;
}

static Similarity CompareOutputs(string candidatePath, string referencePath)
{
    var cand = Normalize(File.ReadAllText(candidatePath));
    var refText = Normalize(File.ReadAllText(referencePath));
    double cer = refText.Length==0?0:(double)Levenshtein(cand, refText) / refText.Length;
    var candTokens = cand.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var refTokens = refText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    double tp = candTokens.Intersect(refTokens).Count();
    double precision = tp / Math.Max(candTokens.Length,1);
    double recall = tp / Math.Max(refTokens.Length,1);
    double f1 = tp==0?0:2*precision*recall/(precision+recall);
    var structure = StructureCounts(cand);
    var refStructure = StructureCounts(refText);
    double headingMatch = HeadingMatchRatio(cand, refText);
    double tableCellF1 = TableCellF1(cand, refText);
    return new Similarity{Cer=cer, Precision=precision, Recall=recall, F1=f1,
        HeadingLevels=structure.HeadingLevels, ListItems=structure.ListItems, MaxListDepth=structure.MaxListDepth,
        CodeBlocks=structure.CodeBlocks, HorizontalRules=structure.HorizontalRules, Tables=structure.Tables,
        HeadingMatch=headingMatch, TableCellF1=tableCellF1};
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
    return new StructureMetrics{HeadingLevels=headLevels,ListItems=listItems,MaxListDepth=maxDepth,CodeBlocks=codeBlocks,HorizontalRules=hrs,Tables=tables};
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
}

record StructureMetrics
{
    public int[] HeadingLevels { get; set; } = new int[6];
    public int ListItems { get; set; }
    public int MaxListDepth { get; set; }
    public int CodeBlocks { get; set; }
    public int HorizontalRules { get; set; }
    public int Tables { get; set; }
}
