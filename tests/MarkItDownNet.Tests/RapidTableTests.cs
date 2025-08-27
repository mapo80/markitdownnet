using System;
using RapidLayoutNet;
using RapidTableNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class RapidTableTests : IDisposable
{
    private readonly LayoutDetector _layout;
    private readonly TableRecognizer _table;
    private static readonly string TrainingDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dataset", "training"));

    static RapidTableTests()
    {
        Environment.SetEnvironmentVariable("ORT_LOG_VERBOSITY_LEVEL", "0");
        Environment.SetEnvironmentVariable("ORT_LOG_SEVERITY_LEVEL", "3");
    }

    public RapidTableTests()
    {
        string layoutModel = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidLayoutNet", "models", "PP-DocLayout-S_infer.onnx"));
        _layout = new LayoutDetector();
        _layout.InitModel(layoutModel);

        string tableModelDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidTableNet", "models"));
        _table = new TableRecognizer();
        _table.InitModel(TableModel.SlanetPlus, tableModelDir);
    }

    public void Dispose()
    {
        _layout.Dispose();
        _table.Dispose();
    }

    [Fact]
    public void Detects_structure_on_training_image()
    {
        string path = Path.Combine(TrainingDir, "busta_paga_internet.jpeg");
        using var bmp = SKBitmap.Decode(path);
        var boxes = _layout.Detect(bmp, 0.3f);
        var tableBox = boxes.First(b => b.Label == LayoutLabel.Table);
        var rect = new SKRectI((int)tableBox.X1, (int)tableBox.Y1, (int)tableBox.X2, (int)tableBox.Y2);
        using var img = SKImage.FromBitmap(bmp);
        using var subset = img.Subset(rect);
        using var tableBmp = SKBitmap.FromImage(subset);
        var result = _table.Detect(tableBmp);
        Assert.NotEmpty(result.Structure);
        Assert.NotEmpty(result.CellBoxes);
        Assert.False(string.IsNullOrWhiteSpace(result.Html));
        Assert.True(result.PreprocessTimeMs >= 0);
        Assert.True(result.InferenceTimeMs > 0);
        Assert.True(result.DecodeTimeMs > 0);
        Assert.Equal(result.PreprocessTimeMs + result.InferenceTimeMs + result.DecodeTimeMs, result.TotalTimeMs);
    }

    [Fact]
    public void Initialises_all_models()
    {
        string modelDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RapidTableNet", "models"));
        foreach (TableModel m in Enum.GetValues<TableModel>())
        {
            using var rec = new TableRecognizer();
            rec.InitModel(m, modelDir);
        }
    }
}
