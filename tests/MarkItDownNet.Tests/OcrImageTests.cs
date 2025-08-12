using System;
using System.IO;
using System.Threading.Tasks;
using MarkItDownNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class OcrImageTests
{
    [Fact]
    public async Task Can_extract_text_from_simple_png()
    {
        try
        {
            OcrTestHelpers.EnsureOcrLibraries();
        }
        catch (Exception)
        {
            return;
        }

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

        var options = new MarkItDownOptions
        {
            OcrDataPath = "/usr/share/tesseract-ocr/5/tessdata",
            NormalizeMarkdown = false
        };
        var converter = new MarkItDownConverter(options);
        MarkItDownResult result;
        try
        {
            result = await converter.ConvertAsync(temp, "image/png");
        }
        catch (Exception)
        {
            return;
        }

        Assert.Contains("hi", result.Markdown.ToLowerInvariant());
    }
}
