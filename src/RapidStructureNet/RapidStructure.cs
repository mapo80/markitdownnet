using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RapidLayoutNet;
using RapidOcrNet;
using RapidTableNet;
using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// High level orchestrator combining layout detection, OCR and table recognition
/// similar to PaddleOCR's PP-Structure pipeline.
/// </summary>
public sealed class RapidStructure
{
    private readonly LayoutDetector _layoutDetector;
    private readonly IOcrEngine _ocr;
    private readonly TableRecognizer _tableRecognizer;
    private readonly IOrientationDetector? _orientationDetector;

    public RapidStructure(LayoutDetector layoutDetector, IOcrEngine ocr, TableRecognizer tableRecognizer, IOrientationDetector? orientationDetector = null)
    {
        _layoutDetector = layoutDetector;
        _ocr = ocr;
        _tableRecognizer = tableRecognizer;
        _orientationDetector = orientationDetector;
    }

    /// <summary>
    /// Analyse a page image and return detected regions along with the full OCR result.
    /// Orientation detection can be enabled via <see cref="StructureOptions.DetectOrientation"/>.
    /// </summary>
    public StructureResult Analyze(SKBitmap image, StructureOptions? structureOptions = null, RapidOcrOptions? ocrOptions = null)
    {
        structureOptions ??= new StructureOptions();
        ocrOptions ??= RapidOcrOptions.Default;
        ocrOptions = ocrOptions with { DoAngle = structureOptions.DetectOrientation };

        float orientation = 0f;
        SKBitmap working = image;
        if (structureOptions.DetectOrientation && _orientationDetector != null)
        {
            orientation = _orientationDetector.Detect(image);
            if (orientation % 360 != 0)
            {
                working = Rotate(image, orientation);
            }
        }

        // Detect layout regions
        var sw = Stopwatch.StartNew();
        var layout = _layoutDetector.Detect(working, structureOptions.LayoutScoreThreshold);
        long layoutTime = sw.ElapsedMilliseconds;
        if (layout.Count == 0)
        {
            // Fallback to a single table region covering the whole page
            layout = new List<LayoutBox>
            {
                new(LayoutLabel.Table, 1f, 0, 0, working.Width, working.Height)
            };
        }

        // Run full page OCR once
        sw.Restart();
        var ocrResult = _ocr.Detect(working, ocrOptions);
        long ocrTime = sw.ElapsedMilliseconds;
        if (orientation % 360 != 0)
        {
            ocrResult = RotateOcrResult(ocrResult, orientation, image.Width, image.Height);
        }

        var regions = new List<StructureRegion>(layout.Count);
        long tableTime = 0;
        foreach (var box in layout)
        {
            var rect = new SKRect(box.X1, box.Y1, box.X2, box.Y2);
            var origRect = orientation % 360 != 0 ? RotateRectBack(rect, orientation, image.Width, image.Height) : rect;
            var norm = new SKRect(origRect.Left / image.Width, origRect.Top / image.Height,
                                  origRect.Right / image.Width, origRect.Bottom / image.Height);

            SKBitmap? subset = null;
            TableResult? table = null;
            IReadOnlyList<TextBlock>? textBlocks = null;
            bool needsImage = box.Label == LayoutLabel.Table || box.Label == LayoutLabel.Image;
            if (needsImage)
            {
                var rectI = SKRectI.Round(rect);
                subset = new SKBitmap(rectI.Width, rectI.Height);
                working.ExtractSubset(subset, rectI);
                if (box.Label == LayoutLabel.Table)
                {
                    table = _tableRecognizer.Detect(subset);
                    tableTime += table.TotalTimeMs;
                }
            }
            else
            {
                textBlocks = FilterBlocks(ocrResult.TextBlocks, origRect);
            }

            regions.Add(new StructureRegion(box.Label, norm, box.Score, 0, subset, table, textBlocks));
        }

        return new StructureResult(regions, ocrResult, orientation, layoutTime, ocrTime, tableTime);
    }

    private static List<TextBlock> FilterBlocks(TextBlock[] blocks, SKRect region)
    {
        var filtered = new List<TextBlock>();
        foreach (var b in blocks)
        {
            var pts = b.BoxPoints;
            float minX = pts[0].X, maxX = pts[0].X, minY = pts[0].Y, maxY = pts[0].Y;
            for (int i = 1; i < pts.Length; i++)
            {
                var p = pts[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            var textRect = new SKRect(minX, minY, maxX, maxY);
            if (region.IntersectsWith(textRect))
                filtered.Add(b);
        }
        return filtered;
    }

    private static OcrResult RotateOcrResult(OcrResult src, float angle, int width, int height)
    {
        var blocks = new TextBlock[src.TextBlocks.Length];
        for (int i = 0; i < src.TextBlocks.Length; i++)
        {
            var b = src.TextBlocks[i];
            var pts = new SKPointI[b.BoxPoints.Length];
            for (int j = 0; j < b.BoxPoints.Length; j++)
            {
                var p = RotatePointBack(b.BoxPoints[j], angle, width, height);
                pts[j] = new SKPointI((int)System.Math.Round(p.X), (int)System.Math.Round(p.Y));
            }
            blocks[i] = new TextBlock
            {
                BoxPoints = pts,
                BoxScore = b.BoxScore,
                AngleIndex = b.AngleIndex,
                AngleScore = b.AngleScore,
                AngleTime = b.AngleTime,
                Chars = b.Chars,
                CharScores = b.CharScores,
                CrnnTime = b.CrnnTime,
                BlockTime = b.BlockTime
            };
        }
        return new OcrResult
        {
            TextBlocks = blocks,
            DbNetTime = src.DbNetTime,
            DetectTime = src.DetectTime,
            StrRes = src.StrRes
        };
    }

    private static SKRect RotateRectBack(SKRect rect, float angle, int width, int height)
    {
        if (angle % 360 == 0) return rect;
        var pts = new[]
        {
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Right, rect.Top),
            new SKPoint(rect.Right, rect.Bottom),
            new SKPoint(rect.Left, rect.Bottom)
        };
        var rotated = pts.Select(p => RotatePointBack(p, angle, width, height)).ToArray();
        float minX = rotated.Min(p => p.X);
        float maxX = rotated.Max(p => p.X);
        float minY = rotated.Min(p => p.Y);
        float maxY = rotated.Max(p => p.Y);
        return new SKRect(minX, minY, maxX, maxY);
    }

    private static SKPoint RotatePointBack(SKPoint p, float angle, int width, int height)
    {
        angle %= 360;
        return angle switch
        {
            90 => new SKPoint(p.Y, height - p.X),
            180 => new SKPoint(width - p.X, height - p.Y),
            270 => new SKPoint(width - p.Y, p.X),
            _ => p
        };
    }

    private static SKBitmap Rotate(SKBitmap src, float angle)
    {
        angle %= 360;
        if (angle == 0) return src;
        SKBitmap dst;
        if (angle == 90 || angle == 270)
            dst = new SKBitmap(src.Height, src.Width);
        else
            dst = new SKBitmap(src.Width, src.Height);

        using var canvas = new SKCanvas(dst);
        canvas.Translate(dst.Width / 2f, dst.Height / 2f);
        canvas.RotateDegrees(angle);
        canvas.Translate(-src.Width / 2f, -src.Height / 2f);
        canvas.DrawBitmap(src, 0, 0);
        canvas.Flush();
        return dst;
    }

    // Bounding boxes are mapped back to the original image coordinate space.
}
