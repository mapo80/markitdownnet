using System.Collections.Generic;
using System.Diagnostics;
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

    public RapidStructure(LayoutDetector layoutDetector, IOcrEngine ocr, TableRecognizer tableRecognizer)
    {
        _layoutDetector = layoutDetector;
        _ocr = ocr;
        _tableRecognizer = tableRecognizer;
    }

    /// <summary>
    /// Analyse a page image and return detected regions along with the full OCR result.
    /// Orientation detection is wired but not yet implemented; the returned orientation
    /// angle will always be zero for now.
    /// </summary>
    public StructureResult Analyze(SKBitmap image, StructureOptions? structureOptions = null, RapidOcrOptions? ocrOptions = null)
    {
        structureOptions ??= new StructureOptions();
        ocrOptions ??= RapidOcrOptions.Default;
        // Disable angle classification until orientation handling is implemented
        ocrOptions = ocrOptions with { DoAngle = false };

        float orientation = 0f; // placeholder – orientation detection not yet implemented
        SKBitmap working = image; // no rotation applied

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

        var regions = new List<StructureRegion>(layout.Count);
        long tableTime = 0;
        foreach (var box in layout)
        {
            var rect = new SKRect(box.X1, box.Y1, box.X2, box.Y2);
            var norm = new SKRect(rect.Left / working.Width, rect.Top / working.Height,
                                  rect.Right / working.Width, rect.Bottom / working.Height);

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
                textBlocks = FilterBlocks(ocrResult.TextBlocks, rect);
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
}

