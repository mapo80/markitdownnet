using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidLayoutNet;

public sealed class LayoutDetector : IDisposable
{
    private readonly float[] MeanValues = [0.485F * 255F, 0.456F * 255F, 0.406F * 255F];
    private readonly float[] NormValues = [1.0F / 0.229F / 255.0F, 1.0F / 0.224F / 255.0F, 1.0F / 0.225F / 255.0F];

    private InferenceSession? _session;
    private string _imageInput = string.Empty;
    private string _scaleInput = string.Empty;

    public void InitModel(string path, int numThread = 1)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Layout model file does not exist: '{path}'.");
        }

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            InterOpNumThreads = numThread,
            IntraOpNumThreads = numThread
        };

        _session = new InferenceSession(path, options);
        // Inputs: scale_factor and image
        _scaleInput = _session.InputMetadata.Keys.First(k => k.Contains("scale"));
        _imageInput = _session.InputMetadata.Keys.First(k => k.Contains("image"));
    }

    public IReadOnlyList<LayoutBox> Detect(SKBitmap src, float scoreThreshold = 0.5f)
    {
        if (_session == null)
        {
            throw new InvalidOperationException("Model not initialised.");
        }

        float scaleY = 480f / src.Height;
        float scaleX = 480f / src.Width;

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var resized = src.Resize(new SKSizeI(480, 480), sampling);
        Tensor<float> input = LayoutUtils.SubtractMeanNormalize(resized, MeanValues, NormValues);

        var scaleTensor = new DenseTensor<float>(new[] { 1, 2 });
        scaleTensor[0, 0] = scaleY;
        scaleTensor[0, 1] = scaleX;

        IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor(_imageInput, input),
            NamedOnnxValue.CreateFromTensor(_scaleInput, scaleTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        var boxTensor = results[0].AsEnumerable<float>().ToArray();
        int count = results[1].AsEnumerable<int>().First();

        var boxes = new List<LayoutBox>();
        for (int i = 0; i < count; i++)
        {
            int offset = i * 6;
            int rawLabel = (int)boxTensor[offset];
            LayoutLabel label = Enum.IsDefined(typeof(LayoutLabel), rawLabel)
                ? (LayoutLabel)rawLabel
                : LayoutLabel.Unknown;

            float score = boxTensor[offset + 1];
            if (score < scoreThreshold) continue;
            float x1 = Math.Clamp(boxTensor[offset + 2], 0, src.Width);
            float y1 = Math.Clamp(boxTensor[offset + 3], 0, src.Height);
            float x2 = Math.Clamp(boxTensor[offset + 4], 0, src.Width);
            float y2 = Math.Clamp(boxTensor[offset + 5], 0, src.Height);
            boxes.Add(new LayoutBox(label, score, x1, y1, x2, y2));
        }

        return boxes;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
