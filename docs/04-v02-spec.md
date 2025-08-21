# v0.2 spec

Euristiche aggiunte rispetto a v0.1:

1. **Reflow robusto** – evita di unire righe dopo `:` o prima di `Key:`/liste; dehyphenation solo se la parola successiva inizia in minuscolo.
   - `"Fine-\nanno"` → `"Fineanno"`
2. **Liste hardening** – supporto bullet “– — ·”, continuazioni e numerazione coerente.
   - `"– voce"` → `"- voce"`
3. **Headings precisione** – blocca titoli con `:` seguiti da testo e promuove parole chiave tipiche delle paghe.
   - `"Riepilogo"` → `"## Riepilogo"`
4. **Key:Value → tabella** – valori su più righe e controllo key con punto.
   - `"Nome: Mario\n  Rossi"` → tabella `| Nome | Mario Rossi |`
5. **Tabelle monospace** – rimozione colonne vuote e allineamento numerico.
   - `"A  10\nB  20"` → `| A | 10 |\n| B | 20 |`
6. **HR precisione** – linee con caratteri identici e fuori dalle liste.
   - `"---"` rimane regola orizzontale
