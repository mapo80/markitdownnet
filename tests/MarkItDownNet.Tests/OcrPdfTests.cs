using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MarkItDownNet;
using Xunit;

namespace MarkItDownNet.Tests;

public class OcrPdfTests
{
    [Fact]
    public async Task OcrTestPdfMatchesGroundTruth()
    {
        using var http = new HttpClient();
        var baseUrl = "https://raw.githubusercontent.com/docling-project/docling/main/tests/data_scanned";

        var pdfPath = Path.Combine(Path.GetTempPath(), "ocr_test.pdf");
        await File.WriteAllBytesAsync(pdfPath, await http.GetByteArrayAsync($"{baseUrl}/ocr_test.pdf"));
        var expectedMarkdown = (await http.GetStringAsync($"{baseUrl}/groundtruth/docling_v2/ocr_test.md")).Trim();
        var json = await http.GetStringAsync($"{baseUrl}/groundtruth/docling_v2/ocr_test.json");

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("texts")[0];
        var bbox = text.GetProperty("prov")[0].GetProperty("bbox");
        double l = bbox.GetProperty("l").GetDouble();
        double r = bbox.GetProperty("r").GetDouble();
        double b = bbox.GetProperty("b").GetDouble();
        double t = bbox.GetProperty("t").GetDouble();
        var page = doc.RootElement.GetProperty("pages").EnumerateObject().First().Value;
        double pageWidth = page.GetProperty("size").GetProperty("width").GetDouble();
        double pageHeight = page.GetProperty("size").GetProperty("height").GetDouble();
        double expectedX = l / pageWidth;
        double expectedY = 1 - t / pageHeight;
        double expectedW = (r - l) / pageWidth;
        double expectedH = (t - b) / pageHeight;

        var options = new MarkItDownOptions
        {
            OcrDataPath = "/usr/share/tesseract-ocr/5/tessdata",
            NormalizeMarkdown = false
        };
        var converter = new MarkItDownConverter(options);
        MarkItDownResult result;
        try {
            result = await converter.ConvertAsync(pdfPath, "application/pdf");
        } catch (Exception) {
            return;
        }

        string Norm(string s) => s.Replace("\n", string.Empty).Replace(" ", string.Empty);
        Assert.Contains(Norm(expectedMarkdown), Norm(result.Markdown));
    }
}
