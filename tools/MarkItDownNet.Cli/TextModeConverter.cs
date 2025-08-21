using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MarkItDownNet;

public class TextModeConfig
{
    public int ListIndentWidth { get; set; } = 2;
    public string[] BulletChars { get; set; } = new[] {"-", "*", "•", "+", "–", "—", "·"};
    public int MinItemsForListBlock { get; set; } = 2;
    public int HeadingMinLen { get; set; } = 3;
    public int HeadingMaxLen { get; set; } = 80;
    public string HeadingPunctBlacklist { get; set; } = ".:;!?";
    public double HeadingLetterRatioMin { get; set; } = 0.6;
    public int CodeMinLines { get; set; } = 3;
    public int CodeMinIndent { get; set; } = 4;
    public double CodeSymbolDensityMin { get; set; } = 0.25;
    public bool Dehyphenation { get; set; } = true;
    // v01 options
    public bool KeyValueToTableEnabled { get; set; } = true;
    public int KeyMaxLen { get; set; } = 40;
    public int MinKeyValueRows { get; set; } = 3;
    public int MonoTableMinCols { get; set; } = 2;
    public int MonoTableMinRows { get; set; } = 3;
    public int MonoTableMinSpaceGap { get; set; } = 2;
    public int MonoTableColTolerance { get; set; } = 1;
}

public static class TextModeConverter
{
    public static string Convert(string text, string mode, TextModeConfig config)
    {
        text = Normalize(text);
        if (mode == "post-v0" || mode == "post-v01" || mode=="post-v02")
        {
            bool v02 = mode=="post-v02";
            text = Reflow(text, config, v02);
            text = DetectLists(text, config, v02);
            text = DetectHeadings(text, config, mode == "post-v01" || mode=="post-v02", v02);
            text = DetectCodeBlocks(text, config);
            text = DetectHorizontalRules(text, config, v02);
            if (mode == "post-v01" || mode=="post-v02")
            {
                if (config.KeyValueToTableEnabled)
                    text = DetectKeyValueTables(text, config, v02);
                text = DetectMonoTables(text, config, v02);
            }
        }
        return text.TrimEnd() + "\n"; // ensure newline at end
    }

