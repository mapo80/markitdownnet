using System;
using System.Collections.Generic;
using System.IO;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace MarkItDownNet;

static class Rasterizer
{
    public static IEnumerable<(Pix pix, double angle, bool deskewed)> FromPdf(string path, MarkItDownOptions opt)
    {
        var ropt = new RenderOptions { Dpi = opt.OcrUserDpi };
        using var stream = File.OpenRead(path);
        foreach (var bmp in Conversion.ToImages(stream, leaveOpen: false, password: null, ropt))
        {
            yield return FromBitmap(bmp, opt);
        }
    }

    public static (Pix pix, double angle, bool deskewed) FromImage(string path, MarkItDownOptions opt)
    {
        using var src = Pix.LoadFromFile(path);
        var xres = src.XRes;
        if (xres <= 0) xres = opt.OcrUserDpi;
        float scale = xres < 220 ? opt.OcrUserDpi / (float)xres : 1f;
        Pix scaled = scale == 1f ? src.Clone() : src.Scale(scale, scale);
        return Preprocess(scaled, opt);
    }

    static (Pix pix, double angle, bool deskewed) FromBitmap(SKBitmap bmp, MarkItDownOptions opt)
    {
        using (bmp)
        {
            using var gray = bmp.Copy(SKColorType.Gray8);
            using var img = SKImage.FromBitmap(gray);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var pix = Pix.LoadFromMemory(data.ToArray());
            return Preprocess(pix, opt);
        }
    }

    static (Pix pix, double angle, bool deskewed) Preprocess(Pix pix, MarkItDownOptions opt)
    {
        pix.XRes = opt.OcrUserDpi;
        pix.YRes = opt.OcrUserDpi;
        Pix work = pix.Depth == 8 ? pix : pix.ConvertTo8(0);
        if (!ReferenceEquals(work, pix)) pix.Dispose();
        Pix result = work;
        double angle = 0;
        bool deskewed = false;
        if (opt.OcrPreBinarize)
        {
            try
            {
                var bin = result.BinarizeOtsuAdaptiveThreshold(0, 0, 0, 0, 0);
                if (!ReferenceEquals(bin, result))
                {
                    result.Dispose();
                    result = bin;
                }
            }
            catch { }
        }
        try
        {
            var desk = result.Deskew(out var skew);
            angle = skew.Angle;
            if (Math.Abs(angle) >= opt.OcrDeskewMinAngleDeg)
            {
                result.Dispose();
                result = desk;
                deskewed = true;
            }
            else
            {
                desk.Dispose();
            }
        }
        catch { }
        return (result, angle, deskewed);
    }
}
