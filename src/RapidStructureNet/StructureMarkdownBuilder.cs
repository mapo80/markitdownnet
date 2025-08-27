using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RapidLayoutNet;
using RapidOcrNet;
using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// Convert <see cref="StructureResult"/> into Markdown text with optional
/// figure extraction and table serialisation.
/// </summary>
public static class StructureMarkdownBuilder
{
    /// <summary>
    /// Build Markdown from a structure result. When <paramref name="imageDir"/>
    /// is provided, figure regions are saved there and referenced in the output.
    /// </summary>
    public static string Build(StructureResult result, string? imageDir = null)
    {
        var sb = new StringBuilder();
        int figIndex = 0;

        // Pre-associate captions with figures and tables
        var captionMap = new Dictionary<StructureRegion, string>();
        var captionRegions = new HashSet<StructureRegion>();
        foreach (var cap in result.Regions.Where(r => r.Type is LayoutLabel.FigureTitle or LayoutLabel.TableTitle))
        {
            if (cap.TextBlocks == null || cap.TextBlocks.Count == 0)
                continue;
            string caption = MergeText(cap.TextBlocks);
            var candidates = result.Regions.Where(r => r.PageIndex == cap.PageIndex &&
                ((cap.Type == LayoutLabel.FigureTitle && r.Type == LayoutLabel.Image) ||
                 (cap.Type == LayoutLabel.TableTitle && r.Type == LayoutLabel.Table)));
            StructureRegion? target = null;
            float minDist = float.MaxValue;
            foreach (var c in candidates)
            {
                float dist;
                if (cap.BBox.Top >= c.BBox.Bottom)
                    dist = cap.BBox.Top - c.BBox.Bottom;
                else if (c.BBox.Top >= cap.BBox.Bottom)
                    dist = c.BBox.Top - cap.BBox.Bottom;
                else
                    dist = 0;
                if (dist < minDist)
                {
                    minDist = dist;
                    target = c;
                }
            }
            if (target != null && minDist < 0.2f)
            {
                captionMap[target] = caption;
                captionRegions.Add(cap);
            }
        }

        foreach (var region in result.Regions
            .OrderBy(r => r.BBox.Top)
            .ThenBy(r => r.BBox.Left))
        {
            if (captionRegions.Contains(region))
                continue;

            switch (region.Type)
            {
                case LayoutLabel.Table:
                    if (region.Table != null)
                    {
                        string html = region.Table.Html.Trim();
                        if (!html.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine("<table><tbody>");
                            sb.AppendLine(html);
                            sb.AppendLine("</tbody></table>");
                        }
                        else
                        {
                            sb.AppendLine(html);
                        }
                        if (captionMap.TryGetValue(region, out var cap))
                        {
                            sb.AppendLine($"<div align=\"center\">{WebUtility.HtmlEncode(cap)}</div>");
                        }
                        sb.AppendLine();
                    }
                    break;
                case LayoutLabel.Image:
                    if (region.Image != null && imageDir != null)
                    {
                        Directory.CreateDirectory(imageDir);
                        string name = $"figure_{figIndex++}.png";
                        string path = Path.Combine(imageDir, name);
                        using var data = region.Image.Encode(SKEncodedImageFormat.Png, 100);
                        using var fs = File.OpenWrite(path);
                        data.SaveTo(fs);
                        sb.AppendLine("<div align=\"center\">");
                        sb.AppendLine($"<img src=\"{name}\"/>");
                        sb.AppendLine("</div>");
                        if (captionMap.TryGetValue(region, out var cap))
                        {
                            sb.AppendLine($"<div align=\"center\">{WebUtility.HtmlEncode(cap)}</div>");
                        }
                        sb.AppendLine();
                    }
                    break;
                case LayoutLabel.FigureTitle:
                case LayoutLabel.TableTitle:
                    if (region.TextBlocks != null && region.TextBlocks.Count > 0)
                    {
                        string caption = MergeText(region.TextBlocks);
                        sb.AppendLine($"<div align=\"center\">{WebUtility.HtmlEncode(caption)}</div>");
                        sb.AppendLine();
                    }
                    break;
                default:
                    if (region.TextBlocks != null && region.TextBlocks.Count > 0)
                    {
                        string text = MergeText(region.TextBlocks);
                        if (region.Type == LayoutLabel.ParagraphTitle || region.Type == LayoutLabel.DocTitle)
                            sb.Append('#').Append(' ').AppendLine(text);
                        else
                            sb.AppendLine(text);
                        sb.AppendLine();
                    }
                    break;
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string MergeText(IReadOnlyList<TextBlock> blocks)
    {
        var ordered = blocks
            .OrderBy(b => b.BoxPoints.Min(p => p.Y))
            .ThenBy(b => b.BoxPoints.Min(p => p.X))
            .Select(b =>
            {
                var pts = b.BoxPoints;
                float top = pts.Min(p => p.Y);
                float bottom = pts.Max(p => p.Y);
                float left = pts.Min(p => p.X);
                float height = bottom - top;
                return (Block: b, Top: top, Bottom: bottom, Left: left, Height: height);
            }).ToList();

        // Detect list markers
        bool isBullet(string t) => t.TrimStart().StartsWith("- ") || t.TrimStart().StartsWith("* ") || t.TrimStart().StartsWith("• ");
        bool isOrdered(string t) => Regex.IsMatch(t.TrimStart(), @"^\d+[\.\)]\s");

        if (ordered.Any(o => isBullet(o.Block.GetText()) || isOrdered(o.Block.GetText())))
        {
            var items = new List<(int Level, bool Ordered, StringBuilder Content)>();
            var startLines = ordered.Where(o => isBullet(o.Block.GetText()) || isOrdered(o.Block.GetText())).ToList();
            float baseLeft = startLines.Min(l => l.Left);
            float indentUnit = startLines.Select(l => l.Left - baseLeft).Where(d => d > 0).DefaultIfEmpty(startLines.First().Height).Min();

            (int Level, bool Ordered, StringBuilder Content)? current = null;

            foreach (var line in ordered)
            {
                string raw = line.Block.GetText();
                bool start = isBullet(raw) || isOrdered(raw);
                float offset = line.Left - baseLeft;
                int level = indentUnit > 0 ? (int)System.Math.Round(offset / indentUnit) : 0;
                bool orderedItem = isOrdered(raw);
                string text = raw.TrimStart();
                if (start)
                {
                    int idx = text.IndexOf(' ');
                    if (idx >= 0)
                        text = text[(idx + 1)..];
                    current = (level, orderedItem, new StringBuilder(EscapeMarkdown(text)));
                    items.Add(current.Value);
                }
                else if (current != null)
                {
                    current.Value.Content.Append(' ').Append(EscapeMarkdown(raw.Trim()));
                }
                else
                {
                    current = (level, false, new StringBuilder(EscapeMarkdown(raw.Trim())));
                    items.Add(current.Value);
                }
            }

            var counters = new Dictionary<int, int>();
            var sbList = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Ordered)
                    counters[item.Level] = counters.GetValueOrDefault(item.Level) + 1;
                else
                    counters[item.Level] = 0;
                string prefix = item.Ordered ? $"{counters[item.Level]}. " : "- ";
                sbList.Append(new string(' ', item.Level * 2)).Append(prefix).Append(item.Content).AppendLine();
            }
            return sbList.ToString().TrimEnd();
        }

        var paragraphs = new List<List<(TextBlock Block, float Top, float Bottom, float Left, float Height)>>();
        List<(TextBlock Block, float Top, float Bottom, float Left, float Height)>? currentPara = null;
        float prevBottom2 = 0;
        float prevLeft2 = 0;
        float prevHeight2 = 0;

        foreach (var line in ordered)
        {
            bool newPara = currentPara == null;
            if (!newPara)
            {
                float gap = line.Top - prevBottom2;
                float threshold = System.Math.Max(prevHeight2, line.Height) * 0.8f;
                float indent = System.Math.Abs(line.Left - prevLeft2);
                if (gap > threshold || indent > threshold)
                    newPara = true;
            }

            if (newPara)
            {
                currentPara = new List<(TextBlock, float, float, float, float)>();
                paragraphs.Add(currentPara);
            }

            currentPara!.Add(line);
            prevBottom2 = line.Bottom;
            prevLeft2 = line.Left;
            prevHeight2 = line.Height;
        }

        var sb = new StringBuilder();
        foreach (var para in paragraphs)
        {
            if (sb.Length > 0)
                sb.AppendLine().AppendLine();

            var texts = para.Select(p => EscapeMarkdown(p.Block.GetText().Trim())).ToList();
            string? prev = null;
            foreach (var text in texts)
            {
                if (prev is null)
                {
                    sb.Append(text);
                }
                else if (prev.EndsWith('-') && text.Length > 0 && char.IsLetterOrDigit(text[0]))
                {
                    sb.Length -= 1; // remove hyphen
                    sb.Append(text);
                }
                else if (text.Length > 0 && ",.;:!?".IndexOf(text[0]) >= 0)
                {
                    sb.Append(text);
                }
                else
                {
                    sb.Append(' ').Append(text);
                }
                prev = text;
            }
        }
        return sb.ToString();
    }

    private static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is '*' or '_' or '`' or '~' or '[' or ']' or '<' or '>' or '\\')
                sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
