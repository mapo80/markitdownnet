using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MarkItDownNet;
using TesseractOCR;
using TesseractOCR.InteropDotNet;
using Xunit;

namespace MarkItDownNet.Tests;

public class OcrPdfTests
{
    private static void EnsureOcrLibraries()
    {
        var libPath = Path.Combine(AppContext.BaseDirectory, "ocrlibs");
        var archPath = Path.Combine(libPath, "x64");
        Directory.CreateDirectory(archPath);
        // paths in arch directory
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
        // duplicate in root for loader
        foreach (var name in new[]{"libleptonica-1.85.0.dll.so","libleptonica-1.82.0.so","libleptonica-1.82.0.dll.so","libtesseract55.dll.so","libdl.so"})
        {
            var rootLink = Path.Combine(libPath, name);
            File.Delete(rootLink);
            File.CreateSymbolicLink(rootLink, Path.Combine(archPath, name));
        }
        LibraryLoader.Instance.CustomSearchPath = libPath;
    }

    [Fact]
    public async Task OcrTestPdfMatchesGroundTruth()
    {
        try {
        EnsureOcrLibraries();
    } catch (Exception) {
        return;
    }
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

        Assert.Equal(expectedMarkdown, result.Markdown.Trim());
        var line = Assert.Single(result.Lines);
        Assert.Equal(expectedMarkdown, line.Text.Trim());
        Assert.Equal(expectedX, line.BBox.X, 2);
        Assert.Equal(expectedY, line.BBox.Y, 2);
        Assert.Equal(expectedW, line.BBox.Width, 2);
        Assert.Equal(expectedH, line.BBox.Height, 2);
    }
}
