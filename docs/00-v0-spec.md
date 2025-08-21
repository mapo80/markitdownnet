# v0 spec

## Heuristics
1. Normalize line endings and collapse extra spaces.
2. Reflow paragraphs and dehyphenate, stopping before bullets/numbers, table-like lines or code-like lines, and when the previous line ends with `:`.
3. Detect bullet and numbered lists; normalize bullets to `- ` and numbers to `1.`; wrap items if the next line is indented; single isolated items fall back to paragraphs.

## Pipeline
Input text from Tesseract -> optional heuristics (post mode) -> Markdown.

## Limitations
- Headings, tables, code fences and horizontal rules are not yet generated.
- Heuristics are rule based, may mis-detect complex structures.
- Only plain text input supported.
