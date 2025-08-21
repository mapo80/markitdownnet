# Benchmark methodology

1. Generate reference text:
   ```bash
   tesseract dataset/busta_paga_internet.jpeg artifacts/busta_paga_internet -l ita
   ```
2. Convert modes:
   - pre: `markitdownnet convert --input artifacts/busta_paga_internet.txt --mode pre --out artifacts/outputs/busta_paga_internet.pre.md --config markitdownnet.json`
   - post: same with `--mode post`.
   - python: `python -m markitdown artifacts/busta_paga_internet.txt -o artifacts/outputs/busta_paga_internet.python.md`
3. Benchmark:
   ```bash
   markitdownnet bench --input artifacts/busta_paga_internet.txt --modes pre,post,python --out-json artifacts/bench-v0.json --out-html artifacts/bench-v0.html --config markitdownnet.json
   ```

## Metrics
- CER = Levenshtein / len(python)
- Token F1 via whitespace tokens.
- Structural counts for headings, list items, code blocks, horizontal rules.
- Heading match ratio using Jaccard >=0.7.
