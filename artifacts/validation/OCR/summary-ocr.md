# OCR Benchmark (markitdownnet vs pytesseract)

## Global
| scope | CER | Token-F1 | line_F1 | n_files |
| Global | 0.2447 | 0.6954 | 0.3794 | 24 |

## By dataset
| scope | CER | Token-F1 | line_F1 | n_files |
| MARMOT | 0.0807 | 0.8319 | 0.4025 | 4 |
| SROIE2019 | 0.1066 | 0.8367 | 0.6941 | 4 |
| ICDAR | 0.7281 | 0.3611 | 0.0000 | 4 |
| PUBTABLES | 0.2689 | 0.5715 | 0.0899 | 8 |
| FUNSD | 0.0148 | 1.0000 | 1.0000 | 4 |

## Top-5 worst files
| dataset/file | cer_char | token_f1 | line_f1 | note |
| ICDAR/cTDaR_t00015 | 0.7768 | 0.3080 | 0.0000 | |
| ICDAR/cTDaR_t00080 | 0.7648 | 0.3690 | 0.0000 | |
| ICDAR/cTDaR_t00016 | 0.7078 | 0.4268 | 0.0000 | |
| PUBTABLES/PMC1064082_table_0 | 0.6912 | 0.2000 | 0.0000 | |
| ICDAR/cTDaR_t00014 | 0.6631 | 0.3406 | 0.0000 | |

## Run config
- os: Ubuntu 24.04.2 LTS
- cpu: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz
- dotnet: 9.0.0
- python: Python 3.12.10
- tesseract: tesseract 5.3.4
- langs: eng
- psm: 6
- threads: 1
- timings_unit: ms
