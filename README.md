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
Per usare la libreria TesseractOCR con i pacchetti standard di Ubuntu senza modificare il codice sorgente:

Installare Tesseract e Leptonica

sudo apt-get update
sudo apt-get install -y tesseract-ocr libtesseract-dev libleptonica-dev libc6-dev
# install .NET SDK 8
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 8.0.401
export PATH=$HOME/.dotnet:$PATH
Creare i collegamenti simbolici richiesti La libreria cerca i file tesseract55.dll e leptonica-1.85.0.dll nella cartella x64. Con l'installazione di Ubuntu i file hanno nomi diversi (ad es. libtesseract.so.5 e liblept.so.5). Creare dei link nella cartella TesseractOCR/x64:

sudo ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract55.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.85.0.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
Assicurarsi che questi file siano copiati accanto ai binari compilati (ad es. bin/Debug/net8.0/x64). Se non vengono copiati automaticamente dal build, copiarli manualmente dopo la compilazione.

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

The following timings were captured while converting the PDF samples from Docling's `tests/data` directory. Image samples (TIFF and PNG) could not be processed in this environment because the Leptonica runtime was unavailable.

| File | Type | Markdown ms | BBox ms |
| --- | --- | --- | --- |
| 2203.01017v2.pdf | pdf | 1756.00 | 223.11 |
| 2206.01062.pdf | pdf | 927.07 | 52.02 |
| 2305.03393v1-pg9.pdf | pdf | 62.26 | 3.74 |
| 2305.03393v1.pdf | pdf | 333.04 | 28.77 |
| amt_handbook_sample.pdf | pdf | 167.56 | 4.37 |
| code_and_formula.pdf | pdf | 55.00 | 7.73 |
| multi_page.pdf | pdf | 95.89 | 10.01 |
| picture_classification.pdf | pdf | 30.85 | 4.90 |
| redp5110_sampled.pdf | pdf | 373.09 | 27.89 |
| right_to_left_01.pdf | pdf | 34.93 | 1.57 |
| right_to_left_02.pdf | pdf | 24.03 | 1.48 |
| right_to_left_03.pdf | pdf | 45.60 | 1.25 |

| Type | Avg Markdown ms | Avg BBox ms |
| --- | --- | --- |
| pdf | 325.44 | 30.57 |
| **Overall** | 325.44 | 30.57 |

### Comparison with markitdown timings

The [markitdown](https://github.com/mapo80/markitdown) project reports Docling dataset timings in seconds. Comparing the published averages shows that MarkItDownNet processes the PDF samples substantially faster:

| Type | markitdown MD&nbsp;s | markitdown BBox&nbsp;s | MarkItDownNet MD&nbsp;s | MarkItDownNet BBox&nbsp;s |
| --- | --- | --- | --- | --- |
| pdf | 3.29 | 5.14 | 0.33 | 0.03 |
| png | 2.51 | 5.56 | – | – |
| tiff | 2.57 | 4.19 | – | – |
| **Overall** | 3.18 | 5.10 | 0.33 | 0.03 |

On the PDF samples, MarkItDownNet completed Markdown conversion about **10×** faster and bounding box generation roughly **170×** faster than markitdown. Image timings are unavailable here because the Leptonica runtime was missing.

## License

MIT
