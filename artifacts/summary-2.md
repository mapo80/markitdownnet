## Timing summary (md_ms)
| mode | avg md_ms | std md_ms |
| --- | --- | --- |
| pre | 0.2 | 0.0 |
| post-1S | 0.7 | 0.1 |
| post-2 | 1.3 | 0.4 |
| python-hot | 12.9 | 5.7 |
| python-cold | 3028.5 | 64.5 |

post-1S vs pre: 368.5%

post-2 vs post-1S: 78.5%

post-2 vs python-hot: -89.9%

## Quality vs python-hot
| mode | CER | Token-F1 | line_F1 | line_count | list_items | pipes_lines | median_pipes | max_pipes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| pre | 0.001 | 1.000 | 0.986 | 36 | 0 | 16 | 2.5 | 6 |
| post-1S | 0.001 | 1.000 | 0.986 | 36 | 0 | 16 | 2.5 | 6 |
| post-2 | 0.001 | 1.000 | 0.986 | 36 | 0 | 16 | 2.5 | 6 |

### Observations
- CER pre 0.001 vs post-2 0.001
- line_F1 post-2 0.986 vs post-1S 0.986
- post-2 overhead vs pre 736.4%
