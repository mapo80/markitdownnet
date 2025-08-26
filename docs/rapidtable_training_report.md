# RapidTableNet training dataset analysis

Model: SlanetPlus

## Summary

| Image | Tables | Layout ms |
| --- | --- | --- |
| busta_paga_internet.jpeg | 1 | 401.6 |
| sample_invoice.jpg | 1 | 204.0 |
| sample_invoice.png | 1 | 121.9 |

## Detailed results

### busta_paga_internet.jpeg
Layout time: 401.6 ms
Detected tables: 1

| Table | Bounding Box [x1,y1,x2,y2] | Recognition ms | Cells | Tokens |
| --- | --- | --- | --- | --- |
| 1 | [2,0,628,780] | 413.8 | 72 | 259 |

Output file: `dataset/training/busta_paga_internet.jpeg.rapidtable.json`

### sample_invoice.jpg
Layout time: 204.0 ms
Detected tables: 1

| Table | Bounding Box [x1,y1,x2,y2] | Recognition ms | Cells | Tokens |
| --- | --- | --- | --- | --- |
| 1 | [58,312,1195,560] | 274.9 | 16 | 28 |

Output file: `dataset/training/sample_invoice.jpg.rapidtable.json`

### sample_invoice.png
Layout time: 121.9 ms
Detected tables: 1

| Table | Bounding Box [x1,y1,x2,y2] | Recognition ms | Cells | Tokens |
| --- | --- | --- | --- | --- |
| 1 | [58,312,1195,560] | 149.0 | 16 | 28 |

Output file: `dataset/training/sample_invoice.png.rapidtable.json`

