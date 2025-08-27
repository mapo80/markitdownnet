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

## Python reference Markdown
- Install system Tesseract languages as above and set `TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata`.
- Install Python dependencies: `pip install 'markitdown[all]' pytesseract`.
- Generate Markdown with `python tools/markitdown_ocr.py <image_or_text> -o <out.md>`.
- When running benchmarks, the CLI automatically calls this script for `python` mode.
- For warm-start benchmarks, `python tools/run_markitdown_hot.py <text> <out.md>` loads the text once and runs five conversions, printing timing data as JSON.

## PPStructure Markdown
- Install dependencies (PPStructureV3 currently requires NumPy 1.x and PaddleX):
  ```bash
  pip install 'numpy<2' paddlepaddle==3.0.0 "paddlex[ocr]"
  ```
- Clone the reference repo once and convert an image to Markdown:
 ```bash
  git clone https://github.com/PaddlePaddle/PaddleOCR /tmp/PaddleOCR
  python - <<'PY'
  import os,sys,tempfile
  repo='/tmp/PaddleOCR'; sys.path.insert(0, repo)
  from paddleocr import PPStructureV3
  pipeline=PPStructureV3(lang='en')
  res=pipeline.predict(sys.argv[1])
  tmp=tempfile.mkdtemp()
  res[0].save_to_markdown(tmp)
  md_path=os.path.join(tmp, os.path.splitext(os.path.basename(sys.argv[1]))[0]+'.md')
  print(open(md_path,encoding='utf-8').read())
  PY <image_path>
  ```
- Tests spin up a long-lived helper that instantiates `PPStructureV3` with
  `use_chart_recognition=False` and `use_formula_recognition=False` to trim
  unused modules and reuse a single model across images. Send each image path
  over `stdin` and read a JSON blob with both layout labels and markdown.
- Use this script to compare .NET Markdown output with the Python reference.
