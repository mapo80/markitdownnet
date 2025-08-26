using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidTableNet;

public sealed class TableRecognizer : IDisposable
{
    private InferenceSession? _session;
    private string _inputName = string.Empty;
    private TableModel _model;
    private TableLabelDecoder? _decoder;

    public void InitModel(TableModel model, string? modelDir = null, int numThread = 1)
    {
        string fileName = model.GetFileName();
        string path = modelDir == null ? fileName : Path.Combine(modelDir, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Table model file does not exist: '{path}'.");
        }

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            InterOpNumThreads = numThread,
            IntraOpNumThreads = numThread
        };

        _session = new InferenceSession(path, options);
        _inputName = _session.InputMetadata.Keys.First();
        _model = model;
        _decoder = new TableLabelDecoder(model);
    }

    public TableResult Detect(SKBitmap src)
    {
        if (_session == null || _decoder == null)
        {
            throw new InvalidOperationException("Model not initialised.");
        }

        var (tensor, shape) = TablePreprocessor.Process(src);
        IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };
        using var results = _session.Run(inputs);
        var bboxPreds = results[0].AsTensor<float>();
        var structProbs = results[1].AsTensor<float>();
        var (structure, boxes) = _decoder.Decode(bboxPreds, structProbs, shape);
        return new TableResult(structure, boxes);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