    private static string Normalize(string text)
    {
        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var leading = Regex.Match(line, "^ *").Value;
            var rest = line.Substring(leading.Length);
            rest = Regex.Replace(rest, " {2,}", " ");
            lines[i] = leading + rest;
        }
        return string.Join("\n", lines);
    }

    private static string Reflow(string text, TextModeConfig config, bool v02)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; )
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
                i++;
                continue;
            }
            if (IsListLine(line, config) || IsCodeLine(line, config.CodeMinIndent))
            {
                sb.AppendLine(line);
                i++;
                continue;
            }
            var paragraph = new StringBuilder(line.TrimEnd());
            while (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i+1]) && !IsListLine(lines[i+1], config) && !IsCodeLine(lines[i+1], config.CodeMinIndent) && !(v02 && IsKeyValueLine(lines[i+1])) && !(v02 && paragraph.ToString().EndsWith(":")))
            {
                var next = lines[i+1].TrimStart();
                if (config.Dehyphenation && paragraph.ToString().EndsWith("-") && Regex.IsMatch(next, v02?"^[a-z0-9].*":"^[A-Za-z].*"))
                {
                    paragraph.Length--; // remove hyphen
                    paragraph.Append(next);
                }
                else
                {
                    paragraph.Append(' ');
                    paragraph.Append(next);
                }
                i++;
            }
            sb.AppendLine(paragraph.ToString());
            i++;
        }
        return sb.ToString();
    }

    private static bool IsListLine(string line, TextModeConfig config)
    {
        var trimmed = line.TrimStart();
        foreach (var b in config.BulletChars)
            if (trimmed.StartsWith(b + " ")) return true;
        if (Regex.IsMatch(trimmed, @"^[0-9]+[\.)]\s")) return true;
        return false;
    }

    private static bool IsCodeLine(string line, int minIndent)
        => line.StartsWith(new string(' ', minIndent));

    private static string DetectLists(string text, TextModeConfig config, bool v02)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        int i = 0;
        while (i < lines.Length)
        {
            if (TryParseBullet(lines[i], config, out int indent, out int? num, out string content))
            {
                var items = new List<(int indent,int? num,string content)>();
                while (i < lines.Length && TryParseBullet(lines[i], config, out indent, out num, out content))
                {
                    var builder = new StringBuilder(content);
                    int j = i + 1;
                    while (j < lines.Length && !TryParseBullet(lines[j], config, out _, out _, out _) )
                    {
                        if (lines[j].StartsWith(new string(' ', indent + config.ListIndentWidth + 2)) ||
                            (v02 && lines[j].StartsWith(new string(' ', indent)) && builder.Length>0 && (builder[^1]==',' || builder[^1]==';')) )
                        {
                            builder.Append(' ').Append(lines[j].Trim());
                            j++;
                        }
                        else break;
                    }
                    items.Add((indent,num,builder.ToString()));
                    i = j;
                }
                if (items.Count >= config.MinItemsForListBlock)
                {
                    bool numeric = items.All(it => it.num.HasValue);
                    if (numeric)
                    {
                        bool coherent = true;
                        for (int k=0;k<items.Count;k++) if (items[k].num != k+1) { coherent=false; break; }
                        if (!coherent)
                            for (int k=0;k<items.Count;k++) items[k]= (items[k].indent,1,items[k].content);
                    }
                    foreach (var it in items)
                    {
                        string bullet = it.num.HasValue? $"{it.num}. ":"- ";
                        sb.AppendLine(new string(' ', it.indent) + bullet + it.content);
                    }
                }
                else
                {
                    foreach (var it in items)
                        sb.AppendLine(new string(' ', it.indent) + it.content);
                }
            }
            else
            {
                sb.AppendLine(lines[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static bool TryParseBullet(string line, TextModeConfig config, out int indent, out int? number, out string content)
    {
        indent = 0; number = null; content = string.Empty;
        var trimmed = line.TrimStart();
        indent = line.Length - trimmed.Length;
        if (Regex.IsMatch(trimmed, @"^([0-9]+)[\.)]\s"))
        {
            var m = Regex.Match(trimmed, @"^([0-9]+)[\.)]\s(.*)");
            number = int.Parse(m.Groups[1].Value);
            content = m.Groups[2].Value;
            return true;
        }
        foreach (var b in config.BulletChars)
        {
            if (trimmed.StartsWith(b + " "))
            {
                content = trimmed.Substring(b.Length + 1);
                return true;
            }
        }
        return false;
    }

    private static string DetectHeadings(string text, TextModeConfig config, bool allowWhitelist, bool v02)
    {
        var lines = text.Split('\n');
        string[] whitelist = new[]{"Dettaglio","Riepilogo","Totali","Contributi","Trattenute"};
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            bool prevBlank = i == 0 || string.IsNullOrWhiteSpace(lines[i-1]);
            bool nextBlank = i == lines.Length-1 || string.IsNullOrWhiteSpace(lines[i+1]);

            if (allowWhitelist && whitelist.Any(w => string.Equals(line, w, StringComparison.OrdinalIgnoreCase)) && prevBlank)
            {
                lines[i] = "## " + line;
                continue;
            }

            if (line.Length < config.HeadingMinLen || line.Length > config.HeadingMaxLen) continue;
            if (config.HeadingPunctBlacklist.Contains(line[^1]))
            {
                if (!(allowWhitelist && line.EndsWith(":") && nextBlank))
                    continue;
            }
            if (v02 && line.Contains(':') && !nextBlank) continue;
            double letters = line.Count(char.IsLetter);
            double ratio = letters / line.Replace(" ", "").Length;
            if (ratio < config.HeadingLetterRatioMin) continue;
            if (prevBlank && nextBlank)
            {
                int level = i==0 ? 1 : 2;
                lines[i] = new string('#', level) + " " + line;
            }
        }
        return string.Join('\n', lines);
    }

    private static string DetectCodeBlocks(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        int i = 0;
        while (i < lines.Length)
        {
            if (IsCodeLine(lines[i], config.CodeMinIndent))
            {
                int start = i;
                var block = new List<string>();
                while (i < lines.Length && IsCodeLine(lines[i], config.CodeMinIndent))
                {
                    block.Add(lines[i].Substring(config.CodeMinIndent));
                    i++;
                }
                if (block.Count >= config.CodeMinLines)
                {
                    sb.AppendLine("```");
                    foreach (var l in block) sb.AppendLine(l);
                    sb.AppendLine("```");
                }
                else
                {
                    foreach (var l in block) sb.AppendLine(new string(' ', config.CodeMinIndent) + l);
                }
            }
            else
            {
                sb.AppendLine(lines[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string DetectHorizontalRules(string text, TextModeConfig config, bool v02)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var t = original.Replace(" ", "");
            if (t.Length >=3 && t.Distinct().Count()==1 && "-_*".Contains(t[0]))
            {
                if (v02 && TryParseBullet(original, config, out _, out _, out _)) continue;
                if (original.TrimStart().TrimEnd(t[0]).Length==0)
                    lines[i] = "---";
            }
        }
        return string.Join('\n', lines);
    }

    private static string DetectKeyValueTables(string text, TextModeConfig config, bool v02)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        var kv = new Regex($"^([A-Za-zÀ-ÖØ-öø-ÿ0-9 ._/()-]{{2,{config.KeyMaxLen}}})\\s*:\\s+(\\S.*)$");
        int i=0;
        while(i<lines.Length)
        {
            var m = kv.Match(lines[i]);
            if (m.Success)
            {
                var rows = new List<(string key,string val)>();
                while(i<lines.Length && (m=kv.Match(lines[i])).Success)
                {
                    string key=m.Groups[1].Value.Trim();
                    string val=m.Groups[2].Value.Trim();
                    int j=i+1;
                    while(j<lines.Length && (Regex.IsMatch(lines[j],"^ {2,}\\S") || (v02 && Regex.IsMatch(lines[j].TrimStart(),"^(di |per |dal )",RegexOptions.IgnoreCase))))
                    {
                        val += " " + lines[j].Trim();
                        j++;
                    }
                    rows.Add((key,val));
                    i=j;
                }
                if (rows.Count>=config.MinKeyValueRows && (!v02 || rows.Count(r=>r.key.EndsWith("."))<=rows.Count*0.3))
                {
                    sb.AppendLine("| Key | Value |");
                    sb.AppendLine("| --- | ----- |");
                    foreach(var r in rows)
                        sb.AppendLine($"| {r.key} | {r.val} |");
                }
                else
                {
                    foreach(var r in rows)
                        sb.AppendLine($"{r.key}: {r.val}");
                }
                continue;
            }
            sb.AppendLine(lines[i]);
            i++;
        }
        return sb.ToString();
    }

    private static string DetectMonoTables(string text, TextModeConfig config, bool v02)
    {
        var lines=text.Split('\n');
        var sb=new StringBuilder();
        var rowRegex=new Regex(@"\S.*\s{2,}\S");
        int i=0;
        while(i<lines.Length)
        {
            if(rowRegex.IsMatch(lines[i]))
            {
                var rows=new List<List<string>>();
                while(i<lines.Length && rowRegex.IsMatch(lines[i]))
                {
                    var cols=Regex.Split(lines[i].TrimEnd(),@"\s{"+config.MonoTableMinSpaceGap+@",}").ToList();
                    rows.Add(cols);
                    i++;
                }
                int colCount=rows[0].Count;
                bool uniform=rows.All(r=>Math.Abs(r.Count-colCount)<=config.MonoTableColTolerance);
                if(v02)
                {
                    for(int c=colCount-1;c>=0;c--)
                    {
                        if(rows.All(r=>c>=r.Count || string.IsNullOrWhiteSpace(r[c])))
                            foreach(var r in rows) if(c<r.Count) r.RemoveAt(c);
                    }
                    colCount=rows[0].Count;
                }
                if(rows.Count>=config.MonoTableMinRows && colCount>=config.MonoTableMinCols && uniform)
                {
                    bool header=rows.Count>1 && NumericDensity(rows[0].ToArray())<NumericDensity(rows[1].ToArray());
                    var headerRow=header?rows[0]:Enumerable.Range(1,colCount).Select(c=>"Col"+c).ToList();
                    var dataRows=header?rows.Skip(1):rows.AsEnumerable();
                    sb.AppendLine("| "+string.Join(" | ",headerRow)+" |");
                    var aligns=new List<string>();
                    for(int c=0;c<colCount;c++)
                    {
                        bool numeric=dataRows.All(r=>c<r.Count && Regex.IsMatch(r[c].Trim(),@"^-?[0-9.,]+$"));
                        aligns.Add(numeric?"---:":"---");
                    }
                    sb.AppendLine("|"+string.Join("|",aligns)+"|");
                    foreach(var r in dataRows)
                        sb.AppendLine("| "+string.Join(" | ",r)+" |");
                }
                else
                {
                    foreach(var r in rows)
                        sb.AppendLine(string.Join(" ",r));
                }
                continue;
            }
            sb.AppendLine(lines[i]);
            i++;
        }
        return sb.ToString();
    }

    private static double NumericDensity(string[] cols)
    {
        double digits=0,chars=0;
        foreach(var c in cols){digits+=c.Count(char.IsDigit); chars+=c.Replace(" ","").Length;}
        return chars==0?0:digits/chars;
    }

    private static bool IsKeyValueLine(string line)
        => Regex.IsMatch(line.TrimStart(), @"^[A-Za-zÀ-ÖØ-öø-ÿ0-9 ._/()-]{2,40}\s*:\s+\S");
}
