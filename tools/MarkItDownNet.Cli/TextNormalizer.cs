using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MarkItDownNet.Cli;

public static class TextNormalizer
{
    static readonly Regex Link = new(@"\[(.*?)\]\((.*?)\)");
    static readonly Regex Md = new(@"[`*_>|#\[\]\(\)]");

    public static string Normalize(string markdown)
    {
        var text = Link.Replace(markdown, "$1");
        text = Md.Replace(text, " ");
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = Regex.Replace(lines[i].Trim(), "\\s+", " ");
        var joined = string.Join("\n", lines);
        joined = Regex.Replace(joined, " +", " ").Trim();
        return joined.Normalize(NormalizationForm.FormC);
    }
}
