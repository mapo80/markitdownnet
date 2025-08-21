namespace MarkItDownNet;

using Tesseract;

public enum OcrColorDepth
{
    Grayscale8bpp,
    Color32bpp
}

/// <summary>Runtime options for conversion.</summary>
public class MarkItDownOptions
{
    /// <summary>Path to Tesseract language data (TESSDATA_PREFIX).</summary>
    public string? OcrDataPath { get; set; }

    /// <summary>Languages for OCR, e.g. "eng" or "ita+eng".</summary>
    public string OcrLanguages { get; set; } = "eng";

    /// <summary>User provided DPI metadata and rendering target.</summary>
    public int OcrUserDpi { get; set; } = 300;

    /// <summary>Tesseract page segmentation mode (PSM).</summary>
    public int OcrPsm { get; set; } = 6;

    /// <summary>Tesseract engine mode (OEM).</summary>
    public EngineMode OcrOem { get; set; } = EngineMode.LstmOnly;

    /// <summary>Number of threads to use for Tesseract OCR.</summary>
    public int OcrThreads { get; set; } = 1;

    /// <summary>Force PDF rasterization even when native text is available.</summary>
    public bool OcrForceRaster { get; set; } = true;

    /// <summary>Apply binarization before OCR.</summary>
    public bool OcrPreBinarize { get; set; } = false;

    /// <summary>Minimum deskew angle in degrees to trigger rotation.</summary>
    public double OcrDeskewMinAngleDeg { get; set; } = 2.0;

    /// <summary>Color depth for OCR input.</summary>
    public OcrColorDepth OcrColorDepth { get; set; } = OcrColorDepth.Grayscale8bpp;

    /// <summary>Set DPI metadata on images passed to Tesseract.</summary>
    public bool OcrSetDpiMetadata { get; set; } = true;

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
