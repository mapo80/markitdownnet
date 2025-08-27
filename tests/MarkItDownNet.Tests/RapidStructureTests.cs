using System;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using RapidLayoutNet;
using RapidOcrNet;
using RapidStructureNet;
using RapidTableNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class RapidStructureTests : IDisposable
{
    private readonly LayoutDetector _layout;
    private readonly TableRecognizer _table;
    private readonly RapidOcr _ocr;
    private readonly RapidStructure _structure;

    private static readonly string TrainingDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dataset", "training"));
    private static readonly string ValidationDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dataset", "validation"));

    static RapidStructureTests()
    {
        Environment.SetEnvironmentVariable("ORT_LOG_VERBOSITY_LEVEL", "0");
        Environment.SetEnvironmentVariable("ORT_LOG_SEVERITY_LEVEL", "3");
    }

    public RapidStructureTests()
    {
        string layoutModel = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidLayoutNet", "models", "PP-DocLayout-S_infer.onnx"));
        _layout = new LayoutDetector();
        _layout.InitModel(layoutModel);

        string tableModelDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidTableNet", "models"));
        _table = new TableRecognizer();
        _table.InitModel(TableModel.SlanetPlus, tableModelDir);

        _ocr = new RapidOcr();
        _ocr.InitModels(OcrLanguage.English, OcrVersion.V5);
        _structure = new RapidStructure(_layout, new RapidOcrEngine(_ocr), _table);
    }

    public void Dispose()
    {
        _layout.Dispose();
        _table.Dispose();
        _ocr.Dispose();
    }

    public static IEnumerable<object[]> TrainingImages()
    {
        foreach (var file in Directory.EnumerateFiles(TrainingDir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg")
                yield return new object[] { file };
        }
    }

    public static IEnumerable<object[]> ValidationImages()
    {
        foreach (var dir in Directory.EnumerateDirectories(ValidationDir))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith("_"))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg")
                    yield return new object[] { file };
            }
        }
    }

    public static IEnumerable<object[]> AllImages()
    {
        foreach (var img in TrainingImages())
            yield return img;
        foreach (var img in ValidationImages())
            yield return img;
    }

    private static SKBitmap DecodeColor(string path)
    {
        var bmp = SKBitmap.Decode(path);
        if (bmp.ColorType != SKColorType.Bgra8888)
        {
            var converted = bmp.Copy(SKColorType.Bgra8888);
            bmp.Dispose();
            return converted;
        }
        return bmp;
    }

    [Theory]
    [MemberData(nameof(TrainingImages))]
    public void Analyze_returns_regions_with_normalised_boxes(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);

        Assert.NotEmpty(result.Regions);
        Assert.Equal(0f, result.Orientation);
        foreach (var r in result.Regions)
        {
            Assert.InRange(r.BBox.Left, 0, 1);
            Assert.InRange(r.BBox.Top, 0, 1);
            Assert.InRange(r.BBox.Right, 0, 1);
            Assert.InRange(r.BBox.Bottom, 0, 1);
            Assert.InRange(r.Score, 0f, 1f);
            Assert.Equal(0, r.PageIndex);
        }

        Assert.All(result.Regions.Where(r => r.Type == LayoutLabel.Table), r =>
        {
            Assert.NotNull(r.Table);
            Assert.False(string.IsNullOrWhiteSpace(r.Table!.Html));
            Assert.NotEmpty(r.Table!.CellBoxes);
            Assert.True(r.Table!.TotalTimeMs > 0);
        });

        Assert.NotEmpty(result.Ocr.TextBlocks);
        Assert.All(result.Regions.Where(r => r.Type != LayoutLabel.Table && r.Type != LayoutLabel.Image), r =>
        {
            Assert.NotNull(r.TextBlocks);
            Assert.NotEmpty(r.TextBlocks!);
            foreach (var block in r.TextBlocks!)
            {
                var pts = block.BoxPoints;
                float minX = pts.Min(p => p.X);
                float minY = pts.Min(p => p.Y);
                float maxX = pts.Max(p => p.X);
                float maxY = pts.Max(p => p.Y);
                var textRect = new SKRect(minX, minY, maxX, maxY);
                var regionRect = new SKRect(r.BBox.Left * bmp.Width, r.BBox.Top * bmp.Height,
                                            r.BBox.Right * bmp.Width, r.BBox.Bottom * bmp.Height);
                Assert.True(regionRect.IntersectsWith(textRect));
            }
        });

        var regionBlocks = result.Regions.Where(r => r.Type != LayoutLabel.Table && r.Type != LayoutLabel.Image)
                                         .SelectMany(r => r.TextBlocks!)
                                         .ToList();
        Assert.Equal(regionBlocks.Count, regionBlocks.Distinct().Count());
        Assert.True(regionBlocks.Count <= result.Ocr.TextBlocks.Length);

        Assert.True(result.LayoutTimeMs > 0);
        Assert.True(result.OcrTimeMs > 0);
        long expectedTable = result.Regions.Where(r => r.Type == LayoutLabel.Table && r.Table != null)
                                           .Sum(r => r.Table!.TotalTimeMs);
        Assert.Equal(expectedTable, result.TableTimeMs);
    }

    [Fact]
    public void Fallbacks_to_table_when_layout_empty()
    {
        string path = Path.Combine(TrainingDir, "sample_invoice.png");
        using var bmp = DecodeColor(path);
        var options = new StructureOptions { LayoutScoreThreshold = 1.1f };
        var result = _structure.Analyze(bmp, options);
        var region = Assert.Single(result.Regions);
        Assert.Equal(LayoutLabel.Table, region.Type);
    }

    [Theory]
    [MemberData(nameof(AllImages))]
    public void Markdown_contains_tables_and_images_when_present(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string md = StructureMarkdownBuilder.Build(result, dir);

        Assert.False(string.IsNullOrWhiteSpace(md));
        if (result.Regions.Any(r => r.Type == LayoutLabel.Table))
        {
            Assert.Contains("<table", md);
            Assert.All(result.Regions.Where(r => r.Type == LayoutLabel.Table), r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Table!.Html));
            });
        }
        if (result.Regions.Any(r => r.Type == LayoutLabel.Image))
        {
            Assert.Contains("<img", md);
            var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "figure_*.png") : Array.Empty<string>();
            Assert.Equal(result.Regions.Count(r => r.Type == LayoutLabel.Image), files.Length);
            foreach (var f in files)
                Assert.Contains(Path.GetFileName(f), md);
        }
    }

    [Theory]
    [MemberData(nameof(AllImages))]
    public void Table_html_cell_count_matches_boxes(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);
        foreach (var table in result.Regions.Where(r => r.Type == LayoutLabel.Table))
        {
            int tdCount = Regex.Matches(table.Table!.Html, "<td", RegexOptions.IgnoreCase).Count;
            Assert.Equal(tdCount, table.Table!.CellBoxes.Count);
        }
    }

    [SkippableTheory]
    [MemberData(nameof(TrainingImages))]
    public void Pipeline_detects_table_like_ppstructure(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);

        var pythonTypes = RunPpstructure(path);
        Skip.If(pythonTypes is null || pythonTypes.Count == 0, "ppstructure not available");

        bool pythonHasTable = pythonTypes.Contains("table");
        if (pythonHasTable)
            Assert.Contains(result.Regions, r => r.Type == LayoutLabel.Table);
    }

    [SkippableTheory]
    [MemberData(nameof(TrainingImages))]
    public void Markdown_is_reasonably_close_to_ppstructure(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);
        string ours = StructureMarkdownBuilder.Build(result);

        var pythonMd = RunPpstructureMarkdown(path);
        Skip.If(pythonMd is null, "ppstructure not available");

        Assert.False(string.IsNullOrWhiteSpace(ours));
        Assert.False(string.IsNullOrWhiteSpace(pythonMd));

        if (pythonMd.Contains("<table"))
            Assert.Contains("<table", ours);
        if (pythonMd.Contains("![") || pythonMd.Contains("<img"))
            Assert.Contains("<img", ours);

        string oursSnippet = ours.ReplaceLineEndings("\n").Trim();
        string pythonSnippet = pythonMd.ReplaceLineEndings("\n").Trim();
        int len = Math.Min(80, Math.Min(oursSnippet.Length, pythonSnippet.Length));
        Assert.Contains(pythonSnippet[..len], oursSnippet);
    }

    [SkippableTheory]
    [MemberData(nameof(ValidationImages))]
    public void Pipeline_detects_table_like_ppstructure_validation(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);

        var pythonTypes = RunPpstructure(path);
        Skip.If(pythonTypes is null || pythonTypes.Count == 0, "ppstructure not available");

        bool pythonHasTable = pythonTypes.Contains("table");
        if (pythonHasTable)
            Assert.Contains(result.Regions, r => r.Type == LayoutLabel.Table);
    }

    [SkippableTheory]
    [MemberData(nameof(ValidationImages))]
    public void Markdown_is_reasonably_close_to_ppstructure_validation(string path)
    {
        using var bmp = DecodeColor(path);
        var result = _structure.Analyze(bmp);
        string ours = StructureMarkdownBuilder.Build(result);

        var pythonMd = RunPpstructureMarkdown(path);
        Skip.If(pythonMd is null, "ppstructure not available");

        Assert.False(string.IsNullOrWhiteSpace(ours));
        Assert.False(string.IsNullOrWhiteSpace(pythonMd));

        if (pythonMd.Contains("<table"))
            Assert.Contains("<table", ours);
        if (pythonMd.Contains("![") || pythonMd.Contains("<img"))
            Assert.Contains("<img", ours);

        string oursSnippet = ours.ReplaceLineEndings("\n").Trim();
        string pythonSnippet = pythonMd.ReplaceLineEndings("\n").Trim();
        int len = Math.Min(120, Math.Min(oursSnippet.Length, pythonSnippet.Length));
        Assert.StartsWith(pythonSnippet[..len], oursSnippet);
    }

    private static Process? _ppServer = StartPpstructureServer();

    private static Process? StartPpstructureServer()
    {
        string script = """
import json,sys,os,tempfile,subprocess
repo='/tmp/PaddleOCR'
if not os.path.exists(repo):
    subprocess.run(['git','clone','--depth','1','https://github.com/PaddlePaddle/PaddleOCR',repo], check=True)
sys.path.insert(0, repo)
from paddleocr import PPStructureV3
pipeline=PPStructureV3(lang='en', use_chart_recognition=False, use_formula_recognition=False)
for line in sys.stdin:
    path=line.strip()
    if not path:
        continue
    if path=='__quit__':
        break
    res=pipeline.predict(path)
    types=[box['label'] for box in res[0]['layout_det_res']['boxes']]
    tmp=tempfile.mkdtemp()
    res[0].save_to_markdown(tmp)
    md_path=os.path.join(tmp, os.path.splitext(os.path.basename(path))[0] + '.md')
    with open(md_path,'r',encoding='utf-8') as f:
        md=f.read()
    print(json.dumps({'types':types,'markdown':md}), flush=True)
""";

        string tmp = Path.GetTempFileName() + ".py";
        File.WriteAllText(tmp, script);
        var psi = new ProcessStartInfo("python", $"\"{tmp}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        return Process.Start(psi);
    }

    private static (List<string>? types, string? markdown) QueryPpstructure(string imagePath)
    {
        try
        {
            if (_ppServer == null || _ppServer.HasExited)
                _ppServer = StartPpstructureServer();
            var p = _ppServer;
            if (p == null) return (null, null);
            lock (p)
            {
                p.StandardInput.WriteLine(imagePath);
                p.StandardInput.Flush();
                string? line = p.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) return (null, null);
                var doc = JsonDocument.Parse(line);
                var types = new List<string>();
                foreach (var el in doc.RootElement.GetProperty("types").EnumerateArray())
                    types.Add(el.GetString()!);
                string markdown = doc.RootElement.GetProperty("markdown").GetString()!;
                return (types, markdown);
            }
        }
        catch
        {
            return (null, null);
        }
    }

    private static List<string>? RunPpstructure(string imagePath)
    {
        var (types, _) = QueryPpstructure(imagePath);
        return types;
    }

    private static string? RunPpstructureMarkdown(string imagePath)
    {
        var (_, md) = QueryPpstructure(imagePath);
        return md;
    }
}

