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
Le dipendenze native minime per Linux `x64` (Tesseract e Leptonica) sono incluse nel repository in `runtimes/linux-x64/native` e vengono copiate accanto ai binari. Non è richiesta l'installazione separata di Tesseract.

Il binding .NET di Tesseract è distribuito come pacchetto NuGet locale (`local-packages/Tesseract.5.2.0.nupkg`); `nuget.config` forza l'uso di questa sorgente.

Per l'OCR servono solo i dati delle lingue. Su Ubuntu 24.04 possono essere installati con:

```bash
sudo apt-get install -y tesseract-ocr-eng tesseract-ocr-ita tesseract-ocr-osd
```

Indicare quindi il percorso tramite `OcrDataPath`.
