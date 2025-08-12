using MarkItDownNet;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using Xunit;

namespace MarkItDownNet.Tests;

public class ConversionTests
{
    [Fact]
    public async Task PdfWithDigitalTextProducesMarkdownAndWords()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "sample.pdf");
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(200, 200);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Hello world", 12, new PdfPoint(10, 150), font);
        await File.WriteAllBytesAsync(tmp, builder.Build());

        var converter = new MarkItDownConverter(new MarkItDownOptions { NormalizeMarkdown = false });
        var result = await converter.ConvertAsync(tmp, "application/pdf");

        Assert.False(string.IsNullOrWhiteSpace(result.Markdown));
        Assert.NotEmpty(result.Words);
        Assert.All(result.Words, w =>
        {
            Assert.InRange(w.BBox.X, 0, 1);
            Assert.InRange(w.BBox.Y, 0, 1);
        });
    }
}
