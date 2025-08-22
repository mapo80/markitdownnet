#!/usr/bin/env bash
set -euo pipefail
log(){ printf "[%(%Y-%m-%dT%H:%M:%SZ)T] %s\n" -1 "$*"; }
TXT_DIR="dataset/validation/_ocr/pytesseract-cli"
MD_DIR="dataset/validation/_md"

log "STEP 0: Manifest check (create if missing)"
if [[ ! -f artifacts/mdbench/mdready.manifest.json ]]; then
  ~/.dotnet/dotnet run --project tools/MarkItDownNet.Cli -- \
    mdmanifest --txt-dir "$TXT_DIR" --out artifacts/mdbench/mdready.manifest.json
else
  ~/.dotnet/dotnet run --project tools/MarkItDownNet.Cli -- \
    mdcheck --txt-dir "$TXT_DIR" --manifest artifacts/mdbench/mdready.manifest.json
fi

log "STEP 1: mdgen (markitdown + markitdownnet)"
~/.dotnet/dotnet run --project tools/MarkItDownNet.Cli -- \
  mdgen --txt-dir "$TXT_DIR" --out-dir "$MD_DIR" --engines markitdown,markitdownnet --python-exe python3

log "STEP 2: mdcompare STRICT"
~/.dotnet/dotnet run --project tools/MarkItDownNet.Cli -- \
  mdcompare --md-dir "$MD_DIR" --baseline markitdown \
  --out-json artifacts/mdbench/bench-md.json \
  --out-html artifacts/mdbench/bench-md.html \
  --summary-md artifacts/mdbench/summary-md.md \
  --strict true

log "STEP 3: COMMIT OBBLIGATORIO risultati"
git add -A dataset/validation/_md artifacts/mdbench tools/scripts/md_parity_strict.sh
ts=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
git commit -m "MD parity STRICT: hashes & metrics identical (baseline=markitdown) — ${ts}"
git show --stat -1
