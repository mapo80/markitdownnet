namespace RapidTableNet;

public sealed record TableResult(
    IReadOnlyList<string> Structure,
    IReadOnlyList<float[]> CellBoxes,
    string Html,
    long PreprocessTimeMs,
    long InferenceTimeMs,
    long DecodeTimeMs)
{
    public long TotalTimeMs => PreprocessTimeMs + InferenceTimeMs + DecodeTimeMs;
}
