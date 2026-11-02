# Redoslijed rada – šta se postavlja prvo, korak po korak

Ovo je **kratki spisak redoslijeda**. Svaki korak kaže **gdje kliknete** (AutoCAD: kartica **VANJSKO UREĐENJE
PRO** → ploča → dugme; SketchUp: meni **Extensions → Vanjsko uređenje PRO**), a u zagradi je naredba za kucanje.
Objašnjenja s primjerima su u [Prvi projekat](PRVI_PROJEKAT.md), sve opcije u [Priručniku](PRIRUCNIK.md).

> **Zlatno pravilo:** redoslijed je *jedinica → ishodište → nulta kota → slojevi → preuzimanje → kote → teren →
> crtanje → provjera → izvoz*. Ako se preskoči korak koji je ranije na spisku, kasniji koraci daju pogrešne
> mjere ili visine.

---

## Šta mora biti postavljeno prije čega

| Ovo … | … mora biti postavljeno PRIJE | Zašto |
|---|---|---|
| Jedinica crteža (mm / cm / m) | svega ostalog | Pogrešna jedinica daje klupu 10× ili 1000× veću ili manju. |
| Ishodište projekta | izvoza u SketchUp | Ishodište postaje 0,0,0 u SketchUpu. Daleke državne koordinate prave probleme u SketchUpu. |
| Nulta kota ±0,00 (`SDPDATUM`) | kota objekata, terena, izvoza | Bez nje nema apsolutnih kota. Program je **ne izmišlja**. |
| Mapiranje slojeva (`SDPMAPIRANJE`) | provjere crteža, preuzimanja, kota iz oznaka, terena | Program mora znati koji sloj je parcela, objekti, kote, teren. |
| Provjera crteža bez GREŠAKA | preuzimanja objekata | Otvorene granice i duple linije daju neispravne elemente. |
| Preuzeti parcela i objekti (`SDPPREUZMI`) | kota iz oznaka, provjere izlaska iz parcele | Kote se dodjeljuju elementima, a provjera parcele traži granicu. |
| Provjera projekta (`SDPVALIDATE`) bez grešaka | izvoza (`SDPEXPORT`) | Elementi s greškom mogu biti preskočeni u paketu. |
| Instaliran dodatak u SketchUpu | uvoza paketa | Paket otvara samo proširenje `pb_site_design_pro.rbz`. |

---

## 0. Jednom, na početku (instalacija)

1. **AutoCAD:** zatvorite AutoCAD → raspakujte `VanjskoUredjenjePRO-AutoCAD-instalacija.zip` → pokrenite
   `INSTALIRAJ.bat` → pokrenite AutoCAD. Mora se pojaviti kartica **VANJSKO UREĐENJE PRO**.
   * Ako se kartica ne pojavi, ukucajte `SDPABOUT`. Ako javi *Unknown command*, instalacija nije uspjela
     (v. [Instalacija](INSTALACIJA.md)).
2. **SketchUp:** **Extensions → Extension Manager → Install Extension** → odaberite `pb_site_design_pro.rbz` →
   ponovo pokrenite SketchUp. Mora se pojaviti meni **Extensions → Vanjsko uređenje PRO** i traka alata.
   * Ako traka alata nije vidljiva: **View → Toolbars → Vanjsko uređenje PRO**.

---

## A. Vaš projekat „Situacija 29.10." (već pretvoren u paket)

Paket `situacija-vanjsko-uredjenje.sdpkg` već ima jedinicu (cm), ishodište i ±0,00 = 504,00 m n.v. Koraci 1–8 iz
dijela B ovdje se **ne rade**; to je urađeno pri pretvaranju.

