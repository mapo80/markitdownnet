# MarkItDownNet – agent notes

## Install the .NET 9 SDK locally if needed:
```bash
  ./dotnet-install.sh --version 9.0.100 --install-dir "$HOME/dotnet"
  export PATH="$HOME/dotnet:$PATH"
```
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
Le librerie native minime per Linux `x64` sono disponibili in `src/MarkItDownNet/TesseractOCR/x64` e vengono copiate accanto ai binari (`x64`) ad eccezione di `libdl.so`, posizionata in `runtimes/linux-x64/native`:

* `libopenjp2.so.7`
* `liblept.so.5` con il symlink `libleptonica-1.85.0.dll.so`
* `libtesseract.so.5` con il symlink `libtesseract55.dll.so`
* `libdl.so`

Grazie a queste dipendenze la libreria è auto‑consistente e **non richiede l'installazione di Tesseract o Leptonica**.

Per l'OCR servono solo i dati delle lingue. Su Ubuntu 24.04 possono essere installati con:

```bash
sudo apt-get install -y tesseract-ocr-eng tesseract-ocr-ita tesseract-ocr-osd
```

Indicare quindi il percorso tramite `OcrDataPath`.
