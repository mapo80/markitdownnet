using System;
using System.Collections.Generic;
using System.IO;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace MarkItDownNet;

static class Rasterizer
{
    public static IEnumerable<Pix> FromPdf(string path, int dpi)
    {
        var opts = new RenderOptions { Dpi = dpi };
        using var stream = File.OpenRead(path);
        foreach (var bmp in Conversion.ToImages(stream, leaveOpen: false, password: null, opts))
        {
            yield return FromBitmap(bmp, dpi);
        }
    }

    public static Pix FromImage(string path, int dpi)
    {
        using var src = Pix.LoadFromFile(path);
        var xres = src.XRes;
        if (xres <= 0) xres = dpi;
        float scale = xres < 220 ? dpi / (float)xres : 1f;
        Pix scaled = scale == 1f ? src.Clone() : src.Scale(scale, scale);
        return Preprocess(scaled, dpi);
    }

    static Pix FromBitmap(SKBitmap bmp, int dpi)
    {
        using (bmp)
        {
            using var gray = bmp.Copy(SKColorType.Gray8);
            using var img = SKImage.FromBitmap(gray);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var pix = Pix.LoadFromMemory(data.ToArray());
            return Preprocess(pix, dpi);
        }
    }

    static Pix Preprocess(Pix pix, int dpi)
    {
        pix.XRes = dpi;
        pix.YRes = dpi;
        Pix work = pix.Depth == 8 ? pix : pix.ConvertTo8(0);
        if (!ReferenceEquals(work, pix)) pix.Dispose();
        Pix den = work;
        Pix bin;
        try { bin = den.BinarizeOtsuAdaptiveThreshold(0, 0, 0, 0, 0); if (!ReferenceEquals(bin, den)) den.Dispose(); }
        catch { bin = den; }
        try
        {
            var desk = bin.Deskew(out var skew);
            if (Math.Abs(skew.Angle) <= 3f)
            {
                bin.Dispose();
                bin = desk;
            }
            else
            {
                desk.Dispose();
            }
        }
        catch { }
        return bin;
    }
}
