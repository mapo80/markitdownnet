namespace MarkItDownNet;

using Tesseract;

public enum OcrColorDepth
{
    Grayscale8bpp,
    Bgra32bpp
}

/// <summary>Runtime options for conversion.</summary>
public class MarkItDownOptions
{
    /// <summary>Path to Tesseract language data (TESSDATA_PREFIX).</summary>
    public string? OcrDataPath { get; set; }

    /// <summary>Languages for OCR, e.g. "eng" or "ita+eng".</summary>
    public string OcrLanguages { get; set; } = "eng";

    /// <summary>User-specified DPI for OCR rasterization.</summary>
    public int OcrUserDpi { get; set; } = 300;

    /// <summary>Page segmentation mode.</summary>
    public int OcrPsm { get; set; } = 6;

    /// <summary>OCR engine mode.</summary>
    public EngineMode OcrOem { get; set; } = EngineMode.LstmOnly;

    /// <summary>Maximum number of OCR threads.</summary>
    public int OcrThreads { get; set; } = 1;

    /// <summary>Force rasterization even for digital PDFs.</summary>
    public bool OcrForceRaster { get; set; } = true;

    /// <summary>Apply Otsu binarization before Tesseract.</summary>
    public bool OcrPreBinarize { get; set; } = false;

    /// <summary>Deskew only if |angle| exceeds this threshold.</summary>
    public double OcrDeskewMinAngleDeg { get; set; } = 2.0;

    /// <summary>Color depth for images passed to Tesseract.</summary>
    public OcrColorDepth OcrColorDepth { get; set; } = OcrColorDepth.Grayscale8bpp;

    /// <summary>Minimum number of native words required before falling back to OCR.</summary>
    public int MinimumNativeWordThreshold { get; set; } = 1;

    /// <summary>Normalize markdown output using Markdig.</summary>
    public bool NormalizeMarkdown { get; set; } = true;

    /// <summary>
    /// Normalized vertical gap threshold above which a blank line is inserted
    /// to separate paragraphs in the generated markdown.
    /// </summary>
    public double ParagraphGapThreshold { get; set; } = 0.012;

    /// <summary>
    /// Detect bullet or numbered list items and emit proper Markdown list syntax.
    /// </summary>
    public bool DetectBulletLists { get; set; } = true;

    /// <summary>
    /// Merge consecutive lines into paragraphs instead of preserving line breaks.
    /// </summary>
    public bool MergeLines { get; set; } = true;
}
