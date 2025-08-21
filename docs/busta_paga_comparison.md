# Confronto per `busta_paga_internet.jpeg`

Per l'immagine `dataset/busta_paga_internet.jpeg` sono stati eseguiti i seguenti passaggi:

- OCR con **pytesseract** seguito dalla conversione a Markdown con `markitdown` tramite `tools/markitdown_ocr.py`
- OCR e conversione a Markdown con **MarkItDownNet** tramite `tools/BustaPagaNet`

## Testo OCR con pytesseract

```text
Datore dilevoro: Colleboratore

Rossi Cristina Tosticiku Beata
Vitaliano Rotellini, 167 Via Venezia, 3
00128 Roma (RM) 00147 Roma (RM)
Cod.Fise. Cod.Fisc
Det Assunzione | Daw Cessazione [SmstDIEm | Scatti | ScatoPreces:| Pros. Scato:
0110812010 3° | otmsrote | ctwazoie
Caccmri Cosa FS
sirantime | Lieto |Convienza eta) fisse ona
s si | 000 000 0,00 |19792%6
Fooanase | iron | scomiane StForett | intAsso® | AceFUA. | Retsbuzione Toni:
79885 9548 910,00 TRAE
Fara Cor REST
1| ore ordinarie 1.801,13
2| Ore noniavorete 17,00 | 197926 33647
®| Lavoro supplementare 30,00 | 21,358312 64075
10|| Festività 500 | 197926 96,96
40| Ind.sost vitolalloggio su assenze 100 568 548

anni WEBCOLF.com - 2008-2017.

Cesi] corti E]

impor Lordo [Contcancocol. | Cosscoli. | Hilsorsk | H Conriuti[ AnciFieo. | Ano Anok
220985 | 2500 110,00 | 10000 | 028 048

53 Fase Amen [Fano Go | Foe Gute | Foe Pesdoe [pi a | Seta MPS:

S ore: 4200 | ore: 15,17 | ore: ore: s7a7| 20 | "stats

FrogrImpLorso | Fro.NPS Cal. | Frog CassaCalt | Pr GGMalatta [FRA Prea | TFRAnro Con. | Retibu.a TFR | FrogrRer. TFR
597601 5448 1.000,00 | 102402 | 1.689,66 | 1382423

25/0720:
```

## Testo OCR con Tesseract .NET

```text
Datore dilevoro: Colleboratore

Rossi Cristina Tosticiku Beata
Vitaliano Rotellini, 167 Via Venezia, 3
00128 Roma (RM) 00147 Roma (RM)
Cod.Fise. Cod.Fisc
Det Assunzione | Daw Cessazione [SmstDIEm | Scatti | ScatoPreces:| Pros. Scato:
0110812010 3° | otmarote | ctwazoie
Cocconi Cosi FS
siFantime | Lieto |Convvenza E fissa ora
8 si | 000 000 000 |19,792%6
Fosafase | inirune | scomione SrForett | insAsso® | AceFUA. | Retsbuzione Toni:
79885 9548 910,00 TESGE
Fara Cor REST
1| ore ordinarie 1.801,13
2| Ore noniavorate 17,00 | 197926 33647
8| Lavoro supplementare 30,00 | 21,358312 64075
10|| Festività 500 | 197928 96,96
40| Ind.sost vitolalloggio su assenze 1,00 568 548

ann WEBCOLF.com - 2008-2017.

Cesi] corta E]

Imporb Lordo [conkcancocoli | Cassa Coli | H.Lavorae | H Conhiuti [ AmotFreo. | Amo Atti
220988 25,00 110,00 | 10000 | 028 0,48

ES Fare Amerae [Fase Anno Com | Fare Goiute/ | Fe Resove [Rami aa || Setmmne RESvertsie:

S| ore: 42,00 | ore: 15,17 | ore: ore: 5717) 200 | "si°si*si*si

FrogrImpLorso | Frog.NPS Col. | Frog CassaCali | PrGGMalatta [TFRAmo Prec | TFRAnvo Con. | Retibuz.a TFR | FrogrRet. TFR
597601 ErC 1.000,00 | 102402 | 1.689,66 | 1382423

25/07/20
```

## Markdown con markitdown

