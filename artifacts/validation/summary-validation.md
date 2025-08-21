## Run config
- OS: Ubuntu 24.04.2 LTS (Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz)
- .NET: 9.0.0
- Python: Python 3.12.10 markitdown 0.1.2
- threads bench: 1
- Tesseract: eng+ita psm 6

## Timing
### Global
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.2|0.2|0.4|0.4|
|post-2|0.4±0.6|0.2|0.9|1.4|
|python-hot|4.4±0.8|4.3|5.2|5.3|
Δ post-2 vs post-1S: 93.1%
Δ .NET vs python-hot: -90.2%

### FUNSD
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.0|0.2|0.2|0.2|
|post-2|0.9±1.2|0.2|2.2|2.6|
|python-hot|4.8±1.0|4.7|6.0|6.2|
Δ post-2 vs post-1S: 376.0%  Δ .NET vs python-hot: -81.3%

### ICDAR
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.1±0.0|0.0|0.1|0.1|
|post-1S|0.5±0.2|0.4|0.7|0.8|
|post-2|0.6±0.3|0.5|0.9|1.0|
|python-hot|4.8±0.4|4.8|5.2|5.3|
Δ post-2 vs post-1S: 36.0%  Δ .NET vs python-hot: -87.1%

### MARMOT
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.3±0.1|0.3|0.4|0.4|
|post-2|0.6±0.5|0.3|1.1|1.3|
|python-hot|4.1±0.7|3.8|4.8|5.1|
Δ post-2 vs post-1S: 105.0%  Δ .NET vs python-hot: -86.4%

### PUBTABLES
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.1±0.1|0.1|0.2|0.2|
|post-2|0.1±0.1|0.1|0.2|0.2|
|python-hot|4.3±0.6|4.3|5.0|5.1|
Δ post-2 vs post-1S: 16.3%  Δ .NET vs python-hot: -97.1%

### SROIE2019
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.0|0.2|0.2|0.3|
|post-2|0.2±0.0|0.2|0.3|0.3|
|python-hot|3.9±0.5|3.9|4.5|4.6|
Δ post-2 vs post-1S: 20.5%  Δ .NET vs python-hot: -93.8%

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
- Δ post-2 vs post-1S: 93.1%
- Δ .NET vs python-hot: -90.2%
- Global Token-F1 (post-2): 0.981
- Global CER (post-2): 0.020
- Tables detected (post-2): 0.2
