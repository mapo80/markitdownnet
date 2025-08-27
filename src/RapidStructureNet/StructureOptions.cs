namespace RapidStructureNet;

/// <summary>
/// Options controlling the structure analysis pipeline.
/// </summary>
public sealed record StructureOptions
{
    /// <summary>
    /// When true the pipeline will attempt to detect page orientation.
    /// Currently this feature is not implemented and orientation will always be 0.
    /// </summary>
    public bool DetectOrientation { get; init; } = false;

    /// <summary>
    /// Minimum confidence required for a layout region to be kept.
    /// </summary>
    public float LayoutScoreThreshold { get; init; } = 0.5f;
}

