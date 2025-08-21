## Timing summary (md_ms)
| mode | avg md_ms | std md_ms |
| --- | --- | --- |
| pre | 0.1 | 0.0 |
| post-1S | 0.7 | 0.1 |
| post-2 | 1.4 | 0.2 |
| python-hot | 6.7 | 0.9 |
| python-cold | 3434.2 | 130.6 |

post-1S vs pre: 494.1%

post-2 vs post-1S: 91.7%

post-2 vs python-hot: -79.7%

## Quality vs python-hot
| mode | CER | Token-F1 | line_F1 | tables_count | line_count | list_items | pipes_lines | median_pipes | max_pipes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| pre | 0.001 | 1.000 | 0.986 | 0 | 36 | 0 | 16 | 2.5 | 6 |
| post-1S | 0.001 | 1.000 | 0.986 | 0 | 36 | 0 | 16 | 2.5 | 6 |
| post-2 | 0.001 | 1.000 | 0.986 | 0 | 36 | 0 | 16 | 2.5 | 6 |

**Tables:** none detected in this sample (all modes).

### Observations
- CER pre 0.001 vs post-2 0.001
- line_F1 post-2 0.986 vs post-1S 0.986
- post-2 overhead vs pre 1038.9%
