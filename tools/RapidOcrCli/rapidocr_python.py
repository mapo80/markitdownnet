#!/usr/bin/env python3
import sys
from pathlib import Path
from rapidocr_onnxruntime import RapidOCR

if len(sys.argv) != 2:
    sys.exit(1)

img = sys.argv[1]
here = Path(__file__).resolve()
for parent in here.parents:
    candidate = parent / "src" / "RapidOcrNet" / "models" / "v5"
    if candidate.exists():
        base = candidate
        break
else:
    base = here

ocr = RapidOCR(
    det_model_path=str(base / "Multilingual_PP-OCRv3_det_infer.onnx"),
    rec_model_path=str(base / "latin_PP-OCRv5_rec_mobile_infer.onnx"),
)
res, _ = ocr(img)
print("\n".join(text for _, text, _ in res))

