# v0 spec

## Heuristics
1. Normalize line endings and spaces.
2. Reflow paragraphs and dehyphenate.
3. Detect lists with bullets/numbering.
4. Promote isolated lines to headings.
5. Fence code blocks by indentation or symbol density.
6. Replace runs of '-', '*', '_' with horizontal rules.

## Pipeline
Input text from Tesseract -> optional heuristics (post mode) -> Markdown.

## Limitations
- No metadata extraction.
- Heuristics are rule based, may mis-detect complex structures.
- Only plain text input supported.
