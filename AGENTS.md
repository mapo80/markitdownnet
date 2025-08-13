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
```

- Verify the installation:

```
tesseract --version  # should report leptonica 1.82.0 and tesseract 5.x
```
