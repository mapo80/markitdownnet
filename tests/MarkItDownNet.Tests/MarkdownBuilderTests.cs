using System;
using System.IO;
using System.Collections.Generic;
using RapidLayoutNet;
using RapidOcrNet;
using RapidTableNet;
using RapidStructureNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class MarkdownBuilderTests
{
    [Fact]
    public void Build_handles_text_table_image()
    {
        var block1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "Hello" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var block2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 25), new SKPointI(10, 25), new SKPointI(10, 35), new SKPointI(0, 35) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "world" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var textRegion = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { block1, block2 });

        var tableHtml = "<table><thead><tr><td>a</td></tr></thead></table>";
        var table = new TableResult(new[] { tableHtml }, Array.Empty<float[]>(), tableHtml, 0, 0, 0);
        var tableRegion = new StructureRegion(LayoutLabel.Table, new SKRect(0, 0, 1, 1), 1, 0, null, table, null);

        var img = new SKBitmap(1, 1);
        using (var c = new SKCanvas(img)) c.Clear(SKColors.Black);
        var imgRegion = new StructureRegion(LayoutLabel.Image, new SKRect(0, 0, 1, 1), 1, 0, img, null, null);

        var result = new StructureResult(new[] { textRegion, tableRegion, imgRegion }, new OcrResult { TextBlocks = new[] { block1, block2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string md = StructureMarkdownBuilder.Build(result, dir);

        Assert.Contains("Hello\n\nworld", md);
        Assert.Contains(tableHtml, md);
        Assert.Contains("<img src=\"figure_0.png\"/>", md);
        Assert.True(File.Exists(Path.Combine(dir, "figure_0.png")));
    }

    [Fact]
    public void Build_merges_lines_and_splits_paragraphs()
    {
        var block1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "Hello" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var block2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 12), new SKPointI(10, 12), new SKPointI(10, 22), new SKPointI(0, 22) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "world" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var block3 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(50, 50), new SKPointI(60, 50), new SKPointI(60, 60), new SKPointI(50, 60) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "Next" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };

        var region = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { block2, block3, block1 });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { block1, block2, block3 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);

        Assert.Equal("Hello world\n\nNext", md.Trim());
    }

    [Fact]
    public void Build_sorts_regions_and_escapes_markdown()
    {
        var block1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "*top*" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var block2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 50), new SKPointI(10, 50), new SKPointI(10, 60), new SKPointI(0, 60) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "bottom" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };

        var top = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 0.4f), 1, 0, null, null, new List<TextBlock> { block1 });
        var bottom = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0.5f, 1, 1), 1, 0, null, null, new List<TextBlock> { block2 });
        // Provide regions out of order to ensure sorting
        var result = new StructureResult(new[] { bottom, top }, new OcrResult { TextBlocks = new[] { block1, block2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);

        Assert.StartsWith("\\*top\\*", md.Trim());
        Assert.Contains("bottom", md);
    }

    [Fact]
    public void Build_outputs_lists()
    {
        var item1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "- first" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var item2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 20), new SKPointI(10, 20), new SKPointI(10, 30), new SKPointI(0, 30) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "- second" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var region = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { item1, item2 });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { item1, item2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);
        Assert.Equal("- first\n- second", md.Trim());
    }

    [Fact]
    public void Build_outputs_ordered_lists()
    {
        var item1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "1. one" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var item2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 20), new SKPointI(10, 20), new SKPointI(10, 30), new SKPointI(0, 30) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "2. two" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var region = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { item1, item2 });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { item1, item2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);
        Assert.Equal("1. one\n2. two", md.Trim());
    }

    [Fact]
    public void Build_outputs_nested_lists()
    {
        var outer = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "- outer" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var inner = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(20, 20), new SKPointI(30, 20), new SKPointI(30, 30), new SKPointI(20, 30) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "- inner" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var outer2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 40), new SKPointI(10, 40), new SKPointI(10, 50), new SKPointI(0, 50) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "- outer2" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var region = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { outer, inner, outer2 });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { outer, inner, outer2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);
        Assert.Equal("- outer\n  - inner\n- outer2", md.Trim());
    }

    [Fact]
    public void Build_outputs_captions()
    {
        var cap = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "A figure caption" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var region = new StructureRegion(LayoutLabel.FigureTitle, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { cap });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { cap }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result).Replace("\r", "");
        Assert.Equal("<div align=\"center\">A figure caption</div>", md.Trim());
    }

    [Fact]
    public void Build_associates_caption_with_image()
    {
        var cap = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 60), new SKPointI(10, 60), new SKPointI(10, 70), new SKPointI(0, 70) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "An image" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var img = new SKBitmap(10, 10);
        using (var c = new SKCanvas(img)) c.Clear(SKColors.Black);
        var imgRegion = new StructureRegion(LayoutLabel.Image, new SKRect(0, 0, 1, 0.5f), 1, 0, img, null, null);
        var capRegion = new StructureRegion(LayoutLabel.FigureTitle, new SKRect(0, 0.55f, 1, 0.7f), 1, 0, null, null, new List<TextBlock> { cap });
        var result = new StructureResult(new[] { imgRegion, capRegion }, new OcrResult { TextBlocks = new[] { cap }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string md = StructureMarkdownBuilder.Build(result, dir).Replace("\r", "");
        Assert.Contains("<div align=\"center\">\n<img src=\"figure_0.png\"/>\n</div>\n<div align=\"center\">An image</div>", md.Trim());
    }

    [Fact]
    public void Build_associates_caption_before_image()
    {
        var cap = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "Before" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var img = new SKBitmap(10, 10);
        using (var c = new SKCanvas(img)) c.Clear(SKColors.Black);
        var capRegion = new StructureRegion(LayoutLabel.FigureTitle, new SKRect(0, 0, 1, 0.1f), 1, 0, null, null, new List<TextBlock> { cap });
        var imgRegion = new StructureRegion(LayoutLabel.Image, new SKRect(0, 0.2f, 1, 0.7f), 1, 0, img, null, null);
        var result = new StructureResult(new[] { capRegion, imgRegion }, new OcrResult { TextBlocks = new[] { cap }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string md = StructureMarkdownBuilder.Build(result, dir).Replace("\r", "");
        Assert.Equal("<div align=\"center\">\n<img src=\"figure_0.png\"/>\n</div>\n<div align=\"center\">Before</div>", md.Trim());
    }

    [Fact]
    public void Build_associates_caption_with_table()
    {
        var cap = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 60), new SKPointI(10, 60), new SKPointI(10, 70), new SKPointI(0, 70) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "Table caption" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var tableHtml = "<table><tbody><tr><td>x</td></tr></tbody></table>";
        var table = new TableResult(new[] { tableHtml }, Array.Empty<float[]>(), tableHtml, 0, 0, 0);
        var tableRegion = new StructureRegion(LayoutLabel.Table, new SKRect(0, 0, 1, 0.5f), 1, 0, null, table, null);
        var capRegion = new StructureRegion(LayoutLabel.TableTitle, new SKRect(0, 0.55f, 1, 0.7f), 1, 0, null, null, new List<TextBlock> { cap });
        var result = new StructureResult(new[] { tableRegion, capRegion }, new OcrResult { TextBlocks = new[] { cap }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result).Replace("\r", "");
        Assert.Contains("<table><tbody><tr><td>x</td></tr></tbody></table>\n<div align=\"center\">Table caption</div>", md);
    }

    [Fact]
    public void Build_wraps_table_html_when_missing_tags()
    {
        var tableHtml = "<tr><td>1</td></tr>";
        var table = new TableResult(new[] { tableHtml }, Array.Empty<float[]>(), tableHtml, 0, 0, 0);
        var region = new StructureRegion(LayoutLabel.Table, new SKRect(0, 0, 1, 1), 1, 0, null, table, null);
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = Array.Empty<TextBlock>(), DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);
        string md = StructureMarkdownBuilder.Build(result).Replace("\r", "");
        Assert.Equal("<table><tbody>\n<tr><td>1</td></tr>\n</tbody></table>", md.Trim());
    }

    [Fact]
    public void MergeText_joins_hyphenated_lines()
    {
        var block1 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 0), new SKPointI(10, 0), new SKPointI(10, 10), new SKPointI(0, 10) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "hyphen-" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var block2 = new TextBlock
        {
            BoxPoints = new[] { new SKPointI(0, 12), new SKPointI(10, 12), new SKPointI(10, 22), new SKPointI(0, 22) },
            BoxScore = 1,
            AngleIndex = -1,
            AngleScore = 0,
            AngleTime = 0,
            Chars = new[] { "ation" },
            CharScores = new[] { 1f },
            CrnnTime = 0,
            BlockTime = 0
        };
        var region = new StructureRegion(LayoutLabel.Text, new SKRect(0, 0, 1, 1), 1, 0, null, null, new List<TextBlock> { block1, block2 });
        var result = new StructureResult(new[] { region }, new OcrResult { TextBlocks = new[] { block1, block2 }, DbNetTime = 0, DetectTime = 0, StrRes = string.Empty }, 0, 0, 0, 0);

        string md = StructureMarkdownBuilder.Build(result);
        Assert.Contains("hyphenation", md);
    }
}
