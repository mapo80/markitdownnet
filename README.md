# MarkItDownNet

MarkItDownNet is a lightweight .NET library that converts PDFs and images into normalised Markdown with positional metadata. For each processed document the library returns:

* Canonical Markdown text
* Page information (original width and height)
* Line level bounding boxes
* Word level bounding boxes

Bounding boxes use `[x,y,w,h]` normalised to `[0..1]` with a top left origin.

## Pipeline

```
PDF -> PdfPig text extraction -> (optional) PDFtoImage rasterisation -> Tesseract OCR
Image -> Tesseract OCR
                 |                 
                 v                 
            Markdown (Markdig)
```

If a PDF yields too few native words the pages are rasterised with **PDFtoImage** and OCRed with **Tesseract**.

## Installing .NET

This repository does not rely on the system dotnet. Install the SDK locally using the provided script:

```bash
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 8.0
~/.dotnet/dotnet --version
```

## Build and Test

All build and test commands must use the locally installed `dotnet`:

```bash
~/.dotnet/dotnet build
~/.dotnet/dotnet test
```

## Ubuntu 24.04 dependencies

The `TesseractOCR` package requires native **Tesseract** and **Leptonica** libraries. On Ubuntu 24.04 these are available via the standard packages, e.g. the [`libleptonica-dev_1.82.0-3build4` package](https://ubuntu.pkgs.org/24.04/ubuntu-universe-amd64/libleptonica-dev_1.82.0-3build4_amd64.deb.html):

```bash
sudo apt-get update
sudo apt-get install -y tesseract-ocr libleptonica-dev

# provide friendly names expected by TesseractOCR
sudo ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.85.0.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract55.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
```

## Usage

```csharp
var options = new MarkItDownOptions
{
    OcrDataPath = "/usr/share/tesseract-ocr/5/tessdata",
    OcrLanguages = "eng",
    PdfRasterDpi = 300
};
var converter = new MarkItDownConverter(options);
var result = await converter.ConvertAsync("sample.pdf", "application/pdf");
Console.WriteLine(result.Markdown);
```

## Configuration

`MarkItDownOptions` exposes run‑time tunables:

* `OcrDataPath` – location of Tesseract language data (`TESSDATA_PREFIX`)
* `OcrLanguages` – languages passed to Tesseract (e.g. `ita+eng`)
* `PdfRasterDpi` – DPI for rasterising PDFs during OCR fallback
* `MinimumNativeWordThreshold` – minimum words before OCR is triggered
* `NormalizeMarkdown` – toggle Markdig normalisation

## Logging

Logging uses **Serilog**. The library reads standard Serilog settings (see `src/MarkItDownNet/appsettings.json` for an example) and supports console and rolling file sinks. Set `Serilog__MinimumLevel` via environment variables to control verbosity.

## Testing assets

Tests create a small PDF on the fly ensuring that extraction works without external files. OCR based tests are not executed by default as they require Tesseract data files.

## Evaluation

A comparison with Docling ground truth on sample PDFs and TIFFs is available in the [Docling comparison report](docs/docling_comparison.md).

Docling's image samples are distributed as TIFF files. The comparison tool converts them to JPEG via [BitMiracle.LibTiff.NET](https://www.nuget.org/packages/BitMiracle.LibTiff.NET) and [SkiaSharp](https://www.nuget.org/packages/SkiaSharp) before passing them to MarkItDownNet:

```bash
~/.dotnet/dotnet run --project tools/DoclingComparison/DoclingComparison.csproj docling/tests/data/tiff/2206.01062.tif
```

| Metric | Docling | MarkItDownNet | Difference |
| --- | --- | --- | --- |
| Word count | 16 080 | 16 048 | −0.20% |
| Word match rate | 100% | 99.20% | −0.80% |
| Markdown similarity | – | 38% | – |
| BBox mean absolute error | 0% | 5.27% | +5.27% |

Overall, MarkItDownNet produced slightly fewer words than Docling, matched 99.20% of ground-truth words and showed a 5.27% bounding-box deviation.

## License

MIT
