using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Serilog;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PDFtoImage;
using SkiaSharp;

namespace MarkItDownNet;

/// <summary>Main entry point for converting documents to markdown and bounding boxes.</summary>
public class MarkItDownConverter
{
    private readonly MarkItDownOptions _options;
    private readonly ILogger _logger;

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
        foreach (var bitmap in Conversion.ToImages(stream, leaveOpen: false, password: null, renderOptions))
        {
            ct.ThrowIfCancellationRequested();
            using (bitmap)
            {
                pages.Add(new Page(pages.Count + 1, bitmap.Width, bitmap.Height));
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var pix = Pix.LoadFromMemory(data.ToArray());
                var result = ProcessPix(pix, pages.Count, ct);
                lines.AddRange(result.lines);
                words.AddRange(result.words);
            }
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private MarkItDownResult ProcessImage(string path, CancellationToken ct)
    {
        using var pix = Pix.LoadFromFile(path);
        var (lines, words) = ProcessPix(pix, 1, ct);
        var pages = new List<Page> { new Page(1, pix.Width, pix.Height) };
        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private (List<Line> lines, List<Word> words) ProcessPix(Pix pix, int pageNumber, CancellationToken ct)
    {
        var lines = new List<Line>();
        var words = new List<Word>();
        using var engine = new TesseractEngine(_options.OcrDataPath ?? string.Empty, _options.OcrLanguages, EngineMode.Default);
        using var page = engine.Process(pix);
        var iterator = page.GetIterator();

        iterator.Begin();
        do
        {
            ct.ThrowIfCancellationRequested();
            if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
            {
                var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    lines.Add(new Line(pageNumber, text, Normalize(rect, pix.Width, pix.Height)));
                }
            }
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        iterator.Begin();
        do
        {
            ct.ThrowIfCancellationRequested();
            if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
            {
                var text = iterator.GetText(PageIteratorLevel.Word)?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    words.Add(new Word(pageNumber, text, Normalize(rect, pix.Width, pix.Height)));
                }
            }
        }
        while (iterator.Next(PageIteratorLevel.Word));

        return (lines, words);
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
        var raw = string.Join("\n", lines.Select(l => l.Text));
        if (_options.NormalizeMarkdown)
        {
            return Markdown.Normalize(raw);
        }
        return raw;
    }
}
