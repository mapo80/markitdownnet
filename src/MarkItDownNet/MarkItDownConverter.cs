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
        var pages = new List<Page>();
        var lines = new List<Line>();
        var words = new List<Word>();

        int pageNum = 0;
        foreach (var pix in Rasterizer.FromPdf(path, _options.OcrDpi))
        {
            using (pix)
            {
                ct.ThrowIfCancellationRequested();
                pageNum++;
                pages.Add(new Page(pageNum, pix.Width, pix.Height));
                var result = ProcessPix(pix, pageNum, ct);
                lines.AddRange(result.lines);
                words.AddRange(result.words);
            }
        }

        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private MarkItDownResult ProcessImage(string path, CancellationToken ct)
    {
        using var pix = Rasterizer.FromImage(path, _options.OcrDpi);
        var (lines, words) = ProcessPix(pix, 1, ct);
        var pages = new List<Page> { new Page(1, pix.Width, pix.Height) };
        var markdown = BuildMarkdown(lines);
        return new MarkItDownResult(markdown, pages, lines, words);
    }

    private (List<Line> lines, List<Word> words) ProcessPix(Pix pix, int pageNumber, CancellationToken ct)
    {
        var lines = new List<Line>();
        var words = new List<Word>();
        Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT", _options.OcrThreads.ToString());
        using var engine = new TesseractEngine(
            _options.OcrDataPath ?? string.Empty,
            _options.OcrLanguages,
            _options.OcrOem);
        engine.DefaultPageSegMode = (PageSegMode)_options.OcrPsm;
        engine.SetVariable("preserve_interword_spaces", "1");
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

    private static BoundingBox Normalize(Rect rect, int width, int height)
    {
        return new BoundingBox((double)rect.X1 / width, (double)rect.Y1 / height, (double)rect.Width / width, (double)rect.Height / height);
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
