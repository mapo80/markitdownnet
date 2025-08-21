using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    string modes = GetOption(args, "--modes") ?? "pre,post";
    string outJson = GetOption(args, "--out-json") ?? throw new ArgumentException("--out-json required");
    string outHtml = GetOption(args, "--out-html") ?? throw new ArgumentException("--out-html required");
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
}

static BenchResult RunMode(string mode, string input, string pythonExe, TextModeConfig config)
{
    string tempOut = Path.GetTempFileName();
    var times = new List<long>();
    for (int i=0;i<5;i++)
    {
        var sw = Stopwatch.StartNew();
        if (mode == "python")
        {
            var psi = new ProcessStartInfo(pythonExe, $"-m markitdown {input} -o {tempOut}")
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
        times.Add(sw.ElapsedMilliseconds);
    }
    string outputPath = $"artifacts/outputs/{Path.GetFileNameWithoutExtension(input)}.{mode}.md";
    File.Copy(tempOut, outputPath, true);
    var avg = times.Average();
    var std = Math.Sqrt(times.Select(t => Math.Pow(t-avg,2)).Average());
    return new BenchResult{Mode=mode, AvgMs=avg, StdMs=std, Output=outputPath};
}

static string HtmlReport(List<BenchResult> results)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<html><body><table border='1'><tr><th>Mode</th><th>avg ms</th><th>std ms</th></tr>");
    foreach (var r in results)
        sb.AppendLine($"<tr><td>{r.Mode}</td><td>{r.AvgMs:F1}</td><td>{r.StdMs:F1}</td></tr>");
    sb.AppendLine("</table></body></html>");
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
    double cer = (double)Levenshtein(cand, refText) / refText.Length;
    var candTokens = cand.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    var refTokens = refText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    double tp = candTokens.Intersect(refTokens).Count();
    double precision = tp / Math.Max(candTokens.Length,1);
    double recall = tp / Math.Max(refTokens.Length,1);
    double f1 = tp==0?0:2*precision*recall/(precision+recall);
    var structure = StructureCounts(cand);
    var refStructure = StructureCounts(refText);
    double headingMatch = HeadingMatchRatio(cand, refText);
    return new Similarity{Cer=cer, Precision=precision, Recall=recall, F1=f1,
        Headings=structure.Headings, ListItems=structure.ListItems, CodeBlocks=structure.CodeBlocks,
        HorizontalRules=structure.HorizontalRules, HeadingMatch=headingMatch};
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

static (int Headings,int ListItems,int CodeBlocks,int HorizontalRules) StructureCounts(string text)
{
    int headings=0,listItems=0,codeBlocks=0,hrs=0; bool inCode=false;
    foreach(var line in text.Split('\n'))
    {
        if (line.StartsWith("```")) { inCode = !inCode; if(inCode) codeBlocks++; continue; }
        if (inCode) continue;
        if (Regex.IsMatch(line, @"^#+ ")) headings++;
        if (Regex.IsMatch(line, @"^ *(-|\d+\.) ")) listItems++;
        if (Regex.IsMatch(line.Trim(), @"^---$")) hrs++;
    }
    return (headings,listItems,codeBlocks,hrs);
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
    public int Headings { get; set; }
    public int ListItems { get; set; }
    public int CodeBlocks { get; set; }
    public int HorizontalRules { get; set; }
    public double HeadingMatch { get; set; }
}
