# Busta paga comparison

Benchmarks on `dataset/busta_paga_internet.jpeg` using Tesseract text from `artifacts/busta_paga_internet.txt`.

| mode  | avg ms | std ms | CER vs python | token F1 |
|------|-------:|-------:|-------------:|---------:|
| pre  | 3.4    | 6.8    | 0.0008       | 0.78 |
| post | 1.8    | 3.6    | 0.0202       | 0.78 |
| python | 2885.8 | 89.5 | – | – |

Outputs are stored in `artifacts/outputs/`.
