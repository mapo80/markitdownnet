# Busta paga OCR comparison

Conversion of `dataset/training/busta_paga_internet.jpeg` with Tesseract and RapidOCR.

Raw transcripts are available in
`dataset/training/busta_paga_internet.tesseract.md` and
`dataset/training/busta_paga_internet.rapidocr.md`.

## Timing

| Engine    | Time (s) | Δ vs Tesseract |
|-----------|---------:|---------------:|
| Tesseract | 1.12     | –              |
| RapidOCR  | 3.68     | +229%          |

Tesseract finished in roughly 1.1 s, while RapidOCR took about 3.7 s
(+229%).

Timings were recorded using the .NET `BustaPagaNet` tool.

## Tesseract

```markdown
Datore dilevoro: Colleboratore Rossi Cristina Tosticiku Beata Vitaliano Rotellini, 167 Via Venezia, 3 00128 Roma (RM) 00147 Roma (RM) Cod.Fise. Cod.Fisc

Det Assunzione | Daw Cessazione [SmstDIEm | Scatti | ScatoPreces:| Pros. Scato: 0110812010 3° | otmarote | ctwazoie Cocconi Cosi FS siFantime | Lieto |Convvenza E fissa ora 8 si | 000 000 000 |19,792%6

Fosafase | inirune | scomione SrForett | insAsso® | AceFUA. | Retsbuzione Toni: 79885 9548 910,00 TESGE Fara Cor REST

1| ore ordinarie 1.801,13 2| Ore noniavorate 17,00 | 197926 33647 8| Lavoro supplementare 30,00 | 21,358312 64075 10|| Festività 500 | 197928 96,96 40| Ind.sost vitolalloggio su assenze 1,00 568 548

ann WEBCOLF.com - 2008-2017. Cesi] corta E] Imporb Lordo [conkcancocoli | Cassa Coli | H.Lavorae | H Conhiuti [ AmotFreo. | Amo Atti 220988 25,00 110,00 | 10000 | 028 0,48 ES Fare Amerae [Fase Anno Com | Fare Goiute/ | Fe Resove [Rami aa || Setmmne RESvertsie: S| ore: 42,00 | ore: 15,17 | ore: ore: 5717) 200 | "si°si*si*si

25/07/20 FrogrImpLorso | Frog.NPS Col. | Frog CassaCali | PrGGMalatta [TFRAmo Prec | TFRAnvo Con. | Retibuz.a TFR | FrogrRet. TFR 597601 ErC 1.000,00 | 102402 | 1.689,66 | 1382423
```

## RapidOCR

```markdown
Datoredilavoro:
Collaboratore:
Rossi Cristina
Tosticku Beata
VitalianoRotellini167
ViaVenezia,3
00128Roma（RM)
00147Roma（RM)
Cod.Fisc.
Cod.Fisc.:
Data Assunzione
Data Cessazione
Scad.T.Determ.
Scati:
Scatto Preced:
Pross.Scatto:
Mese diretribuzione:
01/08/2010
3
01/08/2016
01/08/2018
%Part-time
Livello
Convi venza
Base Oraria
Codice INPS
Agosto2017
Pranzo:
Cena:
Alloggio:
B
rapp.domesfico
Si
0,00
0,00
00'0
19,7926
Paga Base
Ind .Funz.
Scatti Anz.
Str.Forfett
Ind Assorb.
Acc.FutA.
Retribuzione Totale:
795,65
95,48
910,00
1.801,13
RateoFerie:
Rato13a:
Rateo TFR:
Cod.
Descrizione
Tempo
Valore
Competenze
Trattenute
1
Ore ordinarie
1.801,13
2
Ore non lavorate
17,00
19,7926
-336,47
8
Lavoro supplementare
30,00
21,358312
640,75
10
Festivita
5,00
19,7926
98,96
40
Ind.so st.vitto/alloggio su assenze
1,00
5,48
5,48
vnnw.WEBCOLFcom-2008-2017
Tipopagamento:
Contanti
Codice IBAN:
Importo Lordo
Contr.carico coll.
Cas.sa Colf
H. Lavorate
H.Contributi
Arrot.Prec.
Arrot Attuale
2.209,85
25,00
110,00
100,00
0,28
0,43
NettodaPagare
Ferie Arretate
Ferie Anno Corr.
Ferie Godute
Ferie Residue
Ratei 13a:
Setimane INPSretibuite:
3:
5:
2.185,00
15
Ore:
42,00
Ore:
15,17
Ore:
:ao
57,17
2,00
1:
Si
Si
Si
Si
Progr.lmp.Lordo
Prog.INPS Coll.
Prog.CassaColf
Pr.GG.Malatfia
TFR Anno Prec.
TFR Anno Corr.
Retribuz.a TFR
Progr.Ret.TFR
25/07/
5.976,01
54,44
1.000,00
1.024,02
1.689,66
13.824,23
```
