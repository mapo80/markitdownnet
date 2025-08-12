using System;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TesseractOCR;
using TesseractOCR.Enums;

namespace MarkItDownNet.Tests;

public class TesseractOcrTests
{
    [Fact]
    public void Can_extract_text_from_simple_image()
    {
        var libPath = Path.Combine(AppContext.BaseDirectory, "ocrlibs");
        Directory.CreateDirectory(Path.Combine(libPath, "x64"));
        var leptLink = Path.Combine(libPath, "x64", "libleptonica-1.85.0.dll.so");
        var tessLink = Path.Combine(libPath, "x64", "libtesseract55.dll.so");
        var dlLink = Path.Combine(libPath, "x64", "libdl.so");
        File.Delete(leptLink);
        File.Delete(tessLink);
        File.Delete(dlLink);
        File.CreateSymbolicLink(leptLink, "/usr/lib/x86_64-linux-gnu/liblept.so.5");
        File.CreateSymbolicLink(tessLink, "/usr/lib/x86_64-linux-gnu/libtesseract.so.5");
        File.CreateSymbolicLink(dlLink, "/usr/lib/x86_64-linux-gnu/libdl.so.2");
        TesseractOCR.InteropDotNet.LibraryLoader.Instance.CustomSearchPath = libPath;

        using var image = new Image<Rgba32>(120, 40);
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White);
            Font font = SystemFonts.CreateFont("DejaVu Sans", 20);
            ctx.DrawText("hi", font, Color.Black, new PointF(10, 5));
        });

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        image.Save(temp);

        using var engine = new Engine("/usr/share/tesseract-ocr/5/tessdata", Language.English, EngineMode.Default);
        using var pix = TesseractOCR.Pix.Image.LoadFromFile(temp);
        using var page = engine.Process(pix);

        Assert.Contains("hi", page.Text.ToLowerInvariant());
    }
}
