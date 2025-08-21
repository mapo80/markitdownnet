## Timing summary (md_ms)
| mode | avg md_ms | std md_ms |
| --- | --- | --- |
| pre | 0.1 | 0.0 |
| post-1R | 0.4 | 0.1 |
| python-cold | 1987.2 | 18.2 |
| python-hot | 3.8 | 0.4 |

post-1R vs pre: 290.4%

post-1R vs python-hot: -88.4%

## Quality vs python-hot
| mode | CER | Token-F1 | line_count | list_items |
| --- | --- | --- | --- | --- |
| pre | 0.001 | 1.000 | 36 | 0 |
| post-1R | 0.007 | 1.000 | 29 | 0 |

### Observations
- CER pre 0.001 vs post-1R 0.007
- line_count pre 36, post-1R 29, python 37
- post-1R overhead vs pre 290.4%
