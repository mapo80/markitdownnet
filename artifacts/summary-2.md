## Timing summary (md_ms)
| mode | avg md_ms | std md_ms |
| --- | --- | --- |
| pre | 0.1 | 0.0 |
| post-1S | 0.8 | 0.1 |
| post-2 | 0.9 | 0.1 |
| python-hot | 14.0 | 8.1 |
| python-cold | 2924.3 | 58.2 |

post-1S vs pre: 613.1%

post-2 vs post-1S: 10.6%

post-2 vs python-hot: -93.4%

## Quality vs python-hot
| mode | CER | Token-F1 | line_F1 | line_count | list_items | tables_count | pipes_lines | median_pipes | max_pipes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| pre | 0.001 | 1.000 | 0.986 | 36 | 0 | 0 | 16 | 2.5 | 6 |
| post-1S | 0.001 | 1.000 | 0.986 | 36 | 0 | 0 | 16 | 2.5 | 6 |
| post-2 | 0.001 | 1.000 | 0.986 | 36 | 0 | 0 | 16 | 2.5 | 6 |

### Observations
- CER pre 0.001 vs post-2 0.001
- line_F1 post-2 0.986 vs post-1S 0.986
- post-2 overhead vs pre 688.5%
