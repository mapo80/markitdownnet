using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Linq;

namespace MarkItDownNet;

public class TextModeConfig
{
    public int ListIndentWidth { get; set; } = 2;
    public string[] BulletChars { get; set; } = new[] {"-", "*", "+", "•", "·", "–", "—"};
    public int MinItemsForListBlock { get; set; } = 2;
    public int HeadingMinLen { get; set; } = 3;
    public int HeadingMaxLen { get; set; } = 80;
    public string HeadingPunctBlacklist { get; set; } = ".:;!?";
    public double HeadingLetterRatioMin { get; set; } = 0.6;
    public int CodeMinLines { get; set; } = 3;
    public int CodeMinIndent { get; set; } = 4;
    public double CodeSymbolDensityMin { get; set; } = 0.25;
    public bool Dehyphenation { get; set; } = true;
    public int MinKeyValueRows { get; set; } = 3;
    public int KeyMaxLen { get; set; } = 40;
    public double KVMaxKeyEndPunctPct { get; set; } = 0.3;
    public double KVMaxPipePct { get; set; } = 0.2;
    public int MonoMinRows { get; set; } = 3;
    public int MonoMinGap { get; set; } = 2;
    public int MonoColTolerance { get; set; } = 1;
    public double MonoSameColPct { get; set; } = 0.7;
    public double MonoMinGapLinesPct { get; set; } = 0.6;
    public double MonoShortRowPctMax { get; set; } = 0.3;
    public double NumericColPct { get; set; } = 0.6;
    public int MonoTableMinCols { get; set; } = 2; // legacy
    public int MonoTableMinRows { get; set; } = 3; // legacy
    public int MonoTableMinSpaceGap { get; set; } = 2; // legacy
    public int MonoTableColTolerance { get; set; } = 1; // legacy
}

