using System.Collections.Generic;
using RapidOcrNet;

namespace RapidStructureNet;

/// <summary>
/// Result of running the structure pipeline on a single page.
/// </summary>
/// <param name="Regions">Detected regions ordered by appearance.</param>
/// <param name="Ocr">Full page OCR result.</param>
/// <param name="Orientation">Rotation angle in degrees (0 when orientation detection is disabled).</param>
public sealed record StructureResult(
    IReadOnlyList<StructureRegion> Regions,
    OcrResult Ocr,
    float Orientation,
    long LayoutTimeMs,
    long OcrTimeMs,
    long TableTimeMs);

