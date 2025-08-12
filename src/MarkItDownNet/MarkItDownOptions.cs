namespace MarkItDownNet;

/// <summary>Runtime options for conversion.</summary>
public class MarkItDownOptions
{
    /// <summary>Path to Tesseract language data (TESSDATA_PREFIX).</summary>
    public string? OcrDataPath { get; set; }

    /// <summary>Languages for OCR, e.g. "eng" or "ita+eng".</summary>
    public string OcrLanguages { get; set; } = "eng";

    /// <summary>DPI used when rasterizing PDFs for OCR fallback.</summary>
    public int PdfRasterDpi { get; set; } = 300;

    /// <summary>Minimum number of native words required before falling back to OCR.</summary>
    public int MinimumNativeWordThreshold { get; set; } = 1;

    /// <summary>Normalize markdown output using Markdig.</summary>
    public bool NormalizeMarkdown { get; set; } = true;
}
