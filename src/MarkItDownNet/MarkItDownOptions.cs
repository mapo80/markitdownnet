namespace MarkItDownNet;

using RapidOcrNet;
using Tesseract;

/// <summary>Runtime options for conversion.</summary>
public class MarkItDownOptions
{
    /// <summary>OCR engine to use.</summary>
    public OcrEngine OcrEngine { get; set; } = OcrEngine.Tesseract;

    /// <summary>Path to Tesseract language data (TESSDATA_PREFIX).</summary>
    public string? OcrDataPath { get; set; }

    /// <summary>Language for OCR.</summary>
    public OcrLanguage OcrLanguage { get; set; } = OcrLanguage.English;

    /// <summary>Version of the RapidOCR ONNX models.</summary>
    public OcrVersion OcrModelVersion { get; set; } = OcrVersion.V5;

    /// <summary>Page segmentation mode used by Tesseract.</summary>
    public PageSegMode PageSegMode { get; set; } = PageSegMode.SingleBlock;

    /// <summary>DPI used when rasterizing PDFs for OCR fallback.</summary>
    public int PdfRasterDpi { get; set; } = 300;

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