| # | Gdje kliknuti | Šta uraditi / odgovoriti |
|---|---|---|
| A1 | AutoCAD: **File → New** | Prazan crtež (šablon `acadiso.dwt`). Ne uvozite u crtež koji već ima elemente drugog projekta. |
| A2 | Ploča **SketchUp razmjena** → **Uvoz paketa** (`SDPUVOZPAKETA`) | Odaberite `situacija-vanjsko-uredjenje.sdpkg`. |
| A3 | Pitanje *INSUNITS … Postaviti INSUNITS na cm?* | **Da** |
| A4 | Kucajte `ZOOM` → `E` | Vidi se cijela situacija. |
| A5 | **File → Save As** | Snimite kao DWG, npr. `Situacija_VU.dwg`. **To je vaš projekat.** |
| A6 | Ploča **Projekt** → **Postavke** (`SDPSETTINGS`) | Provjerite: jedinica **Centimetri**, naziv projekta. Ništa ne mijenjajte ako nije potrebno. |
| A7 | Ploča **Teren i kote** → **Nulta kota** (`SDPDATUM`) | Samo provjera: ±0,00 = 504,00. Ako arhitekta da drugu vrijednost, ispravite ovdje. |
| A8 | Ploča **3D modeli** → **Uredi element** (`SDPEDIT`) | Ispravite pretpostavke iz izvještaja konverzije (redom ispod). |
| A9 | Nastavite od koraka **B9** (projektovanje) | Dalje je isto kao za svaki projekat. |

**Pretpostavke koje se ispravljaju prve (korak A8):**

1. **Objekti:** pomoću `SDPSELECT` označite objekte, pa **Uredi element** → *Visina* i *Broj etaža* po
   stvarnom projektu. Sada je visina = broj etaža × 3,00 m.
2. **Popločanje:** maslinasta šrafura je uvezena kao *Betonsko popločanje*. Ako je kamen ili ploče, u **Uredi
   element** promijenite *Materijal*, ili obrišite element i nacrtajte pravu vrstu po istoj konturi
   (`SDPCONVERT` na konturu).
3. **Zidovi i žardinjere:** upišite stvarne visine (*Visina*, kod žardinjera *Visina zida*).
4. **Stabla:** prečnik krošnje je veličina simbola (1,12 m). Označite stabla (`SDPSELECT` → po vrsti) →
   **Uredi element** → *Prečnik krošnje*, *Visina (pri sadnji)*.
5. **Mala rampa:** provjerite smjer uspona (izvještaj konverzije).
6. **Igrališta:** upišite proizvod i visine sprava (**Uredi element** → svojstva i *Visina*).

---

## B. Novi projekat iz vašeg DWG-a – strogi redoslijed

| # | Gdje kliknuti | Šta postaviti | Obavezno? |
|---|---|---|---|
| B0 | Windows | Kopirajte DWG i otvorite **kopiju**. | da |
| B1 | Kucajte `DIST` | Izmjerite nešto poznato (parking 5 m): `5` = m, `500` = cm, `5000` = mm. | **da – prvo** |
| B2 | **Projekt** → **Novi projekat** (`SDPNEW`) | Naziv, jedinica iz B1, **ishodište = tačka u sredini parcele**, ugao sjevera (`0` ako je sjever gore), mjerilo anotacija (npr. `500`). | **da** |
| B3 | **Teren i kote** → **Nulta kota** (`SDPDATUM`) | **NultaKota** (npr. `504,00`) → (samo ako crtež već ima apsolutne visine: *Postaviti visinu ishodišta…?* **Da**; ravan 2D crtež se sam prebacuje na relativne Z) → **Parter** → **Saobracajnice** (cm ispod partera ili Enter) → **Zkoordinate** (Apsolutne ako tačke terena imaju npr. 503,20) → **Izlaz**. | da (ako znate kotu) |
| B4 | **Uvoz DWG-a** → **Mapiranje slojeva** (`SDPMAPIRANJE`) | **Prijedlog prema nazivima** → provjerite: *Parcelna granica*, *Tlocrt objekta*, *Oznake kota* (obavezne); teren/izohipse ako imate snimak → **U redu**. | **da** |
| B5 | **Uvoz DWG-a** → **Provjera crteža** (`SDPPROVJERADWG`) | Popravite sve **GREŠKE** (`PEDIT`→`Close`, `OVERKILL`) i ponavljajte dok ih ne bude. | **da** |
| B6 | **Uvoz DWG-a** → **Preuzmi objekte** (`SDPPREUZMI`) | Od parcele i tlocrta nastaju elementi. Originalne linije ostaju. | **da** |
| B7 | **Teren i kote** → **Kote iz oznaka** (`SDPKOTEIZOZNAKA`) | Pregled prijedloga → **Da**. Nejasne: **Dodijeli kotu** (`SDPKOTA`). | ako ima kota |
| B8 | **Teren i kote** → **Model terena** (`SDPTEREN`) | Granica **Odaberi** (parcela) → oznake kota kao tačke terena **Da/Ne**. | samo uz geodetski snimak |
| B9 | Ploče s elementima (**Parter i popločanje**, **Zelenilo**, **Saobraćaj**, **Rasvjeta**, **Urbani mobilijar** …) | Crtanje: površine (poligon), linije (ivičnjak, ograda, staza), tačke (stablo, klupa, svjetiljka). Postojeće konture pretvarate s **Pretvori krive** (`SDPCONVERT`). | – |
| B10 | **3D modeli** → **Uredi element** (`SDPEDIT`) | Parametri svakog elementa (visina, materijal, dimenzije). Iz njih se gradi 3D. | po potrebi |
| B11 | **Nacrti i oznake** → **Numeracija** (`SDPNUMBER`) → **Oznaka** (`SDPLABEL`) → **Legenda** (`SDPLEGEND`) | Za nacrte: prvo numeracija, pa oznake, pa legenda. | za nacrte |
| B12 | **Provjera projekta** → **Provjeri projekat** (`SDPVALIDATE`) | Ispravite **GREŠKE**. Upozorenja pročitajte. Provjere su pomoć softvera, ne potvrda usklađenosti s propisima. | **da** |
| B13 | **Količinski obračun** → **Tabele** (`SDPSCHEDULE`) / **Izvoz CSV** (`SDPSCHEDULECSV`) | Količine za predmjer. | po potrebi |
| B14 | **SketchUp razmjena** → **Izvoz paketa** (`SDPEXPORT`) | Snimite `.sdpkg` (npr. pored DWG-a). | **da – za 3D** |

