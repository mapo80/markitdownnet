# OCR Benchmark

**Baseline: GT-shared (pytesseract-cli)**

## Global
| engine | CER | Token-F1 | line_F1 | n_files |
| markitdownnet | 0.0000 | 1.0000 | 1.0000 | 24 |
| markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 24 |
| pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 24 |

## By dataset
| dataset | engine | CER | Token-F1 | line_F1 | n_files |
| PUBTABLES | markitdownnet | 0.0000 | 1.0000 | 1.0000 | 8 |
| PUBTABLES | markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 8 |
| PUBTABLES | pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 8 |
| ICDAR | markitdownnet | 0.0000 | 1.0000 | 1.0000 | 4 |
| ICDAR | markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| ICDAR | pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| MARMOT | markitdownnet | 0.0000 | 1.0000 | 1.0000 | 4 |
| MARMOT | markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| MARMOT | pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| SROIE2019 | markitdownnet | 0.0000 | 1.0000 | 1.0000 | 4 |
| SROIE2019 | markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| SROIE2019 | pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| FUNSD | markitdownnet | 0.0000 | 1.0000 | 1.0000 | 4 |
| FUNSD | markitdownnet-cli | 0.0000 | 1.0000 | 1.0000 | 4 |
| FUNSD | pytesseract-cli | 0.0000 | 1.0000 | 1.0000 | 4 |

### Legacy comparison (informativa)

#### Global
| engine | CER | Token-F1 | line_F1 | n_files |
| markitdownnet | 0.2334 | 0.7043 | 0.3870 | 24 |
| markitdownnet-cli | 0.2334 | 0.7043 | 0.3870 | 24 |
| pytesseract-cli | 0.2334 | 0.7043 | 0.3870 | 24 |

#### By dataset
| dataset | engine | CER | Token-F1 | line_F1 | n_files |
| PUBTABLES | markitdownnet | 0.2633 | 0.5715 | 0.0899 | 8 |
| PUBTABLES | markitdownnet-cli | 0.2633 | 0.5715 | 0.0899 | 8 |
| PUBTABLES | pytesseract-cli | 0.2633 | 0.5715 | 0.0899 | 8 |
| ICDAR | markitdownnet | 0.7446 | 0.3681 | 0.0000 | 4 |
| ICDAR | markitdownnet-cli | 0.7446 | 0.3681 | 0.0000 | 4 |
| ICDAR | pytesseract-cli | 0.7446 | 0.3681 | 0.0000 | 4 |
| MARMOT | markitdownnet | 0.0786 | 0.8320 | 0.4067 | 4 |
| MARMOT | markitdownnet-cli | 0.0786 | 0.8320 | 0.4067 | 4 |
| MARMOT | pytesseract-cli | 0.0786 | 0.8320 | 0.4067 | 4 |
| SROIE2019 | markitdownnet | 0.0498 | 0.8829 | 0.7352 | 4 |
| SROIE2019 | markitdownnet-cli | 0.0498 | 0.8829 | 0.7352 | 4 |
| SROIE2019 | pytesseract-cli | 0.0498 | 0.8829 | 0.7352 | 4 |
| FUNSD | markitdownnet | 0.0011 | 1.0000 | 1.0000 | 4 |
| FUNSD | markitdownnet-cli | 0.0011 | 1.0000 | 1.0000 | 4 |
| FUNSD | pytesseract-cli | 0.0011 | 1.0000 | 1.0000 | 4 |

## Run config
- baseline: "pytesseract-cli"
- os: "Ubuntu 24.04.2 LTS"
- cpu: "Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz"
- dotnet: "9.0.0"
- langs: "eng"
- psm: "6"
- threads: "1"
- timings_unit: "ms"
- engines: ["markitdownnet","markitdownnet-cli","pytesseract-cli"]
- txt_variant_used: {"pytesseract-cli":"mdready","markitdownnet":"mdready","markitdownnet-cli":"mdready"}
- markitdownnet: {"tesseract_version":"5.3.4","leptonica_version":"leptonica-1.82.0","tessdata_path":"/usr/share/tesseract-ocr/5/tessdata","eng_checksum":"7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"}
- markitdownnet-cli: {"tesseract_version":"tesseract 5.3.4","leptonica_version":"leptonica-1.82.0","tessdata_path":"/usr/share/tesseract-ocr/5/tessdata","eng_checksum":"7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"}
- pytesseract-cli: {"pytesseract_version":"0.3.13","tesseract_cmd":""}
