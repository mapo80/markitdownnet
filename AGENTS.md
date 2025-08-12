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
- On Ubuntu 24.04 install Tesseract 5.x and the [`libleptonica-dev_1.82.0-3build4` package](https://ubuntu.pkgs.org/24.04/ubuntu-universe-amd64/libleptonica-dev_1.82.0-3build4_amd64.deb.html). The `TesseractOCR` wrapper expects `libtesseract55.dll.so` and `libleptonica-1.85.0.dll.so`; create symlinks to the system libraries:

```
sudo apt-get install -y tesseract-ocr libleptonica-dev
sudo ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.85.0.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract55.dll.so
sudo ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
```