```markdown
Datore dilevoro: Colleboratore

Rossi Cristina Tosticiku Beata
Vitaliano Rotellini, 167 Via Venezia, 3
00128 Roma (RM) 00147 Roma (RM)
Cod.Fise. Cod.Fisc
Det Assunzione | Daw Cessazione [SmstDIEm | Scatti | ScatoPreces:| Pros. Scato:
0110812010 3° | otmsrote | ctwazoie
Caccmri Cosa FS
sirantime | Lieto |Convienza eta) fisse ona
s si | 000 000 0,00 |19792%6
Fooanase | iron | scomiane StForett | intAsso® | AceFUA. | Retsbuzione Toni:
79885 9548 910,00 TRAE
Fara Cor REST
1| ore ordinarie 1.801,13
2| Ore noniavorete 17,00 | 197926 33647
®| Lavoro supplementare 30,00 | 21,358312 64075
10|| Festività 500 | 197926 96,96
40| Ind.sost vitolalloggio su assenze 100 568 548

anni WEBCOLF.com - 2008-2017.

Cesi] corti E]

impor Lordo [Contcancocol. | Cosscoli. | Hilsorsk | H Conriuti[ AnciFieo. | Ano Anok
220985 | 2500 110,00 | 10000 | 028 048

53 Fase Amen [Fano Go | Foe Gute | Foe Pesdoe [pi a | Seta MPS:

S ore: 4200 | ore: 15,17 | ore: ore: s7a7| 20 | "stats

FrogrImpLorso | Fro.NPS Cal. | Frog CassaCalt | Pr GGMalatta [FRA Prea | TFRAnro Con. | Retibu.a TFR | FrogrRer. TFR
597601 5448 1.000,00 | 102402 | 1.689,66 | 1382423

25/0720:
```

## Markdown con MarkItDownNet

```markdown
Datore dilevoro: Colleboratore Rossi Cristina Tosticiku Beata Vitaliano Rotellini, 167 Via Venezia, 3 00128 Roma (RM) 00147 Roma (RM) Cod.Fise. Cod.Fisc

Det Assunzione | Daw Cessazione [SmstDIEm | Scatti | ScatoPreces:| Pros. Scato: 0110812010 3° | otmarote | ctwazoie Cocconi Cosi FS siFantime | Lieto |Convvenza E fissa ora 8 si | 000 000 000 |19,792%6

Fosafase | inirune | scomione SrForett | insAsso® | AceFUA. | Retsbuzione Toni: 79885 9548 910,00 TESGE Fara Cor REST

1| ore ordinarie 1.801,13 2| Ore noniavorate 17,00 | 197926 33647 8| Lavoro supplementare 30,00 | 21,358312 64075 10|| Festività 500 | 197928 96,96 40| Ind.sost vitolalloggio su assenze 1,00 568 548

ann WEBCOLF.com - 2008-2017. Cesi] corta E] Imporb Lordo [conkcancocoli | Cassa Coli | H.Lavorae | H Conhiuti [ AmotFreo. | Amo Atti 220988 25,00 110,00 | 10000 | 028 0,48 ES Fare Amerae [Fase Anno Com | Fare Goiute/ | Fe Resove [Rami aa || Setmmne RESvertsie: S| ore: 42,00 | ore: 15,17 | ore: ore: 5717) 200 | "si°si*si*si

25/07/20 FrogrImpLorso | Frog.NPS Col. | Frog CassaCali | PrGGMalatta [TFRAmo Prec | TFRAnvo Con. | Retibuz.a TFR | FrogrRet. TFR 597601 ErC 1.000,00 | 102402 | 1.689,66 | 1382423
```

## Tempi di conversione

| Pipeline | OCR ms | Markdown ms |
| --- | --- | --- |
| pytesseract + markitdown | 756.37 | 20.22 |
| MarkItDownNet | 911.59 | – |

## Osservazioni

- Gli OCR differiscono: ad esempio, pytesseract restituisce `Caccmri Cosa FS` mentre la versione .NET produce `Cocconi Cosi FS`. La discrepanza sembra dovuta a differenti versioni del motore Tesseract.
- `markitdown` non esegue OCR; necessita di testo in input. Lo script Python usa pytesseract per fornire il testo da convertire.
- L'opzione `MergeLines` unisce le linee contigue in paragrafi, avvicinando il layout al risultato di `markitdown`.
- `markitdown` applica una normalizzazione più aggressiva e introduce interruzioni aggiuntive dopo alcune intestazioni; `MarkItDownNet` resta più compatto ma integra l'OCR in un'unica passata.
