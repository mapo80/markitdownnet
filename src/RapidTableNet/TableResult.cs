namespace RapidTableNet;

public sealed record TableResult(IReadOnlyList<string> Structure, IReadOnlyList<float[]> CellBoxes);
