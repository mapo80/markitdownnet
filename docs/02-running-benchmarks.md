# Running benchmarks

```bash
# 1. ensure dependencies
apt-get install -y tesseract-ocr tesseract-ocr-ita
pip install 'markitdown[all]'

# 2. generate OCR text
tesseract dataset/busta_paga_internet.jpeg artifacts/busta_paga_internet -l ita

# 3. run conversions
./dotnet-install.sh --version 9.0.100 --install-dir "$HOME/dotnet"
export PATH="$HOME/dotnet:$PATH"
dotnet run --project tools/MarkItDownNet.Cli -- convert --input artifacts/busta_paga_internet.txt --mode pre --out artifacts/outputs/busta_paga_internet.pre.md --config markitdownnet.json
dotnet run --project tools/MarkItDownNet.Cli -- convert --input artifacts/busta_paga_internet.txt --mode post-v0 --out artifacts/outputs/busta_paga_internet.post-v0.md --config markitdownnet.json
dotnet run --project tools/MarkItDownNet.Cli -- convert --input artifacts/busta_paga_internet.txt --mode post-v01 --out artifacts/outputs/busta_paga_internet.post-v01.md --config markitdownnet.json
python -m markitdown artifacts/busta_paga_internet.txt -o artifacts/outputs/busta_paga_internet.python.md

# 4. benchmarking
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet run --project tools/MarkItDownNet.Cli -- bench \
  --input artifacts/busta_paga_internet.txt \
  --modes pre,post-v0,post-v01,python \
  --out-json artifacts/bench-v01.json --out-html artifacts/bench-v01.html \
  --summary-md artifacts/summary-v01.md --config markitdownnet.json
```

Artifacts are written under `artifacts/`.
