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

        // If forced or there are not enough words, fall back to OCR
        if (_options.OcrForceRaster || words.Count < _options.MinimumNativeWordThreshold)
        {
            _logger.Information("Native text too small ({Count}), attempting OCR fallback", words.Count);
            return ProcessPdfWithOcr(path, ct);
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words, null);
    }

    private MarkItDownResult ProcessPdfWithOcr(string path, CancellationToken ct)
    {
        var pages = new List<Page>();
        var lines = new List<Line>();
        var words = new List<Word>();
        OcrMetadata? meta = null;

        // Rasterize PDF into images using PDFtoImage
        var renderOptions = new RenderOptions { Dpi = _options.OcrUserDpi };
        using var stream = File.OpenRead(path);
        foreach (var bitmap in Conversion.ToImages(stream, leaveOpen: false, password: null, renderOptions))
        {
            ct.ThrowIfCancellationRequested();
            using (bitmap)
            {
                pages.Add(new Page(pages.Count + 1, bitmap.Width, bitmap.Height));
                var prep = PreparePix(bitmap);
                meta ??= prep.meta;
                var result = ProcessPix(prep.pix, pages.Count, ct);
                prep.pix.Dispose();
                lines.AddRange(result.lines);
                words.AddRange(result.words);
            }
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words, meta);
    }

    private MarkItDownResult ProcessImage(string path, CancellationToken ct)
    {
        var prep = PreparePix(path);
        var pages = new List<Page> { new Page(1, prep.pix.Width, prep.pix.Height) };
        var (lines, words) = ProcessPix(prep.pix, 1, ct);
        prep.pix.Dispose();
        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words, prep.meta);
    }

    private (List<Line> lines, List<Word> words) ProcessPix(Pix pix, int pageNumber, CancellationToken ct)
    {
        var lines = new List<Line>();
        var words = new List<Word>();
        using var engine = new TesseractEngine(
            _options.OcrDataPath ?? string.Empty,
            _options.OcrLanguages,
            _options.OcrOem);
        engine.DefaultPageSegMode = (PageSegMode)_options.OcrPsm;
        engine.SetVariable("OMP_THREAD_LIMIT", _options.OcrThreads.ToString());
        using var page = engine.Process(pix);
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

        return (lines, words);
    }

    private (Pix pix, OcrMetadata meta) PreparePix(string path)
    {
        var src = Pix.LoadFromFile(path);
        return PreparePix(src);
    }

    private (Pix pix, OcrMetadata meta) PreparePix(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var pix = Pix.LoadFromMemory(data.ToArray());
        return PreparePix(pix);
    }

    private (Pix pix, OcrMetadata meta) PreparePix(Pix pix)
    {
        var working = pix;

        if (working.Depth == 32)
        {
            var gray = working.ConvertRGBToGray();
            working.Dispose();
            working = gray;
        }
        else if (working.Depth != 8)
        {
            var eight = working.ConvertTo8(0);
            working.Dispose();
            working = eight;
        }

        var dpi = working.XRes > 0 ? working.XRes : _options.OcrUserDpi;
        if (dpi < 220)
        {
            var scale = (float)_options.OcrUserDpi / dpi;
            var scaled = working.Scale(scale, scale);
            working.Dispose();
            working = scaled;
            dpi = _options.OcrUserDpi;
        }

        if (_options.OcrSetDpiMetadata)
        {
            working.XRes = _options.OcrUserDpi;
            working.YRes = _options.OcrUserDpi;
        }

        double? angle = null;
        var deskewed = working.Deskew(out var scew);
        if (Math.Abs(scew.Angle) >= _options.OcrDeskewMinAngleDeg)
        {
            working.Dispose();
            working = deskewed;
            angle = scew.Angle;
        }
        else
        {
            deskewed.Dispose();
        }

        var meta = new OcrMetadata(dpi, _options.OcrColorDepth, angle, _options.OcrPsm, _options.OcrOem);
        return (working, meta);
    }

    private static BoundingBox Normalize(Rect rect, int width, int height)
    {
        return new BoundingBox((double)rect.X1 / width, (double)rect.Y1 / height, (double)rect.Width / width, (double)rect.Height / height);
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