public static class TextModeConverter
{
    public static string Convert(string text, string mode, TextModeConfig config)
    {
        text = Normalize(text);
        if (mode == "post-1R")
        {
            text = Reflow(text, config, false, false);
            text = DetectLists(text, config, false, false);
        }
        else if (mode == "post-1S")
        {
            text = Reflow1S(text, config);
            text = DetectLists(text, config, false, false);
        }
        else if (mode == "post-2")
        {
            text = Reflow1S(text, config);
            text = DetectLists(text, config, false, false);
            text = DetectKeyValueTables2(text, config);
            text = DetectMonoTables2(text, config);
        }
        else if (mode == "post-v0" || mode == "post-v01" || mode=="post-v02" || mode=="post-v03")
        {
            bool v02 = mode=="post-v02" || mode=="post-v03";
            bool v03 = mode=="post-v03";
            if (v03)
                text = StripHeaders(text);
            text = Reflow(text, config, v02, v03);
            text = DetectLists(text, config, v02, v03);

            if (mode != "post-v0")
            {
                text = DetectHeadings(text, config, mode == "post-v01" || mode=="post-v02", v02, v03);
                text = DetectCodeBlocks(text, config);
                text = DetectHorizontalRules(text, config, v02);
                text = DetectKeyValueTables(text, config, v02, v03);
                text = DetectMonoTables(text, config, v02 || v03);
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

    private static string StripHeaders(string text)
    {
        var lines = text.Split('\n');
        var occ = new Dictionary<string, List<int>>();
        for (int i = 0; i < lines.Length; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0) continue;
            if (!occ.ContainsKey(t)) occ[t] = new List<int>();
            occ[t].Add(i);
        }
        var remove = new HashSet<int>();
        foreach (var kv in occ)
        {
            var idxs = kv.Value;
            if (idxs.Count >= 2 && idxs.Zip(idxs.Skip(1), (a, b) => b - a).Any(g => g > 1))
                foreach (var idx in idxs) remove.Add(idx);
        }
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (Regex.IsMatch(trimmed, @"^(page|pagina|seite|página)\s*\d+(\s*[/\-]\s*\d+)?$", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(trimmed, @"^\d+\s*[/\-]\s*\d+$"))
            {
                sb.AppendLine("---");
                continue;
            }
            if (!remove.Contains(i))
                sb.AppendLine(lines[i]);
        }
        return sb.ToString();
    }

    private static string DetectKeyValueTables2(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        int i = 0;
        var kvRegex = new Regex(@"^[^:\n]{2,40}:\s+\S.*$");
        while (i < lines.Length)
        {
            var trimmed = lines[i].TrimStart();
            if (kvRegex.IsMatch(trimmed))
            {
                var raw = new List<string>();
                var rows = new List<(string key, string val)>();
                while (i < lines.Length && kvRegex.IsMatch(lines[i].TrimStart()))
                {
                    var line = lines[i];
                    trimmed = line.TrimStart();
                    var m = Regex.Match(trimmed, @"^([^:\n]{2,40}):\s+(.*)$");
                    var key = m.Groups[1].Value.Trim();
                    var val = m.Groups[2].Value.Trim();
                    raw.Add(line);
                    int j = i + 1;
                    while (j < lines.Length)
                    {
                        var next = lines[j];
                        var nextTrim = next.TrimStart();
                        if (string.IsNullOrWhiteSpace(nextTrim)) break;
                        bool join = false;
                        if (next.StartsWith("  ")) join = true;
                        else if (Regex.IsMatch(nextTrim, @"^(of|for|with|per|con|di|da|the)\b", RegexOptions.IgnoreCase)) join = true;
                        else if (val.EndsWith(',') || val.EndsWith(';')) join = true;
                        if (!join) break;
                        val = (val + " " + nextTrim).Trim();
                        raw.Add(next);
                        j++;
                    }
                    val = Regex.Replace(val, @"\s+", " ").Trim();
                    rows.Add((key, val));
                    i = j;
                }
                int total = raw.Count;
                double punctPct = rows.Count(r => r.key.EndsWith('.') || r.key.EndsWith('?') || r.key.EndsWith('!')) / (double)rows.Count;
                double pipePct = raw.Count(r => r.Contains('|')) / (double)total;
                double avgKey = rows.Average(r => r.key.Length);
                int distinct = rows.Select(r => r.key).Distinct().Count();
                if (rows.Count >= config.MinKeyValueRows && punctPct <= config.KVMaxKeyEndPunctPct && pipePct <= config.KVMaxPipePct && avgKey <= config.KeyMaxLen && distinct >= 2)
                {
                    sb.AppendLine("| Key | Value |");
                    sb.AppendLine("| --- | ----- |");
                    foreach (var r in rows)
                        sb.AppendLine($"| {r.key} | {r.val} |");
                }
                else
                {
                    foreach (var l in raw)
                        sb.AppendLine(l);
                }
                continue;
            }
            sb.AppendLine(lines[i]);
            i++;
        }
        return sb.ToString();
    }

    private static string DetectMonoTables2(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        var gapRegex = new Regex(@"\s{" + config.MonoMinGap + @",}");
        int i = 0;
        while (i < lines.Length)
        {
            if (!gapRegex.IsMatch(lines[i]))
            {
                sb.AppendLine(lines[i]);
                i++;
                continue;
            }
            var block = new List<string>();
            while (i < lines.Length && gapRegex.IsMatch(lines[i]))
            {
                block.Add(lines[i]);
                i++;
            }
            int total = block.Count;
            int withTwo = block.Count(l => Regex.Matches(l, @"\s{" + config.MonoMinGap + @",}").Count >= 2);
            if (total < config.MonoMinRows || withTwo < total * config.MonoMinGapLinesPct)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            var starts = new List<int>();
            foreach (var l in block)
                foreach (Match m in Regex.Matches(l, @"\s{" + config.MonoMinGap + @",}"))
                    starts.Add(m.Index);
            if (starts.Count == 0)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            starts.Sort();
            var clusters = new List<List<int>>();
            foreach (var pos in starts)
            {
                bool placed = false;
                foreach (var c in clusters)
                {
                    if (Math.Abs(c.Average() - pos) <= config.MonoColTolerance)
                    {
                        c.Add(pos);
                        placed = true;
                        break;
                    }
                }
                if (!placed) clusters.Add(new List<int> { pos });
            }
            var boundaries = clusters.Select(c => (int)Math.Round(c.Average())).OrderBy(x => x).ToList();
            if (boundaries.Count == 0)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            var rows = new List<List<string>>();
            foreach (var l in block)
            {
                var row = new List<string>();
                int start = 0;
                foreach (var b in boundaries)
                {
                    int end = Math.Min(b, l.Length);
                    var cell = l.Substring(start, end - start).Trim();
                    cell = Regex.Replace(cell, @"\s+", " ");
                    row.Add(cell);
                    int skip = b;
                    while (skip < l.Length && l[skip] == ' ') skip++;
                    start = skip;
                }
                var last = start < l.Length ? l.Substring(start).TrimEnd() : "";
                last = Regex.Replace(last.Trim(), @"\s+", " ");
                row.Add(last);
                rows.Add(row);
            }
            var counts = rows.Select(r => r.Count).ToList();
            int mode = counts.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
            int modeLines = counts.Count(c => c == mode);
            if (modeLines < config.MonoMinRows || modeLines < total * config.MonoSameColPct || mode < 2)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            int shortRows = block.Count(l => l.Trim().Length < 8);
            if (shortRows > total * config.MonoShortRowPctMax)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            int bulletLines = block.Count(l => Regex.IsMatch(l.TrimStart(), @"^([-*•·]|\d+[.)])"));
            if (bulletLines >= total * 0.5)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            var first = block[0].Trim();
            var second = block.Count > 1 ? block[1].Trim() : "";
            double num1 = NumericDensityLine(first);
            double num2 = NumericDensityLine(second);
            bool header = (num2 > 0 && num1 < num2 * 0.5) || IsTitleCase(first) || Regex.IsMatch(first, @"^[A-Z0-9\s]+$");
            if (!header)
            {
                foreach (var l in block) sb.AppendLine(l);
                continue;
            }
            var headerRow = rows[0];
            var dataRows = rows.Skip(1).ToList();
            var numRegex = new Regex(@"^[\p{Sc}]?\s*\d{1,3}([.,]\d{3})*([.,]\d+)?%?$");
            var aligns = new List<string>();
            for (int c = 0; c < headerRow.Count; c++)
            {
                int nonEmpty = dataRows.Count(r => c < r.Count && !string.IsNullOrEmpty(r[c]));
                int numericCells = dataRows.Count(r => c < r.Count && numRegex.IsMatch(r[c]));
                bool isNum = nonEmpty > 0 && numericCells >= nonEmpty * config.NumericColPct;
                aligns.Add(isNum ? "---:" : "---");
            }
            sb.AppendLine("| " + string.Join(" | ", headerRow) + " |");
            sb.AppendLine("| " + string.Join(" | ", aligns) + " |");
            foreach (var r in dataRows)
            {
                var cells = new List<string>();
                for (int c = 0; c < headerRow.Count; c++)
                    cells.Add(c < r.Count ? r[c] : "");
                sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            }
        }
        return sb.ToString();
    }

    private static double NumericDensityLine(string line)
    {
        double digits = line.Count(char.IsDigit);
        double chars = line.Replace(" ", "").Length;
        return chars == 0 ? 0 : digits / chars;
    }

    private static string Reflow(string text, TextModeConfig config, bool v02, bool v03)
    {
        var lines = text.Split('\n');
        double avgLen = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim().Length).DefaultIfEmpty(0).Average();
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
            bool CanMerge(int idx)
            {
                if (idx + 1 >= lines.Length) return false;
                var nl = lines[idx + 1];
                if (string.IsNullOrWhiteSpace(nl)) return false;
                if (IsListLine(nl, config) || IsCodeLine(nl, config.CodeMinIndent)) return false;
                if ((v02 || v03) && IsKeyValueLine(nl)) return false;
                if (paragraph.ToString().EndsWith(":")) return false;
                var trimmed = nl.TrimStart();
                if (IsTableLikeLine(nl, config) || IsProbableCode(nl, config) ||
                    trimmed.StartsWith("|") || trimmed.StartsWith("```") ||
                    Regex.IsMatch(trimmed, @"^(https?://|www\.|[\w.+-]+@[\w.-]+)"))
                    return false;
                return true;
            }
            bool OkMerge(int idx)
            {
                var current = lines[idx].TrimEnd();
                return current.Length < 0.9 * avgLen || !Regex.IsMatch(current, "[.!?]$");
            }
            while (CanMerge(i) && OkMerge(i))
            {
                var next = lines[i+1].TrimStart();
                if (config.Dehyphenation && paragraph.ToString().EndsWith("-") && Regex.IsMatch(next, "^[a-z0-9].*") && !Regex.IsMatch(paragraph.ToString(), @"[A-Z]{2,}\d+$"))
                {
                    paragraph.Length--;
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

    private static string Reflow1S(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        double avgLen = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim().Length).DefaultIfEmpty(0).Average();
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length;)
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
            bool CanMerge(int idx)
            {
                if (idx + 1 >= lines.Length) return false;
                var curr = lines[idx];
                var next = lines[idx + 1];
                if (string.IsNullOrWhiteSpace(next)) return false;
                if (IsListLine(next, config) || IsCodeLine(next, config.CodeMinIndent)) return false;
                var currTrim = curr.TrimEnd();
                var nextTrim = next.TrimStart();
                int currPipes = currTrim.Count(c => c == '|');
                int nextPipes = nextTrim.Count(c => c == '|');
                if (currPipes >= 2 || nextPipes >= 2) return false;
                if (currPipes >= 1 && nextPipes >= 1) return false;
                if (currTrim.EndsWith(':') || currTrim.EndsWith(';')) return false;
                if (IsProbableCode(nextTrim, config)) return false;
                if (Regex.IsMatch(nextTrim, @"^(https?://|www\.|[^\s]+@[^\s]+)", RegexOptions.IgnoreCase)) return false;
                int currIndent = curr.TakeWhile(ch => ch == ' ').Count();
                int nextIndent = next.TakeWhile(ch => ch == ' ').Count();
                if (Math.Abs(currIndent - nextIndent) >= 2) return false;
                if (Regex.IsMatch(nextTrim, @"^\d+\s*\|")) return false;
                bool currDigit = Regex.IsMatch(curr, @"\d");
                bool nextDigit = Regex.IsMatch(next, @"\d");
                bool currSpaceRun = Regex.IsMatch(curr, @" {2,}");
                bool nextSpaceRun = Regex.IsMatch(next, @" {2,}");
                if (currDigit && nextDigit && currSpaceRun && nextSpaceRun) return false;
                return true;
            }
            bool OkMerge(int idx)
            {
                if (idx + 1 >= lines.Length) return false;
                var current = lines[idx].TrimEnd();
                var next = lines[idx + 1].TrimStart();
                if (!(current.Length < 0.6 * avgLen)) return false;
                if (Regex.IsMatch(current, "[.!?;:]$")) return false;
                if (Regex.IsMatch(next, @"^[A-Z][a-z]{0,3}\b") && !next.Contains('|')) return false;
                return true;
            }
            while (CanMerge(i) && OkMerge(i))
            {
                var next = lines[i + 1].TrimStart();
                if (config.Dehyphenation && paragraph.ToString().EndsWith("-") && Regex.IsMatch(next, "^[a-z0-9]") && !Regex.IsMatch(paragraph.ToString(), @"[A-Z]{2,}\d+$"))
                {
                    paragraph.Length--;
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
        if (Regex.IsMatch(trimmed, @"^[ivxlcdm]+[\.)]\s", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private static bool IsCodeLine(string line, int minIndent)
        => line.StartsWith(new string(' ', minIndent));

    private static bool IsProbableCode(string line, TextModeConfig config)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;
        double symbols = Regex.Matches(trimmed, "[{}();<>:=/#]").Count;
        return symbols / trimmed.Length >= config.CodeSymbolDensityMin;
    }

    private static bool IsTableLikeLine(string line, TextModeConfig config)
    {
        if (line.Count(c => c == '|') >= 2) return true;
        return Regex.IsMatch(line, @"\S+\s{" + config.MonoTableMinSpaceGap + @",}\S+\s{" + config.MonoTableMinSpaceGap + @",}\S+");
    }

    private static string DetectLists(string text, TextModeConfig config, bool v02, bool v03)
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
                    while (j < lines.Length && !TryParseBullet(lines[j], config, out _, out _, out _))
                    {
                        if (lines[j].StartsWith(new string(' ', indent + config.ListIndentWidth + 2)) ||
                            (lines[j].StartsWith(new string(' ', indent)) && builder.Length > 0 && (builder[^1] == ',' || builder[^1] == ';')))
                        {
                            builder.Append(' ').Append(lines[j].Trim());
                            j++;
                        }
                        else break;
                    }
                    items.Add((indent, num, builder.ToString()));
                    i = j;
                }
                if (items.Count >= config.MinItemsForListBlock)
                {
                    bool numeric = items.All(it => it.num.HasValue);
                    foreach (var it in items)
                    {
                        string bullet = numeric ? "1. " : "- ";
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

        var numMatch = Regex.Match(trimmed, @"^([0-9]+|[ivxlcdm]+)[\.)]\s+(.*)", RegexOptions.IgnoreCase);
        if (numMatch.Success)
        {
            var raw = numMatch.Groups[1].Value;
            number = int.TryParse(raw, out var n) ? n : RomanToInt(raw);
            content = numMatch.Groups[2].Value;
            if (LooksLikeFalseBullet(content)) { number=null; return false; }
            return true;
        }
        foreach (var b in config.BulletChars)
        {
            if (trimmed.StartsWith(b + " "))
            {
                var after = trimmed.Substring(b.Length + 1);
                if (LooksLikeFalseBullet(after)) return false;
                content = after;
                return true;
            }
        }
        return false;
    }

    private static string DetectHeadings(string text, TextModeConfig config, bool allowWhitelist, bool v02, bool v03)
    {
        var lines = text.Split('\n');
        string[] whitelist = new[]{"Dettaglio","Riepilogo","Totali","Contributi","Trattenute"};
        bool first=false;
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
            if ((v02 || v03) && line.Contains(':') && !nextBlank) continue;
            double letters = line.Count(char.IsLetter);
            double ratio = letters / line.Replace(" ", "").Length;
            if (ratio < config.HeadingLetterRatioMin) continue;
            if (!prevBlank || !nextBlank) continue;
            if (v03)
            {
                if (!(IsTitleCase(line) || line.ToUpper()==line) || LooksLikeFalseBullet(line)) continue;
            }
            int level = !first ? 1 : 2;
            lines[i] = new string('#', level) + " " + line;
            first=true;
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

    private static string DetectKeyValueTables(string text, TextModeConfig config, bool v02, bool v03)
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
                    string contPattern = v03?"^(di |of |for |per |con |dal )":(v02?"^(di |per |dal )":"");
                    while(j<lines.Length && (Regex.IsMatch(lines[j],"^ {2,}\\S") || (!string.IsNullOrEmpty(contPattern) && Regex.IsMatch(lines[j].TrimStart(),contPattern,RegexOptions.IgnoreCase))))
                    {
                        val += " " + lines[j].Trim();
                        j++;
                    }
                    rows.Add((key,val));
                    i=j;
                }
                if (rows.Count>=config.MinKeyValueRows && (!(v02||v03) || rows.Count(r=>r.key.EndsWith("."))<=rows.Count*0.3))
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

    private static bool LooksLikeFalseBullet(string text)
    {
        return Regex.IsMatch(text, @"^\d{1,2}([./-])\d{1,2}\1\d{2,4}\b") ||
               Regex.IsMatch(text, @"^[\p{Sc}]?\s*\d{1,3}([.,]\d{3})*([.,]\d{2})?\b") ||
               Regex.IsMatch(text, @"^\d{1,3}([.,]\d{1,2})?%") ||
               Regex.IsMatch(text, @"^[A-Z]{2,}\d+[A-Z0-9-]*\b");
    }

    private static bool IsTitleCase(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length==0) return false;
        foreach(var w in words)
        {
            if (!char.IsUpper(w[0])) return false;
            for(int i=1;i<w.Length;i++) if (!char.IsLower(w[i])) return false;
        }
        return true;
    }

    private static int RomanToInt(string s)
    {
        int total=0, prev=0;
        foreach(char c in s.ToUpper())
        {
            int val = c switch { 'I'=>1,'V'=>5,'X'=>10,'L'=>50,'C'=>100,'D'=>500,'M'=>1000,_=>0 };
            if (val>prev) total += val - 2*prev; else total += val; prev=val;
        }
        return total;
    }

    private static bool IsKeyValueLine(string line)
        => Regex.IsMatch(line.TrimStart(), @"^[^:]{2,40}:\s+\S");
}
