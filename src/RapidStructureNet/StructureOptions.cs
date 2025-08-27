namespace RapidStructureNet;

/// <summary>
/// Options controlling the structure analysis pipeline.
/// </summary>
public sealed record StructureOptions
{
    /// <summary>
    /// When true the pipeline attempts to detect page orientation and enables angle-aware OCR.
    /// Without an <see cref="IOrientationDetector"/> the orientation angle will remain 0.
    /// </summary>
    public bool DetectOrientation { get; init; } = false;

    /// <summary>
    /// Minimum confidence required for a layout region to be kept.
    /// </summary>
    public float LayoutScoreThreshold { get; init; } = 0.5f;
}

