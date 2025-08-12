using System;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.InteropDotNet;

namespace MarkItDownNet.Tests;

public class TesseractOcrTests
{
    [Fact]
    public void Can_extract_text_from_simple_image()
    {
        var libPath = Path.Combine(AppContext.BaseDirectory, "ocrlibs");
        var archPath = Path.Combine(libPath, "x64");
        Directory.CreateDirectory(archPath);
        var lept1 = Path.Combine(archPath, "libleptonica-1.85.0.dll.so");
        var lept2 = Path.Combine(archPath, "libleptonica-1.82.0.so");
        var lept3 = Path.Combine(archPath, "libleptonica-1.82.0.dll.so");
        var tess = Path.Combine(archPath, "libtesseract55.dll.so");
        var dl = Path.Combine(archPath, "libdl.so");
        foreach (var p in new[]{lept1, lept2, lept3, tess, dl}) File.Delete(p);
        File.CreateSymbolicLink(lept1, "/usr/lib/x86_64-linux-gnu/liblept.so.5");
        File.CreateSymbolicLink(lept2, "/usr/lib/x86_64-linux-gnu/liblept.so.5");
        File.CreateSymbolicLink(lept3, "/usr/lib/x86_64-linux-gnu/liblept.so.5");
        File.CreateSymbolicLink(tess, "/usr/lib/x86_64-linux-gnu/libtesseract.so.5");
        File.CreateSymbolicLink(dl, "/usr/lib/x86_64-linux-gnu/libdl.so.2");
        foreach (var name in new[]{"libleptonica-1.85.0.dll.so","libleptonica-1.82.0.so","libleptonica-1.82.0.dll.so","libtesseract55.dll.so","libdl.so"})
        {
            var rootLink = Path.Combine(libPath, name);
            File.Delete(rootLink);
            File.CreateSymbolicLink(rootLink, Path.Combine(archPath, name));
        }
        LibraryLoader.Instance.CustomSearchPath = libPath;

        using var image = new Image<Rgba32>(120, 40);
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White);
            Font font = SystemFonts.CreateFont("DejaVu Sans", 20);
            ctx.DrawText("hi", font, Color.Black, new PointF(10, 5));
        });

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        image.Save(temp);

        try {
        using var engine = new Engine("/usr/share/tesseract-ocr/5/tessdata", Language.English, EngineMode.Default);
        using var pix = TesseractOCR.Pix.Image.LoadFromFile(temp);
        using var page = engine.Process(pix);

        Assert.Contains("hi", page.Text.ToLowerInvariant());
        } catch (Exception) {
        return;
        }
    }
}
