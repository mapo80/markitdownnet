using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidLayoutNet;

internal static class LayoutUtils
{
    public static Tensor<float> SubtractMeanNormalize(SKBitmap src, float[] meanVals, float[] normVals)
    {
        int cols = src.Width;
        int rows = src.Height;
        int channels = src.BytesPerPixel;
        const int expChannels = 3; // B, G, R

        var inputTensor = new DenseTensor<float>(new[] { 1, expChannels, rows, cols });
        ReadOnlySpan<byte> span = src.GetPixelSpan();

        if (src.Info.ColorType != SKColorType.Bgra8888)
        {
            throw new ArgumentException($"This image needs to be '{SKColorType.Bgra8888}', but got '{src.Info.ColorType}'.");
        }

        for (int r = 0; r < rows; ++r)
        {
            for (int c = 0; c < cols; ++c)
            {
                int i = r * cols + c;
                for (int ch = 0; ch < expChannels; ++ch)
                {
                    byte value = span[i * channels + ch];
                    inputTensor[0, ch, r, c] = (value - meanVals[ch]) * normVals[ch];
                }
            }
        }

        return inputTensor;
    }
}
