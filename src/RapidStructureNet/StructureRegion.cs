using RapidLayoutNet;
using RapidOcrNet;
using RapidTableNet;
using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// Represents a layout region detected on a page.
/// </summary>
public sealed class StructureRegion : IDisposable
{
    /// <summary>The region type coming from layout detection.</summary>
    public LayoutLabel Type { get; }

    /// <summary>Bounding box normalised to the range [0,1].</summary>
    public SKRect BBox { get; }

    /// <summary>Confidence score in [0,1].</summary>
    public float Score { get; }

    /// <summary>Page number starting at 0.</summary>
    public int PageIndex { get; }

    /// <summary>Cropped image of the region, when available.</summary>
    public SKBitmap? Image { get; }

    /// <summary>Detailed table result for table regions.</summary>
    public TableResult? Table { get; }

    /// <summary>OCR text blocks intersecting this region (only for non-table regions).</summary>
    public IReadOnlyList<TextBlock>? TextBlocks { get; }

    public StructureRegion(
        LayoutLabel type,
        SKRect bbox,
        float score,
        int pageIndex,
        SKBitmap? image = null,
        TableResult? table = null,
        IReadOnlyList<TextBlock>? textBlocks = null)
    {
        Type = type;
        BBox = bbox;
        Score = score;
        PageIndex = pageIndex;
        Image = image;
        Table = table;
        TextBlocks = textBlocks;
    }

    public void Dispose()
    {
        Image?.Dispose();
    }
}

