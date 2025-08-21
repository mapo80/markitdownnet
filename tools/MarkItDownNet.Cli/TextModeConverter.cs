using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MarkItDownNet;

public class TextModeConfig
{
    public int ListIndentWidth { get; set; } = 2;
    public string[] BulletChars { get; set; } = new[] {"-", "*", "•", "+"};
    public int MinItemsForListBlock { get; set; } = 2;
    public int HeadingMinLen { get; set; } = 3;
    public int HeadingMaxLen { get; set; } = 80;
    public string HeadingPunctBlacklist { get; set; } = ".:;!?";
    public double HeadingLetterRatioMin { get; set; } = 0.6;
    public int CodeMinLines { get; set; } = 3;
    public int CodeMinIndent { get; set; } = 4;
    public double CodeSymbolDensityMin { get; set; } = 0.25;
    public bool Dehyphenation { get; set; } = true;
}

public static class TextModeConverter
{
    public static string Convert(string text, string mode, TextModeConfig config)
    {
        text = Normalize(text);
        if (mode == "post")
        {
            text = Reflow(text, config);
            text = DetectLists(text, config);
            text = DetectHeadings(text, config);
            text = DetectCodeBlocks(text, config);
            text = DetectHorizontalRules(text);
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

    private static string Reflow(string text, TextModeConfig config)
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
            while (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i+1]) && !IsListLine(lines[i+1], config) && !IsCodeLine(lines[i+1], config.CodeMinIndent))
            {
                var next = lines[i+1].TrimStart();
                if (config.Dehyphenation && paragraph.ToString().EndsWith("-") && Regex.IsMatch(next, "^[A-Za-z].*"))
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

    private static string DetectLists(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        int i = 0;
        while (i < lines.Length)
        {
            if (IsListLine(lines[i], config))
            {
                var items = new List<string>();
                while (i < lines.Length && IsListLine(lines[i], config))
                {
                    items.Add(NormalizeBullet(lines[i], config));
                    i++;
                }
                if (items.Count >= config.MinItemsForListBlock)
                {
                    foreach (var item in items)
                        sb.AppendLine(item);
                }
                else
                {
                    foreach (var item in items)
                        sb.AppendLine(item.Substring(2)); // drop bullet
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

    private static string NormalizeBullet(string line, TextModeConfig config)
    {
        var trimmed = line.TrimStart();
        int indent = line.Length - trimmed.Length;
        string content;
        string bullet;
        if (Regex.IsMatch(trimmed, @"^[0-9]+\)\s"))
        {
            var m = Regex.Match(trimmed, @"^([0-9]+)\)\s(.*)");
            bullet = m.Groups[1].Value + ". ";
            content = m.Groups[2].Value;
        }
        else if (Regex.IsMatch(trimmed, @"^[0-9]+\.\s"))
        {
            var m = Regex.Match(trimmed, @"^([0-9]+)\.\s(.*)");
            bullet = m.Groups[1].Value + ". ";
            content = m.Groups[2].Value;
        }
        else
        {
            bullet = "- ";
            content = trimmed.Substring(2);
        }
        var nesting = indent / config.ListIndentWidth;
        return new string(' ', nesting * config.ListIndentWidth) + bullet + content;
    }

    private static string DetectHeadings(string text, TextModeConfig config)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            if (line.Length < config.HeadingMinLen || line.Length > config.HeadingMaxLen) continue;
            if (config.HeadingPunctBlacklist.Contains(line[^1])) continue;
            double letters = line.Count(char.IsLetter);
            double ratio = letters / line.Replace(" ", "").Length;
            if (ratio < config.HeadingLetterRatioMin) continue;
            bool prevBlank = i == 0 || string.IsNullOrWhiteSpace(lines[i-1]);
            bool nextBlank = i == lines.Length-1 || string.IsNullOrWhiteSpace(lines[i+1]);
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

    private static string DetectHorizontalRules(string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var t = lines[i].Trim();
            if (Regex.IsMatch(t, @"^([-_*])\1{2,}$"))
                lines[i] = "---";
        }
        return string.Join('\n', lines);
    }
}
