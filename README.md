# MarkItDownNet

MarkItDownNet is a lightweight .NET library that converts PDFs and images into normalised Markdown with positional metadata. For each processed document the library returns:

* Canonical Markdown text
* Page information (original width and height)
* Line level bounding boxes
* Word level bounding boxes

Bounding boxes use `[x,y,w,h]` normalised to `[0..1]` with a top left origin.

## FUNSD dataset comparison

Una descrizione del tool di confronto con il dataset FUNSD, il report delle differenze di bounding box e le istruzioni per l'esecuzione sono disponibili in [docs/funsd_comparison.md](docs/funsd_comparison.md).

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
./dotnet-install.sh --channel 9.0
~/.dotnet/dotnet --version
```

## Build and Test

All build and test commands must use the locally installed `dotnet`:

```bash
~/.dotnet/dotnet build
~/.dotnet/dotnet test
```

## Tesseract and leptonica

Le librerie native di **Tesseract** (`libtesseract.so.5`) e **Leptonica** (`liblept.so.5`) per Linux `x64` sono ora incluse nel repository sotto `src/MarkItDownNet/TesseractOCR/x64` e vengono copiate automaticamente vicino ai binari compilati. Non è quindi necessario installare pacchetti di sistema né creare collegamenti simbolici.

Per eseguire l'OCR è comunque richiesto il percorso ai file `tessdata` delle lingue. Impostare `OcrDataPath` nelle opzioni puntando a una cartella contenente i dati di lingua desiderati (ad es. scaricandoli da `https://github.com/tesseract-ocr/tessdata_fast`).

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
| Word count | 17 344 | 17 803 | +2.65% |
| Word match rate | 100% | 99.37% | −0.63% |
| Markdown similarity | – | 73% | – |
| BBox mean absolute error | 0% | 10.74% | +10.74% |

These large arXiv PDFs showed a 99.37% word match rate and a 10.74% mean absolute error in bounding boxes.

## Docling comparison

The `tests` project verifies Markdown and bounding box accuracy against the [Docling](https://github.com/docling-project/docling) ground truth for `ocr_test.pdf`.

| Item | Docling | MarkItDownNet | Abs. diff | Diff % |
| --- | --- | --- | --- | --- |
| Markdown | `Docling bundles PDF document conversion to JSON and Markdown in an easy self contained package` | same | 0 | 0% |
| BBox X | 0.1171 | 0.1171 | 0 | 0% |
| BBox Y | 0.0915 | 0.0915 | 0 | 0% |
| BBox W | 0.7312 | 0.7312 | 0 | 0% |
| BBox H | 0.0902 | 0.0902 | 0 | 0% |

Bounding boxes use normalised `[x,y,w,h]` coordinates. The test asserts equality within a two decimal tolerance.

## Docling data conversion timings

The following timings were captured while converting the PDF, TIFF, and PNG samples from Docling's `tests/data` directory. Each value represents the time in milliseconds to produce Markdown text and to serialise bounding boxes.

| File | Type | Markdown ms | BBox ms |
| --- | --- | --- | --- |
| 2305.03393v1-pg9-img.png | png | 1537.34 | 52.91 |
| 2203.01017v2.pdf | pdf | 1147.85 | 44.90 |
| 2206.01062.pdf | pdf | 654.79 | 20.40 |
| 2305.03393v1-pg9.pdf | pdf | 85.03 | 0.87 |
| 2305.03393v1.pdf | pdf | 287.15 | 16.69 |
| amt_handbook_sample.pdf | pdf | 136.57 | 1.46 |
| code_and_formula.pdf | pdf | 49.39 | 1.85 |
| multi_page.pdf | pdf | 63.96 | 2.85 |
| picture_classification.pdf | pdf | 20.78 | 1.19 |
| redp5110_sampled.pdf | pdf | 302.47 | 12.68 |
| right_to_left_01.pdf | pdf | 32.79 | 0.54 |
| right_to_left_02.pdf | pdf | 20.31 | 0.49 |
| right_to_left_03.pdf | pdf | 34.83 | 0.36 |
| 2206.01062.tif | tiff | 4007.83 | 1.80 |

| Type | Avg Markdown ms | Avg BBox ms |
| --- | --- | --- |
| png | 1537.34 | 52.91 |
| pdf | 236.33 | 8.69 |
| tiff | 4007.83 | 1.80 |
| **Overall** | 598.65 | 11.36 |

### Comparison with markitdown timings

The [markitdown](https://github.com/mapo80/markitdown) project reports Docling dataset timings in seconds. Comparing the published averages shows that MarkItDownNet processes these samples substantially faster:

| Type | markitdown MD&nbsp;s | markitdown BBox&nbsp;s | MarkItDownNet MD&nbsp;s | MarkItDownNet BBox&nbsp;s |
| --- | --- | --- | --- | --- |
| pdf | 3.29 | 5.14 | 0.24 | 0.01 |
| png | 2.51 | 5.56 | 1.54 | 0.05 |
| tiff | 2.57 | 4.19 | 4.01 | 0.00 |
| **Overall** | 3.18 | 5.10 | 0.60 | 0.01 |

On these samples, MarkItDownNet completed Markdown conversion roughly an order of magnitude faster for PDFs and produced bounding boxes two orders of magnitude quicker than markitdown.

## License

MIT