---

## C. SketchUp – 3D model

| # | Gdje kliknuti | Šta uraditi |
|---|---|---|
| C1 | **File → New** (šablon u metrima) | Novi model. Paket u isti model uvozite i kad ga kasnije osvježavate. |
| C2 | **Extensions → Vanjsko uređenje PRO → Uvoz paketa...** (prvo dugme na traci) | Odaberite `.sdpkg`. Dijalog prvo pokazuje **provjeru paketa**. |
| C3 | U dijalogu: **nivo detalja** | Za rad **Standardni kvalitet**; za slike **Visoki kvalitet** (sporije, teži model). |
| C4 | U dijalogu: **Primijeni** | Model se gradi. Izvještaj: **Izvještaj posljednjeg uvoza**. |
| C5 | **Izrada scena za pregled** (dugme na traci) | Gotove scene: tlocrt, ptičja perspektiva … |
| C6 | **Biblioteka 3D modela...** | Dodatni modeli (klupe, svjetiljke, sprave …) bez interneta. |
| C7 | **File → Save** | Snimite `.skp`. |

---

## D. Kad se projekat promijeni (uvijek ovim redom)

1. **Izmjena u AutoCAD-u** (najčešće): izmijenite element (gripovi ili **Uredi element**) →
   **Provjeri projekat** → **Izvoz paketa** u **istu** datoteku → u SketchUpu ponovo **Uvoz paketa...** u isti
   model. Mijenjaju se samo izmijenjeni elementi.
2. **Izmjena u SketchUpu:** u SketchUpu izmjene u **Svojstva elementa** → zatim **Izvoz izmjena za AutoCAD...** → u AutoCAD-u
   **SketchUp razmjena** → **Primijeni izmjene** (`SDPAPPLYCHANGES`) → zatim ponovo **Izvoz paketa**.
3. **Paket napravljen drugdje** (npr. pretvoren iz drugog DWG-a): **Uvoz paketa** (`SDPUVOZPAKETA`) u prazan crtež
   (dio A).

---

## Kontrolna lista (odštampati)

- [ ] Dodatak u AutoCAD-u (kartica VANJSKO UREĐENJE PRO) i u SketchUpu (meni Extensions) radi
- [ ] Rad na **kopiji** DWG-a
- [ ] Jedinica izmjerena (`DIST`) i postavljena
- [ ] Ishodište u sredini parcele
- [ ] Nulta kota ±0,00 upisana (ili svjesno ostavljena prazna)
- [ ] Slojevi mapirani: parcela, objekti, kote
- [ ] Provjera crteža: 0 grešaka
- [ ] Parcela i objekti preuzeti
- [ ] Kote objekata dodijeljene
- [ ] Teren (samo uz snimak)
- [ ] Vanjsko uređenje nacrtano, parametri upisani
- [ ] Provjera projekta: 0 grešaka
- [ ] Paket izvezen → uvezen u SketchUp → snimljen `.skp`
