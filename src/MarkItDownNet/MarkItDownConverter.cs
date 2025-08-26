using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Serilog;
using RapidOcrNet;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PDFtoImage;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace MarkItDownNet;

/// <summary>Main entry point for converting documents to markdown and bounding boxes.</summary>
public class MarkItDownConverter
{
    private readonly MarkItDownOptions _options;
    private readonly ILogger _logger;
    private static readonly Regex BulletRegex = new(@"^\s*(?:[-*•·]|\d+[.)])\s+");

    static MarkItDownConverter()
    {
        var baseDir = AppContext.BaseDirectory;
        TesseractEnviornment.CustomSearchPath = baseDir;
        var x64Dir = Path.Combine(baseDir, "x64");
        var nativeDir = Path.Combine(baseDir, "runtimes", "linux-x64", "native");
        try { NativeLibrary.Load(Path.Combine(x64Dir, "libopenjp2.so.7")); } catch { }
    }

    public MarkItDownConverter(MarkItDownOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new MarkItDownOptions();
        _logger = logger ?? Log.Logger;
    }

    /// <summary>Convert the input file based on the provided mime type.</summary>
    public async Task<MarkItDownResult> ConvertAsync(string path, string mimeType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required", nameof(path));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return mimeType switch
        {
            "application/pdf" => await Task.Run(() => ProcessPdf(path, cancellationToken), cancellationToken),
            var m when m.StartsWith("image/") => await Task.Run(() => ProcessImage(path, cancellationToken), cancellationToken),
            _ => throw new NotSupportedException($"Unsupported mime type '{mimeType}'.")
        };
    }

    private MarkItDownResult ProcessPdf(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var document = PdfDocument.Open(stream);
        var pages = new List<Page>();
        var lines = new List<Line>();
        var words = new List<Word>();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            pages.Add(new Page(page.Number, page.Width, page.Height));

            var pageWords = page.GetWords()
                .Select(w => new Word(page.Number, w.Text, BoundingBox.FromPdf(w.BoundingBox, page.Width, page.Height)))
                .ToList();

            words.AddRange(pageWords);

            foreach (var lineWords in GroupWordsIntoLines(pageWords))
            {
                var text = string.Join(" ", lineWords.Select(w => w.Text));
                var union = Union(lineWords.Select(w => w.BBox));
                lines.Add(new Line(page.Number, text, union));
            }
        }

        // If there are not enough words, fall back to OCR
        if (words.Count < _options.MinimumNativeWordThreshold)
        {
            _logger.Information("Native text too small ({Count}), attempting OCR fallback", words.Count);
            return ProcessPdfWithOcr(path, ct);
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private MarkItDownResult ProcessPdfWithOcr(string path, CancellationToken ct)
    {
        var pages = new List<Page>();
        var lines = new List<Line>();
        var words = new List<Word>();

        // Rasterize PDF into images using PDFtoImage
        var renderOptions = new RenderOptions { Dpi = _options.PdfRasterDpi };
        using var stream = File.OpenRead(path);
#pragma warning disable CA1416
        foreach (var bitmap in Conversion.ToImages(stream, leaveOpen: false, password: null, renderOptions))
#pragma warning restore CA1416
        {
            ct.ThrowIfCancellationRequested();
            using (bitmap)
            {
                pages.Add(new Page(pages.Count + 1, bitmap.Width, bitmap.Height));
                switch (_options.OcrEngine)
                {
                    case OcrEngine.RapidOcr:
                    {
                        var rapidResult = ProcessBitmapWithRapidOcr(bitmap, pages.Count, ct);
                        lines.AddRange(rapidResult.lines);
                        words.AddRange(rapidResult.words);
                        break;
                    }
                    case OcrEngine.Tesseract:
                    default:
                    {
                        using var image = SKImage.FromBitmap(bitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var pix = Pix.LoadFromMemory(data.ToArray());
                        var tessResult = ProcessPixWithTesseract(pix, pages.Count, ct);
                        lines.AddRange(tessResult.lines);
                        words.AddRange(tessResult.words);
                        break;
                    }
                }
            }
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private MarkItDownResult ProcessImage(string path, CancellationToken ct)
    {
        switch (_options.OcrEngine)
        {
            case OcrEngine.RapidOcr:
                using (var bitmap = SKBitmap.Decode(path))
                {
                    var (lines, words) = ProcessBitmapWithRapidOcr(bitmap, 1, ct);
                    var pages = new List<Page> { new Page(1, bitmap.Width, bitmap.Height) };
                    var markdown = BuildMarkdown(lines);
                    return new MarkItDownResult(markdown, pages, lines, words);
                }
            case OcrEngine.Tesseract:
            default:
                using (var pix = Pix.LoadFromFile(path))
                {
                    var (lines, words) = ProcessPixWithTesseract(pix, 1, ct);
                    var pages = new List<Page> { new Page(1, pix.Width, pix.Height) };
                    var markdown = BuildMarkdown(lines);
                    return new MarkItDownResult(markdown, pages, lines, words);
                }
        }
    }

    private (List<Line> lines, List<Word> words) ProcessPixWithTesseract(Pix pix, int pageNumber, CancellationToken ct)
    {
        var lines = new List<Line>();
        var words = new List<Word>();

        var depth = pix.Depth;
        var converted = 0;
        Pix pix8 = pix;
        if (depth != 8)
        {
            pix8 = pix.ConvertTo8(0);
            converted = 1;
        }
        pix8.XRes = 300;
        pix8.YRes = 300;
        _logger.Information("pix.depth={Depth} converted={Converted} xdpi={Xdpi} ydpi={Ydpi}", depth, converted, pix8.XRes, pix8.YRes);

        var tessLang = _options.OcrLanguage == OcrLanguage.Italian ? "ita" : "eng";
        using var engine = new TesseractEngine(
            _options.OcrDataPath ?? string.Empty,
            tessLang,
            EngineMode.LstmOnly);
        engine.SetVariable("user_defined_dpi", "300");
        engine.SetVariable("preserve_interword_spaces", "1");
        engine.DefaultPageSegMode = _options.PageSegMode;
        _logger.Information("psm=6 oem=1 user_defined_dpi=300 preserve_spaces=1");
        using var page = engine.Process(pix8);
        using var iter = page.GetIterator();
        iter.Begin();
        do
        {
            ct.ThrowIfCancellationRequested();

            if (iter.IsAtBeginningOf(PageIteratorLevel.TextLine) &&
                iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rectLine))
            {
                var text = iter.GetText(PageIteratorLevel.TextLine)?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    lines.Add(new Line(pageNumber, text, Normalize(rectLine, pix.Width, pix.Height)));
                }
            }

            if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rectWord))
            {
                var wText = iter.GetText(PageIteratorLevel.Word)?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(wText))
                {
                    words.Add(new Word(pageNumber, wText, Normalize(rectWord, pix.Width, pix.Height)));
                }
            }
        } while (iter.Next(PageIteratorLevel.Word));

