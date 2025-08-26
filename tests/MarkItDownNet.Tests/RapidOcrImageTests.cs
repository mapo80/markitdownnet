using System;
using System.IO;
using System.Threading.Tasks;
using MarkItDownNet;
using RapidOcrNet;
using SkiaSharp;

namespace MarkItDownNet.Tests;

public class RapidOcrImageTests
{
    private static string CreateImage(int width, int height, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        draw(canvas);

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(temp);
        data.SaveTo(fs);
        return temp;
    }

    private static MarkItDownConverter CreateConverter()
    {
        var options = new MarkItDownOptions
        {
            OcrEngine = OcrEngine.RapidOcr,
            OcrLanguage = OcrLanguage.English,
            NormalizeMarkdown = false
        };
        return new MarkItDownConverter(options);
    }

    [Fact]
    public async Task Can_extract_text_from_simple_png_with_rapidocr()
    {
        var temp = CreateImage(120, 40, canvas =>
        {
            using var font = new SKFont { Size = 20 };
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawText("IT", new SKPoint(10, 30), font, paint);
        });

        var converter = CreateConverter();
        var result = await converter.ConvertAsync(temp, "image/png");
        Assert.Contains("it", result.Markdown.ToLowerInvariant());
    }

    [Fact]
    public async Task Can_extract_text_from_multi_line_png_with_rapidocr()
    {
        var temp = CreateImage(200, 80, canvas =>
        {
            using var font = new SKFont { Size = 20 };
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawText("hello world", new SKPoint(10, 30), font, paint);
            canvas.DrawText("from rapidocr", new SKPoint(10, 60), font, paint);
        });

        var converter = CreateConverter();
        var result = await converter.ConvertAsync(temp, "image/png");

        var markdown = result.Markdown.ToLowerInvariant();
        Assert.Contains("hello world", markdown);
        Assert.Contains("from rapidocr", markdown);
        Assert.True(result.Lines.Count >= 2);
    }

    [Fact]
    public async Task Extracted_text_has_valid_bounding_box()
    {
        var temp = CreateImage(120, 40, canvas =>
        {
            using var font = new SKFont { Size = 20 };
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawText("bbox", new SKPoint(10, 30), font, paint);
        });

        var converter = CreateConverter();
        var result = await converter.ConvertAsync(temp, "image/png");

        var word = Assert.Single(result.Words);
        var bbox = word.BBox;
        Assert.InRange(bbox.X, 0, 1);
        Assert.InRange(bbox.Y, 0, 1);
        Assert.InRange(bbox.Width, 0.01, 1);
        Assert.InRange(bbox.Height, 0.01, 1);
    }

    [Fact]
    public async Task Returns_empty_result_for_blank_image()
    {
        var temp = CreateImage(120, 40, _ => { });

        var converter = CreateConverter();
        var result = await converter.ConvertAsync(temp, "image/png");

        Assert.True(string.IsNullOrWhiteSpace(result.Markdown));
        Assert.Empty(result.Lines);
        Assert.Empty(result.Words);
    }
}

