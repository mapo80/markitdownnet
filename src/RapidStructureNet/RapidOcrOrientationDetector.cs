using System;
using RapidOcrNet;
using SkiaSharp;

namespace RapidStructureNet;

/// <summary>
/// Orientation detector based on RapidOCR's angle classifier.
/// Evaluates the image and a 90° rotation to derive 0/90/180/270 angles.
/// </summary>
public sealed class RapidOcrOrientationDetector : IOrientationDetector, IDisposable
{
    private readonly TextClassifier _classifier = new();

    public RapidOcrOrientationDetector(string modelPath, int numThread = 1)
    {
        _classifier.InitModel(modelPath, numThread);
    }

    public float Detect(SKBitmap image)
    {
        var angle0 = _classifier.GetAngle(image);
        using var rot90 = Rotate(image, 90);
        var angle90 = _classifier.GetAngle(rot90);

        float ori0 = angle0.Index == 0 ? 0f : 180f;
        float ori90 = angle90.Index == 0 ? 90f : 270f;

        return angle0.Score >= angle90.Score ? ori0 : ori90;
    }

    private static SKBitmap Rotate(SKBitmap src, float angle)
    {
        SKBitmap dst = angle % 180 == 0 ? new SKBitmap(src.Width, src.Height) : new SKBitmap(src.Height, src.Width);
        using var canvas = new SKCanvas(dst);
        canvas.Translate(dst.Width / 2f, dst.Height / 2f);
        canvas.RotateDegrees(angle);
        canvas.Translate(-src.Width / 2f, -src.Height / 2f);
        canvas.DrawBitmap(src, 0, 0);
        canvas.Flush();
        return dst;
    }

    public void Dispose() => _classifier.Dispose();
}