        if (converted == 1) pix8.Dispose();

        return (lines, words);
    }

    private (List<Line> lines, List<Word> words) ProcessBitmapWithRapidOcr(SKBitmap bitmap, int pageNumber, CancellationToken ct)
    {
        var lines = new List<Line>();
        var words = new List<Word>();

        using var ocr = new RapidOcr();
        ocr.InitModels(_options.OcrLanguage, 0);

        try
        {
            var result = ocr.Detect(bitmap, RapidOcrOptions.Default);
            if (result?.TextBlocks == null)
            {
                _logger.Warning("RapidOCR returned no text blocks");
                return (lines, words);
            }

            foreach (var block in result.TextBlocks)
            {
                ct.ThrowIfCancellationRequested();
                var text = block.GetText();
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var bbox = Normalize(block.BoxPoints, bitmap.Width, bitmap.Height);
                lines.Add(new Line(pageNumber, text, bbox));
                words.Add(new Word(pageNumber, text, bbox));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "RapidOCR detection failed");
        }

        return (lines, words);
    }

    private static BoundingBox Normalize(Rect rect, int width, int height)
    {
        return new BoundingBox((double)rect.X1 / width, (double)rect.Y1 / height, (double)rect.Width / width, (double)rect.Height / height);
    }

    private static BoundingBox Normalize(SKPointI[] points, int width, int height)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        return new BoundingBox((double)minX / width, (double)minY / height,
            (double)(maxX - minX) / width, (double)(maxY - minY) / height);
    }

    private static IEnumerable<IEnumerable<Word>> GroupWordsIntoLines(IReadOnlyList<Word> words)
    {
        const double tolerance = 0.02; // normalized units
        var result = new List<List<Word>>();
        var sorted = words.OrderBy(w => w.BBox.Y).ThenBy(w => w.BBox.X).ToList();

        var current = new List<Word>();
        double? currentTop = null;
        foreach (var w in sorted)
        {
            if (currentTop == null || Math.Abs(w.BBox.Y - currentTop.Value) <= tolerance)
            {
                currentTop = w.BBox.Y;
                current.Add(w);
            }
            else
            {
                result.Add(current);
                current = new List<Word> { w };
                currentTop = w.BBox.Y;
            }
        }
        if (current.Count > 0)
        {
            result.Add(current);
        }

        return result;
    }

    private static BoundingBox Union(IEnumerable<BoundingBox> rects)
    {
        var left = rects.Min(r => r.X);
        var top = rects.Min(r => r.Y);
        var right = rects.Max(r => r.X + r.Width);
        var bottom = rects.Max(r => r.Y + r.Height);
        return new BoundingBox(left, top, right - left, bottom - top);
    }

    private string BuildMarkdown(IEnumerable<Line> lines)
    {
        var ordered = lines
            .OrderBy(l => l.Page)
            .ThenBy(l => l.BBox.Y)
            .ToList();

        var sb = new StringBuilder();
        Line? prev = null;
        var prevBullet = false;
        foreach (var line in ordered)
        {
            var text = line.Text;
            var isBullet = false;
            if (_options.DetectBulletLists)
            {
                var match = BulletRegex.Match(text);
                if (match.Success)
                {
                    text = text.Substring(match.Length).TrimStart();
                    isBullet = true;
                }
            }

            if (prev != null)
            {
                var gap = line.BBox.Y - (prev.BBox.Y + prev.BBox.Height);
                if (isBullet)
                {
                    sb.AppendLine();
                    if (!prevBullet)
                    {
                        sb.AppendLine();
                    }
                }
                else if (prevBullet)
                {
                    sb.AppendLine().AppendLine();
                }
                else if (gap > _options.ParagraphGapThreshold)
                {
                    sb.AppendLine().AppendLine();
                }
                else if (_options.MergeLines)
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.AppendLine();
                }
            }

            if (isBullet)
            {
                sb.Append("- ").Append(text);
            }
            else
            {
                sb.Append(text);
            }

            prev = line;
            prevBullet = isBullet;
        }

        var raw = sb.ToString();
        return _options.NormalizeMarkdown ? Markdown.Normalize(raw) : raw;
    }
}
