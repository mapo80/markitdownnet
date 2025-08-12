using System;
using System.IO;
using SkiaSharp;
using TesseractOCR;
using TesseractOCR.Enums;

namespace MarkItDownNet.Tests;

public class TesseractOcrTests
{
    [Fact(Skip = "Requires native Tesseract libraries")]
    public void Can_extract_text_from_simple_image()
    {
        var libPath = Path.Combine(AppContext.BaseDirectory, "ocrlibs");
        var x64 = Path.Combine(libPath, "x64");
        Directory.CreateDirectory(x64);
        var leptLink = Path.Combine(x64, "libleptonica-1.85.0.dll.so");
        var tessLink = Path.Combine(x64, "libtesseract55.dll.so");
        var dlLink = Path.Combine(x64, "libdl.so");
        File.Delete(leptLink);
        File.Delete(tessLink);
        File.Delete(dlLink);
        File.CreateSymbolicLink(leptLink, "/usr/lib/x86_64-linux-gnu/liblept.so.5");
        File.CreateSymbolicLink(tessLink, "/usr/lib/x86_64-linux-gnu/libtesseract.so.5");
        File.CreateSymbolicLink(dlLink, "/usr/lib/x86_64-linux-gnu/libdl.so.2");
        TesseractOCR.InteropDotNet.LibraryLoader.Instance.CustomSearchPath = x64;

        using var surface = SKSurface.Create(new SKImageInfo(120, 40));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var font = new SKFont { Size = 20 };
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawText("hi", new SKPoint(10, 30), font, paint);

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        using (var image = surface.Snapshot())
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.OpenWrite(temp))
        {
            data.SaveTo(fs);
        }

        using var engine = new Engine("/usr/share/tesseract-ocr/5/tessdata", Language.English, EngineMode.Default);
        using var pix = TesseractOCR.Pix.Image.LoadFromFile(temp);
        using var page = engine.Process(pix);

        Assert.Contains("hi", page.Text.ToLowerInvariant());
    }
}
