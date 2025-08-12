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
- On Ubuntu 24.04 obtain Tesseract 5.x and Leptonica 1.85 binaries (e.g. from the [jitesoft/docker-tesseract-ocr](https://github.com/jitesoft/docker-tesseract-ocr/pkgs/container/tesseract/) packages). The `TesseractOCR` wrapper looks for `libtesseract55.dll.so` and `libleptonica-1.85.0.dll.so`; creating symlinks to system libraries is sufficient.
