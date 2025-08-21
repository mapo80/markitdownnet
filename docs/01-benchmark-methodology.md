# Benchmark methodology

1. Generate reference text:
   ```bash
   tesseract dataset/busta_paga_internet.jpeg artifacts/busta_paga_internet -l ita
   ```
2. Convert modes:
   - `pre` : baseline formatting
   - `post-1R` : safe reflow + robust lists
   - `python-cold` : `python -m markitdown artifacts/busta_paga_internet.txt -o artifacts/outputs/busta_paga_internet.python-cold.md`
   - `python-hot` : `python tools/run_markitdown_hot.py artifacts/busta_paga_internet.txt artifacts/outputs/busta_paga_internet.python-hot.md`
3. Benchmark:
   ```bash
   markitdownnet bench --input artifacts/busta_paga_internet.txt \
     --modes pre,post-1R,python-cold,python-hot \
     --out-json artifacts/bench-1R-hot.json --out-html artifacts/bench-1R-hot.html \
     --summary-md artifacts/summary-1R-hot.md --config markitdownnet.json \
     --python-exe python --python-markitdown-cmd "python -m markitdown" \
     --python-hot-cmd "python tools/run_markitdown_hot.py"
   ```

## Metrics
- CER = Levenshtein / len(python)
- Token F1 via whitespace tokens.
- Structural counts: heading levels, list items, max list depth, code blocks, horizontal rules, tables.
- Heading match ratio using Jaccard >=0.7.
- Table cell-F1 comparing cell-by-cell with Python tables.
