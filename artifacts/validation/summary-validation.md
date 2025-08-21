## Run config
- OS: Ubuntu 24.04.2 LTS (Intel(R) Xeon(R) Platinum 8272CL CPU @ 2.60GHz)
- .NET: 9.0.0
- Python: Python 3.12.10 markitdown 0.1.2
- threads bench: 1
- Tesseract: eng+ita psm 6

## Timing
### Global
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.1|0.1|
|post-1S|0.4±0.5|0.3|0.6|0.8|
|post-2|0.7±1.9|0.5|0.8|1.2|
|python-hot|7.1±2.7|6.4|8.9|13.3|
Δ post-2 vs post-1S: 77.1%
Δ .NET vs python-hot: -89.4%

### FUNSD
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.6±1.0|0.3|0.5|0.8|
|post-2|0.5±0.1|0.5|0.7|0.7|
|python-hot|6.0±1.1|5.7|7.7|7.9|
Δ post-2 vs post-1S: -9.5%  Δ .NET vs python-hot: -91.4%

### ICDAR
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.1|
|post-1S|0.5±0.5|0.4|0.5|0.7|
|post-2|1.6±4.1|0.6|0.8|1.8|
|python-hot|8.9±3.3|7.3|15.0|15.5|
Δ post-2 vs post-1S: 199.0%  Δ .NET vs python-hot: -82.3%

### MARMOT
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.1±0.1|0.0|0.1|0.2|
|post-1S|0.8±0.5|0.6|1.6|1.9|
|post-2|1.5±1.5|0.7|4.2|4.3|
|python-hot|6.8±2.2|6.3|9.1|11.8|
Δ post-2 vs post-1S: 99.9%  Δ .NET vs python-hot: -77.4%

### PUBTABLES
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.2±0.1|0.2|0.3|0.3|
|post-2|0.2±0.1|0.2|0.3|0.3|
|python-hot|6.8±2.0|6.6|8.6|10.6|
Δ post-2 vs post-1S: 9.3%  Δ .NET vs python-hot: -97.3%

### SROIE2019
|Mode|avg±std (ms)|p50|p90|p95|
|---|---|---|---|---|
|pre|0.0±0.0|0.0|0.0|0.0|
|post-1S|0.3±0.1|0.3|0.5|0.5|
|post-2|0.5±0.1|0.5|0.6|0.6|
|python-hot|6.9±3.8|5.9|7.2|8.4|
Δ post-2 vs post-1S: 44.8%  Δ .NET vs python-hot: -93.2%

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
- Δ post-2 vs post-1S: 77.1%
- Δ .NET vs python-hot: -89.4%
- Global Token-F1 (post-2): 0.981
- Global CER (post-2): 0.020
- Tables detected (post-2): 0.2
