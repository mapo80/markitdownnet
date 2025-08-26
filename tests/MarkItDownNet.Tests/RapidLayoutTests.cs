using RapidLayoutNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class RapidLayoutTests : IDisposable
{
    private readonly LayoutDetector _detector;
    private static readonly string TrainingDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dataset", "training"));

    public RapidLayoutTests()
    {
        string modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidLayoutNet", "models", "PP-DocLayout-S_infer.onnx"));
        _detector = new LayoutDetector();
        _detector.InitModel(modelPath);
    }

    public void Dispose() => _detector.Dispose();

    public static IEnumerable<object[]> TrainingImages()
    {
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        foreach (var file in Directory.EnumerateFiles(TrainingDir))
        {
            if (exts.Contains(Path.GetExtension(file)))
            {
                yield return new object[] { file };
            }
        }
    }

    [Theory]
    [MemberData(nameof(TrainingImages))]
    public void Detects_layout_on_training_images(string path)
    {
        using var bmp = SKBitmap.Decode(path);
        var boxes = _detector.Detect(bmp, 0.3f);
        Assert.NotEmpty(boxes);
        Assert.DoesNotContain(boxes, b => b.Label == LayoutLabel.Unknown);
        foreach (var b in boxes)
        {
            Assert.InRange(b.X1, 0, bmp.Width);
            Assert.InRange(b.Y1, 0, bmp.Height);
            Assert.InRange(b.X2, 0, bmp.Width);
            Assert.InRange(b.Y2, 0, bmp.Height);
            Assert.InRange(b.Score, 0f, 1f);
        }
    }

    [Fact]
    public void Throws_when_model_missing()
    {
        var det = new LayoutDetector();
        Assert.Throws<FileNotFoundException>(() => det.InitModel("missing.onnx"));
    }

    [Fact]
    public void Busta_paga_contains_table()
    {
        string path = Path.Combine(TrainingDir, "busta_paga_internet.jpeg");
        using var bmp = SKBitmap.Decode(path);
        var boxes = _detector.Detect(bmp, 0.3f);
        Assert.Contains(boxes, b => b.Label == LayoutLabel.Table);
        Assert.Contains(boxes, b => b.Label == LayoutLabel.Text);
    }
}
