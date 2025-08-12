using System;
using System.IO;
using SkiaSharp;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.InteropDotNet;

namespace MarkItDownNet.Tests;

public class TesseractOcrTests
{
    [Fact]
    public void Can_extract_text_from_simple_image()
    {
        // Ensure the loader searches the system library path where the
        // `libtesseract55.dll.so` and `libleptonica-1.85.0.dll.so` symlinks
        // have been created.
        LibraryLoader.Instance.CustomSearchPath = "/usr/lib/x86_64-linux-gnu";

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

