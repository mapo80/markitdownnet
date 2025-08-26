using System;
using System.IO;
using System.Threading.Tasks;
using MarkItDownNet;
using RapidOcrNet;
using SkiaSharp;
using Xunit;

namespace MarkItDownNet.Tests;

public class OcrImageTests
{
    [SkippableFact]
    public async Task Can_extract_text_from_simple_png()
    {
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
            OcrLanguage = OcrLanguage.English,
            NormalizeMarkdown = false
        };
        Skip.IfNot(Directory.Exists(options.OcrDataPath), "Tesseract data not found");
        var converter = new MarkItDownConverter(options);
        var result = await converter.ConvertAsync(temp, "image/png");

        Assert.Contains("hi", result.Markdown.ToLowerInvariant());
    }
}
