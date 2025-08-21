# v0.3 spec

Euristiche aggiunte rispetto a v0.2:

1. **Header/Footer & paginazione** – rimuove linee ripetute e inserisce `---` per marker pagina.
   - `"Pagina 1"` → `---`
2. **Reflow sicuro** – evita unioni dopo `:` o prima di liste, tabelle o codice; dehyphenation più prudente.
   - `"fine-\nanno"` → `"fineanno"`
3. **Liste generiche** – riconosce bullet multipli e numeri/romani, ignorando date/importi.
   - `"• voce"` → `"- voce"`
4. **Headings tipografici** – promuove linee isolate Title Case o ALL CAPS.
   - `"Dettagli Generali"` → `"# Dettagli Generali"`
5. **Key:Value → tabella** – valori su più righe con connettivi `di/of/for/per/con`.
   - `"Nome: Mario\n  Rossi"` → `| Nome | Mario Rossi |`
6. **Tabelle monospace** – riconosce colonne da spazi e allinea numeri a destra.
   - `"A  10\nB  20"` → `| A | 10 |\n| B | 20 |`
7. **Code e HR robuste** – blocchi simbolici in `````` e linee `---` fuori dalle liste.
   - `"----"` → `---`
