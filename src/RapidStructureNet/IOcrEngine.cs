using RapidOcrNet;
using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// Minimal abstraction over an OCR engine used by the structure pipeline.
/// </summary>
public interface IOcrEngine
{
    OcrResult Detect(SKBitmap image, RapidOcrOptions options);
}

/// <summary>
/// Adapter to use <see cref="RapidOcr"/> as an <see cref="IOcrEngine"/>.
/// </summary>
public sealed class RapidOcrEngine : IOcrEngine
{
    private readonly RapidOcr _inner;
    public RapidOcrEngine(RapidOcr inner) => _inner = inner;
    public OcrResult Detect(SKBitmap image, RapidOcrOptions options) => _inner.Detect(image, options);
}

