using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidTableNet;

internal static class TablePreprocessor
{
    private const int MaxLen = 488;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];
    private const float Scale = 1f / 255f;

    public static (DenseTensor<float> Tensor, float[] Shape) Process(SKBitmap src)
    {
        int h = src.Height;
        int w = src.Width;
        float ratio = MaxLen / (float)Math.Max(h, w);
        int resizeH = (int)(h * ratio);
        int resizeW = (int)(w * ratio);

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var resized = src.Resize(new SKSizeI(resizeW, resizeH), sampling);

        using var padded = new SKBitmap(MaxLen, MaxLen);
        padded.Erase(SKColors.Black);
        using (var canvas = new SKCanvas(padded))
        {
            canvas.DrawBitmap(resized, 0, 0);
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, MaxLen, MaxLen });
        ReadOnlySpan<byte> span = padded.GetPixelSpan();
        int channels = padded.BytesPerPixel;

        for (int r = 0; r < MaxLen; r++)
        {
            for (int c = 0; c < MaxLen; c++)
            {
                int idx = r * MaxLen + c;
                for (int ch = 0; ch < 3; ch++)
                {
                    float val = span[idx * channels + ch];
                    tensor[0, ch, r, c] = (val * Scale - Mean[ch]) / Std[ch];
                }
            }
        }

        float[] shape = [h, w, ratio, ratio, MaxLen, MaxLen];
        return (tensor, shape);
    }
}
