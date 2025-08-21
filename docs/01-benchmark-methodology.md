# Benchmark methodology

1. Generate reference text:
   ```bash
   tesseract dataset/busta_paga_internet.jpeg artifacts/busta_paga_internet -l ita
   ```
2. Convert modes:
   - `pre` : baseline formatting
   - `post-v0` : heuristic set v0
   - `post-v01` : heuristic set v01 (tables etc.)
   - `python` : `python -m markitdown artifacts/busta_paga_internet.txt -o artifacts/outputs/busta_paga_internet.python.md`
3. Benchmark:
   ```bash
   markitdownnet bench --input artifacts/busta_paga_internet.txt \
     --modes pre,post-v0,post-v01,python \
     --out-json artifacts/bench-v01.json --out-html artifacts/bench-v01.html \
     --summary-md artifacts/summary-v01.md --config markitdownnet.json
   ```

## Metrics
- CER = Levenshtein / len(python)
- Token F1 via whitespace tokens.
- Structural counts: heading levels, list items, max list depth, code blocks, horizontal rules, tables.
- Heading match ratio using Jaccard >=0.7.
- Table cell-F1 comparing cell-by-cell with Python tables.
