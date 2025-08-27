# Validation Markdown Comparison

| Dataset | Image | .NET size (bytes) | Python size (bytes) | .NET time (ms) | Python time (ms) | Notes |
|--------|-------|------------------:|--------------------:|---------------:|-----------------:|-------|
| FUNSD | 82092117.png | 63 | 148 | 3821 | 5389 | Python includes figure HTML; .NET minimal text |
| ICDAR | cTDaR_t00014.jpg | 103 | 149 | 3264 | 4019 | Both outputs largely empty; Python adds image tag |
| MARMOT | 10.1.1.1.2006_3.jpeg | 5212 | 4671 | 12586 | 14188 | .NET produces slightly larger table markdown |
| PUBTABLES | PMC1064078_table_0.jpg | 687 | 1363 | 2467 | 3106 | Python markup doubles size with figure wrapper |
| SROIE2019 | X00016469670.jpg | 119 | 148 | 2938 | 2911 | Outputs comparable; structures differ in ordering |

Times measured with local runs of RapidStructure (v5 OCR) and PPStructureV3 `save_to_markdown` (paddlepaddle 3.0.0 + paddlex[ocr]).
