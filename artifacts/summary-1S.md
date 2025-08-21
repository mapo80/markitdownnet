## Timing summary (md_ms)
| mode | avg md_ms | std md_ms |
| --- | --- | --- |
| pre | 0.1 | 0.0 |
| post-1R | 0.6 | 0.1 |
| post-1S | 0.6 | 0.0 |
| python-hot | 4.1 | 0.2 |
| python-cold | 2044.9 | 32.7 |

post-1R vs pre: 577.3%

post-1S vs post-1R: -4.8%

post-1S vs python-hot: -86.3%

## Quality vs python-hot
| mode | CER | Token-F1 | line_F1 | line_count | list_items | pipes_lines | median_pipes | max_pipes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| pre | 0.001 | 1.000 | 0.986 | 36 | 0 | 16 | 2.5 | 6 |
| post-1R | 0.007 | 1.000 | 0.758 | 29 | 0 | 15 | 3.0 | 6 |
| post-1S | 0.001 | 1.000 | 0.986 | 36 | 0 | 16 | 2.5 | 6 |

### Observations
- CER pre 0.001 vs post-1S 0.001
- line_F1 post-1S 0.986 vs post-1R 0.758
- post-1S overhead vs pre 544.7%
