# MarkItDownNet – agent notes

## API contract
- Entry point: `MarkItDownConverter.ConvertAsync(string path, string mimeType, CancellationToken)`
- Response: `MarkItDownResult`
  - `Markdown` – normalised text
  - `Pages` – `Page(number,width,height)`
  - `Lines` – `Line(page,text,bbox)`
  - `Words` – `Word(page,text,bbox)`
- `BoundingBox` is `[x,y,w,h]` with values in `[0,1]` and a top‑left origin.

## Behaviour
- PDFs use PdfPig for text extraction. When native words are below `MinimumNativeWordThreshold`, pages are rasterised with PDFtoImage and passed to Tesseract OCR.
- Images are processed directly with Tesseract.
- SkiaSharp is used for image manipulation; avoid SixLabors.ImageSharp.
- Markdown is optionally normalised via Markdig.
- Cancellation tokens are honoured on every stage.

## Logging
- Serilog is the logging framework.
- Configure sinks and levels via `Serilog` settings (see `src/MarkItDownNet/appsettings.json`).
- Use `Serilog__MinimumLevel=Verbose` to enable detailed timings and counts.

## Operations
- Requires Tesseract binaries and language data (`TESSDATA_PREFIX`).
- Fully CPU based; no GPU dependencies.
- Build and tests must use `~/.dotnet/dotnet` from `dotnet-install.sh`.
- The `TesseractOCR` 5.5.1 wrapper is built for **Tesseract 5.x** and needs **Leptonica ≥ 1.74**.
- On Ubuntu 24.04 the `tesseract-ocr` (5.3.4 at time of writing) and `libleptonica-dev` (1.82.0) packages satisfy these requirements.
- Create stable symlinks so the SDK does not depend on exact package versions. The wrapper still probes legacy names, so link them to the stable ones:

```
sudo apt-get update
sudo apt-get install -y tesseract-ocr libleptonica-dev
sudo ln -sf /usr/lib/x86_64-linux-gnu/liblept.so /usr/lib/x86_64-linux-gnu/libleptonica.so
sudo ln -sf /usr/lib/x86_64-linux-gnu/libleptonica.so /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so
sudo ln -sf /usr/lib/x86_64-linux-gnu/libleptonica.so /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.dll.so
sudo ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract5.so
sudo ln -sf /usr/lib/x86_64-linux-gnu/libtesseract5.so /usr/lib/x86_64-linux-gnu/libtesseract55.dll.so
sudo ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
```

- Verify the installation:

```
tesseract --version  # should report leptonica 1.82.0 and tesseract 5.x
```
