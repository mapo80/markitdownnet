# v01 heuristics

Adds table extraction and heading/list refinements on top of v0:

- Key-value blocks (>=3 rows) converted to pipe tables.
- Monospace tables detected from columns separated by multiple spaces with right alignment for numeric columns.
- Hardened bullets/HR and heading promotion (no `:` headings unless blank line follows; special payroll keywords promoted to H2).
- Existing v0 features: paragraph reflow, dehyphenation, list detection, heading promotion, code fences, horizontal rules.

Configuration fields in `markitdownnet.json` control thresholds like `KeyMaxLen`, `MonoTableMinCols`, `MonoTableMinRows` and others.
