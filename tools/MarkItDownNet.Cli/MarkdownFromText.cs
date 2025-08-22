using System.Text;
using System.Text.RegularExpressions;

namespace MarkItDownNet.Cli;

public static class MarkdownFromText
{
    static readonly Regex Heading = new("^[A-Z0-9 ]{3,}$");

    public static string Generate(string txt)
    {
        var lines = txt.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        foreach (var l in lines)
        {
            var line = l.TrimEnd();
            if (Heading.IsMatch(line))
            {
                int level = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3 ? 1 : 2;
                sb.Append('#', level).Append(' ').AppendLine(line);
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString().TrimEnd() + "\n";
    }
}
