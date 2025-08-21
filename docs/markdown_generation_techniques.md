# Markdown Generation Techniques

This document summarizes the strategies used by the Python **markitdown** library and the current state of the .NET **MarkItDownNet** port.

## markitdown (Python)
- **Format detection** via [Magika](https://github.com/google/magika) and mimetype/extension hints.
- **Converter chain** per format (PDF via `pdfminer.six`, DOCX via `mammoth`, HTML via `markdownify`, etc.).
- **Image handling** adds EXIF metadata and optionally calls a multimodal LLM for captions; it does *not* perform OCR.
- **Plain text** files are returned as-is without structural heuristics.

## MarkItDownNet (.NET)
- **Tesseract OCR** for images and PDFs with low native text.
- **Bounding boxes** for pages, lines and words.
- **Paragraph gap heuristic** to insert blank lines between distant lines.
- **Bullet/numbered list detection** to emit proper Markdown list syntax.
- **Line merging** joins consecutive OCR lines into coherent paragraphs.
- **Optional normalization** of the final Markdown through Markdig.

## Missing features compared to markitdown
- Heading detection and table recognition to better structure complex layouts.
- EXIF metadata extraction.

## Outlook
Focusing on additional structural heuristics (headings, tables) and metadata handling will further align MarkItDownNet with markitdown’s formatting quality.
