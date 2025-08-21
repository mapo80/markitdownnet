## Run config
- OS: Ubuntu 24.04.2 LTS (X64)
- .NET: 9.0.0
- Python: Python 3.12.10 markitdown 0.1.2
- threads bench: 1
- Tesseract: eng+ita psm 6

## Timing
### Global
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.1|0.2|0.4|0.5|
|post-2|0.3±0.3|0.3|0.6|0.7|
|python-hot|4.2±0.8|4.1|5.4|5.8|
Δ post-2 vs post-1S: 46.3%
Δ .NET vs python-hot: -92.1%

### FUNSD
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.0|0.2|0.3|0.3|
|post-2|0.3±0.1|0.2|0.4|0.4|
|python-hot|4.2±0.8|4.1|5.2|5.6|
Δ post-2 vs post-1S: 24.1%  Δ .NET vs python-hot: -93.6%

### ICDAR
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.3±0.1|0.3|0.5|0.5|
|post-2|0.4±0.1|0.3|0.6|0.6|
|python-hot|4.6±0.7|4.6|5.5|5.6|
Δ post-2 vs post-1S: 19.0%  Δ .NET vs python-hot: -92.1%

### MARMOT
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.3±0.1|0.3|0.4|0.4|
|post-2|0.6±0.7|0.3|1.8|1.8|
|python-hot|4.5±0.9|4.3|6.1|6.3|
Δ post-2 vs post-1S: 139.3%  Δ .NET vs python-hot: -85.6%

### PUBTABLES
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.1|
|post-1S|0.2±0.2|0.1|0.5|0.6|
|post-2|0.2±0.2|0.1|0.7|0.7|
|python-hot|4.0±0.9|3.7|5.3|5.6|
Δ post-2 vs post-1S: 30.6%  Δ .NET vs python-hot: -94.3%

### SROIE2019
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.1|0.2|0.3|0.3|
|post-2|0.3±0.1|0.3|0.3|0.3|
|python-hot|4.0±0.6|4.0|5.1|5.1|
Δ post-2 vs post-1S: 18.4%  Δ .NET vs python-hot: -93.4%

## Quality
### Global
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|0.999|
|post-1S|0.006|1.000|0.797|
|post-2|0.020|0.981|0.777|
|python-hot|0.000|1.000|1.000|

### FUNSD
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|0.994|
|post-1S|0.011|0.999|0.704|
|post-2|0.036|0.962|0.663|
|python-hot|0.000|1.000|1.000|

### ICDAR
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|1.000|
|post-1S|0.001|1.000|0.939|
|post-2|0.001|1.000|0.939|
|python-hot|0.000|1.000|1.000|

### MARMOT
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|1.000|
|post-1S|0.003|1.000|0.831|
|post-2|0.003|1.000|0.831|
|python-hot|0.000|1.000|1.000|

### PUBTABLES
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|1.000|
|post-1S|0.007|0.999|0.758|
|post-2|0.007|0.999|0.758|
|python-hot|0.000|1.000|1.000|

### SROIE2019
|Mode|CER|Token-F1|line_F1|
|---|---|---|---|
|pre|0.000|1.000|1.000|
|post-1S|0.007|1.000|0.795|
|post-2|0.063|0.927|0.714|
|python-hot|0.000|1.000|1.000|

## Tables
|Mode|tables_count|table_cell_F1 (post-2)|
|---|---|---|
|pre|0.0||
|post-1S|0.0||
|post-2|0.2||
|python-hot|0.0||

## Key findings
- Δ post-2 vs post-1S: 46.3%
- Δ .NET vs python-hot: -92.1%
- Global Token-F1 (post-2): 0.981
- Global CER (post-2): 0.020
- Tables detected (post-2): 0.2
