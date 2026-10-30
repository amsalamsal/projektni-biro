# Priručnik – Vanjsko uređenje PRO

Kompletan opis svake naredbe, opcije i dijaloga, korak po korak. Kraći pregled toka rada je u [Uputstvu](UPUTSTVO.md),
a instalacija u [Instalaciji](INSTALACIJA.md).

> **Status:** dodatak se kompajlira i prolazi automatske testove, ali još nije isproban u stvarnom AutoCAD-u i
> SketchUpu. Ako nešto radi drugačije nego što piše ovdje, javite tačan tekst iz komandne linije – priručnik i
> program će se uskladiti.

---

## Sadržaj

1. [Osnove rada](#1-osnove-rada)
2. [Prvih 10 minuta – proba na primjeru](#2-prvih-10-minuta--proba-na-primjeru)
3. [Tok rada na stvarnom projektu (kontrolna lista)](#3-tok-rada-na-stvarnom-projektu-kontrolna-lista)
4. [Traka VANJSKO UREĐENJE PRO – svako dugme](#4-traka-vanjsko-uređenje-pro--svako-dugme)
5. [Paleta (Biblioteka, Svojstva, Provjera)](#5-paleta-biblioteka-svojstva-provjera)
6. [Naredbe AutoCAD-a – detaljno](#6-naredbe-autocad-a--detaljno)
7. [Kote – kako program računa](#7-kote--kako-program-računa)
8. [SketchUp – svaka stavka menija i dijaloga](#8-sketchup--svaka-stavka-menija-i-dijaloga)
9. [Razmjena AutoCAD ↔ SketchUp – pravila](#9-razmjena-autocad--sketchup--pravila)
10. [Sigurnosne kopije i oporavak](#10-sigurnosne-kopije-i-oporavak)
11. [Šifre nalaza – šta znače i šta uraditi](#11-šifre-nalaza--šta-znače-i-šta-uraditi)
12. [Česti problemi i rješenja](#12-česti-problemi-i-rješenja)
13. [Pojmovnik](#13-pojmovnik)

---

## 1. Osnove rada

### 1.1 Kako se pokreće naredba
* klikom na dugme na kartici trake **VANJSKO UREĐENJE PRO**, ili
* upisom naziva u komandnu liniju (npr. `SDPDATUM`) i Enter.

Sve naredbe počinju sa `SDP`. Spisak svih je u [poglavlju 6](#6-naredbe-autocad-a--detaljno).

### 1.2 Kako se odgovara na pitanja u komandnoj liniji

| Vidite | Značenje | Šta uraditi |
|---|---|---|
| `Izmjena [Naziv/Broj/Izlaz]` | opcije u uglastim zagradama | kliknite opciju u komandnoj liniji ili upišite njen naziv |
| `<Izlaz>` ili `<8>` | podrazumijevani odgovor | samo Enter prihvata taj odgovor |
| `[Da/Ne]` | potvrda | `Da` ili `Ne`; Enter = vrijednost u `< >` |
| `(m)`, `(cm)`, `(°)` | jedinica unosa | broj u toj jedinici, bez upisa jedinice |

* **Opcije u komandnoj liniji nemaju kvačice** (`Saobracajnice`, `Oznaci`, `Preskoci`) jer ih AutoCAD tako zahtijeva.
  Poruke i dijalozi imaju pravilna slova.
* **Decimalni broj** možete upisati sa zarezom ili tačkom: `518,56` i `518.56` su isto. Hiljade ne odvajajte.
* **Esc** prekida naredbu. Ništa od te naredbe ne ostaje u crtežu.
* **UNDO** (Ctrl+Z) poništava cijelu naredbu jednim korakom, i kad je naredba napravila stotine objekata.

### 1.3 Biranje tipa elementa
Više naredbi traži *Tip elementa (id tipa ili dio naziva, ? za popis)*:
* upišite **dio naziva**, npr. `klupa` – ako odgovara jedan tip, odmah je odabran; ako više, ispisuje se lista;
* upišite **id tipa**, npr. `furniture.bench` – tačan izbor;
* upišite **`?`** – lista svih tipova (prvih 40; suzite pretragu);
* naziv se može pisati i na engleskom (`bench`).

Lakše je birati iz **palete** (`SDPBROWSER`) ili sa trake – tada se tip ne upisuje.

### 1.4 Šta je „element”
Element je objekat crteža (polilinija ili blok) kojem je dodatak dodijelio **tip, parametre, oznaku i jedinstveni
identifikator**. Elementi su na slojevima `VU-…`. Obične linije crteža nisu elementi dok ih ne pretvorite
(`SDPCONVERT`, `SDPPREUZMI`, `SDPDRAW`).

Tri vrste geometrije:
* **tačkasti** (stablo, klupa, svjetiljka, šaht, parking mjesto) – tačka + rotacija;
* **linijski / trasa** (staza, ivičnjak, ograda, cijev) – polilinija;
* **površinski** (popločanje, travnjak, tlocrt objekta, parcela) – zatvorena polilinija.

### 1.5 Veće operacije su zaštićene
Naredbe koje mijenjaju mnogo objekata (`SDPPREUZMI`, `SDPKOTEIZOZNAKA`, `SDPTEREN`, `SDPAPPLYCHANGES`,
`SDPPATHARRAY`, `SDPPARKING`, `SDPGRID`, `SDPSAMPLE`):
1. prvo kopiraju **zadnju sačuvanu verziju** crteža u folder `_SDP_backup` (ako je uključeno, v. `SDPKOPIJEPOSTAVKE`);
2. upišu početak u dnevnik oporavka;
3. sve urade kao **jedan korak UNDO**.

**Savjet:** sačuvajte crtež (Ctrl+S) prije veće operacije – kopija sadrži samo sačuvano stanje.

---

## 2. Prvih 10 minuta – proba na primjeru

1. Novi prazan crtež.
2. `SDPSAMPLE` → **Oba** → Enter. Nastaju primjer izvornog DWG-a (slojevi KP_GRANICA, OBJEKAT_TLOCRT, KOTE…) i
   gotov primjer stambenog kompleksa. `ZOOM` → `Extents`.
3. `SDPPROVJERADWG` – izvještaj o ulaznom crtežu (primjer je već mapiran).
4. `SDPDATUM` – vidite kako je zadana nulta kota primjera (520,15 m n.v.) → `Izlaz`.
5. `SDPKOTEIZOZNAKA` – vidite prijedloge i privremene linije od oznaka do objekata → `Da`.
6. `SDPTEREN` → `Bez` → `Ne` – nastaje mreža terena na sloju `VU-TEREN`.
7. `SDPVALIDATE` – provjera; rezultati u paleti.
8. `SDPSCHEDULE` → `Sve` → kliknite mjesto – tabele količina.
9. `SDPEXPORT` – snimite `.sdpkg`.
10. U SketchUpu: *Extensions → Vanjsko uređenje PRO → Uvoz paketa...* → isti `.sdpkg` → **Uvezi**.

---

## 3. Tok rada na stvarnom projektu (kontrolna lista)

| # | Korak | Naredba | Na šta paziti |
|---|---|---|---|
| 1 | Sačuvajte kopiju originalnog DWG-a | – | radite na kopiji |
| 2 | Postavke projekta i **jedinica crteža** | `SDPNEW` | Jedinicu odredite po stvarnoj geometriji, ne po zaglavlju. Parking 5 m dug 500 jedinica = centimetri. |
| 3 | Pripremite slojeve | AutoCAD (`LAYMCH`, `JOIN`, `PEDIT`) | Tlocrti objekata na svom sloju; granice zatvorene polilinije; obrišite duplikate. |
| 4 | Mapiranje slojeva | `SDPMAPIRANJE` | Jedna uloga po sloju; provjerite prijedlog. |
| 5 | Provjera ulaza | `SDPPROVJERADWG` | Riješite sve **GREŠKE** prije nastavka. |
| 6 | Nulta kota, parter, saobraćajnice | `SDPDATUM` | Bez nulte kote apsolutne kote se ne računaju. |
| 7 | Preuzimanje parcele i objekata | `SDPPREUZMI` | Izvorni objekti se ne mijenjaju. |
| 8 | Kote objekata | `SDPKOTEIZOZNAKA`, `SDPKOTA` | Nejasne oznake riješite ručno. |
| 9 | Teren (ako postoji snimak) | `SDPTEREN`, `SDPPROFIL`, `SDPANALIZATERENA` | Bez geodetskog snimka teren se ne gradi. |
| 10 | Georeferenciranje (po potrebi) | `SDPGEOREF`, `SDPKML` | Dvije ili više kontrolnih tačaka. |
| 11 | Projektovanje | traka, paleta, `SDPPLACE`, `SDPDRAW`, `SDPCONVERT`, generatori | |
| 12 | Oznake i nacrti | `SDPLABEL`, `SDPNUMBER`, `SDPLEGEND`, `SDPTITLEBLOCK`… | |
| 13 | Provjera | `SDPVALIDATE` | Pregledajte upozorenja u paleti. |
| 14 | Količine | `SDPSCHEDULE`, `SDPSCHEDULECSV` | |
| 15 | Izvoz u SketchUp | `SDPEXPORT` | |
| 16 | Izmjene iz SketchUpa (po potrebi) | `SDPAPPLYCHANGES` | Zatim ponovo `SDPEXPORT`. |

---

## 4. Traka VANJSKO UREĐENJE PRO – svako dugme

Prelaskom miša preko dugmeta vidi se opis i naziv naredbe.

**Projekt**

| Dugme | Naredba |
|---|---|
| Novi projekat | `SDPNEW` |
| Postavke | `SDPSETTINGS` |
| Slojevi | `SDPLAYERS` |
| Biblioteka | `SDPBROWSER` |
| Primjer projekta | `SDPSAMPLE` |

**Uvoz DWG-a**

| Dugme | Naredba |
|---|---|
| Mapiranje slojeva | `SDPMAPIRANJE` |
| Provjera crteža | `SDPPROVJERADWG` |
| Preuzmi objekte | `SDPPREUZMI` |
| Pretvori krive | `SDPCONVERT` |
| padajuće liste *Lokacija* i *Objekti i reference* | `SDPDRAW` / `SDPPLACE` za izabrani tip |

**Georeferenciranje**

| Dugme | Naredba |
|---|---|
| Kontrolne tačke | `SDPGEOREF` |
| Izvoz KML | `SDPKML` |
| Koordinate | `SDPCOORD` |
| Sjever | `SDPNORTH` |

**Teren i kote**

| Dugme | Naredba |
|---|---|
| Nulta kota | `SDPDATUM` |
| Dodijeli kotu | `SDPKOTA` |
| Kote iz oznaka | `SDPKOTEIZOZNAKA` |
| Visinska kota | `SDPSPOTLEVEL` |
| Model terena | `SDPTEREN` |
| Profil | `SDPPROFIL` |
| Analiza terena | `SDPANALIZATERENA` |
| Strelica nagiba | `SDPSLOPE` |

**Paneli s elementima**

| Panel | Kategorije u padajućim listama | Dodatni alati |
|---|---|---|
| Parter i popločanje | Parter i popločanje, Ograde i zidovi | Površina (`SDPDRAW`), Oznake površina (`SDPAREALABEL`) |
| Zelenilo | Zelenilo | Drvored (`SDPPATHARRAY`), Raster sadnje (`SDPGRID`) |
| Saobraćaj i parking | Saobraćaj i parking, Biciklistička infrastruktura | Niz parking mjesta (`SDPPARKING`), Oznake širina (`SDPWIDTHLABEL`), Stalci duž staze (`SDPPATHARRAY`) |
| Infrastruktura | Komunalna infrastruktura, Voda i odvodnja, Eko tehnologije, Vanjski klimatski sistemi | – |
| Rasvjeta | Rasvjeta | Svjetiljke duž puta (`SDPPATHARRAY`), Pokrivenost (`SDPLIGHTCOVER`) |
| Urbani mobilijar | Urbani mobilijar i rekreacija, Nadstrešnice i zasjena, Dekorativni elementi, Informativni i sigurnosni sistemi | Raspored duž staze (`SDPPATHARRAY`) |

Svaki od ovih panela ima i:
* veliko dugme **Pregled** – otvara paletu filtriranu na tu kategoriju;
* **padajuće liste po podkategorijama** – klik na stavku odmah pokreće postavljanje tog tipa (tačkasti →
  `SDPPLACE`, ostali → `SDPDRAW`). Zadnja korištena stavka ostaje na dugmetu.

**3D modeli**

| Dugme | Naredba |
|---|---|
| Uredi element | `SDPEDIT` |
| Zone sigurnosti | `SDPCLEARANCE` |
| Odabir | `SDPSELECT` |
| Korisnički katalozi | `SDPCATALOGFOLDER` |

**SketchUp razmjena**

| Dugme | Naredba |
|---|---|
| Izvoz paketa | `SDPEXPORT` |
| Primijeni izmjene | `SDPAPPLYCHANGES` |
| Provjera prije izvoza | `SDPVALIDATE` |

**Količinski obračun**

| Dugme | Naredba |
|---|---|
| Tabele | `SDPSCHEDULE` |
| Izvoz CSV | `SDPSCHEDULECSV` |
| Količine | `SDPQUANTITIES` |

**Nacrti i oznake**

| Dugme | Naredba |
|---|---|
| Oznaka | `SDPLABEL` |
| Numeracija | `SDPNUMBER` |
| Legenda | `SDPLEGEND` |
| Sastavnica | `SDPTITLEBLOCK` |
| Razmjernik | `SDPSCALEBAR` |
| Presjek | `SDPSECTION` |
| Detalj | `SDPDETAIL` |
| Rasterske ose | `SDPGRIDLINES` |
| Oblak revizije | `SDPREVCLOUD` |

**Provjera projekta**

| Dugme | Naredba |
|---|---|
| Provjeri projekat | `SDPVALIDATE` |
| Provjera crteža | `SDPPROVJERADWG` |
| Revizija | `SDPAUDIT` |

**Postavke i pomoć**

| Dugme | Naredba |
|---|---|
| Pomoć | `SDPPOMOC` |
| Sigurnosna kopija | `SDPKOPIJA` |
| Postavke kopija | `SDPKOPIJEPOSTAVKE` |
| Oporavak | `SDPOPORAVAK` |
| Učitaj katalog | `SDPRELOAD` |
| O programu | `SDPABOUT` |

---

## 5. Paleta (Biblioteka, Svojstva, Provjera)

Otvara se naredbom `SDPBROWSER` ili dugmetom **Biblioteka**. Paleta se može usidriti uz rub ekrana kao svaka
AutoCAD paleta.

### 5.1 Kartica Biblioteka
* **Polje za pretragu** (gore) – filtrira tipove dok kucate (naziv, id, engleski naziv).
* **Lista kategorija**:
  * *Sve kategorije*;
  * *Omiljeni* – samo tipovi označeni zvjezdicom;
  * *Nedavno korišteni*;
  * pojedinačne kategorije.
* **Dvostruki klik ili Enter** na tip – pokreće postavljanje (`SDPPLACE` za tačkaste, `SDPDRAW` za ostale).
* **Desni klik** na tip – dodaje u omiljene ili uklanja iz njih (☆/★). Omiljeni se pamte po korisniku.

### 5.2 Kartica Svojstva
* Odaberite jedan ili više elemenata u crtežu – kartica prikazuje njihove parametre.
* Kad odabrani elementi imaju različite vrijednosti, piše *Vrijednosti se razlikuju*.
* Izmijenite vrijednost i kliknite **Primijeni izmjene** – mijenjaju se svi odabrani elementi. Neispravna vrijednost
  (izvan raspona) se ne primjenjuje i javlja se poruka.

### 5.3 Kartica Provjera
* Prikazuje rezultat zadnjeg `SDPVALIDATE` ili `SDPPROVJERADWG`: ozbiljnost, šifra, element, poruka.
* **Dvostruki klik na red** – zumira na objekat i odabire ga.

---

## 6. Naredbe AutoCAD-a – detaljno

Za svaku naredbu: **Šta radi → Koraci (redom kako pita) → Rezultat → Napomene**.

### 6.1 Projekat

#### SDPNEW – novi projekat
**Šta radi:** zapisuje osnovne podatke projekta u crtež i pravi standardne slojeve `VU-…`.

**Koraci:**
1. *Naziv projekta* – tekst.
2. *Broj projekta* – tekst; `-` = bez broja.
3. *Investitor*, *Lokacija* – tekst; `-` = prazno.
4. *Jedinica crteža [Milimetri/Centimetri/Metri]*:
   * odaberite jedinicu **u kojoj je geometrija stvarno nacrtana**;
   * postavlja i AutoCAD varijablu INSUNITS;
   * ako crtež već ima elemente, promjena jedinice **ne skalira** geometriju (dobićete upozorenje).
5. *Ishodište projekta <0,0,0>*:
   * kliknite tačku ili Enter;
   * ta tačka u SketchUpu postaje 0,0,0;
   * birajte ugao parcele ili tačku blizu projekta da SketchUp model ne bude daleko od ishodišta.
6. *Ugao pravog sjevera* – stepeni u smjeru kazaljke od +Y ose crteža (0 = sjever je gore).
7. *Mjerilo anotacija 1:* – npr. 200, 500. Određuje veličinu teksta i simbola oznaka.

**Rezultat:** postavke su sačuvane u crtežu i slojevi napravljeni. Visine se **ne** zadaju ovdje: sljedeći korak je
`SDPDATUM`, pa `SDPMAPIRANJE`.

#### SDPSETTINGS – postavke projekta
Prikazuje trenutne postavke i nudi izmjenu u petlji dok ne odaberete **Izlaz**:

| Opcija | Šta mijenja |
|---|---|
| Naziv, Broj, Investitor, Lokacija, Autor, Revizija | tekstualni podaci (idu u sastavnicu i paket) |
| Jedinice | jedinica crteža (kao u `SDPNEW`) |
| Ishodiste | nova tačka ishodišta |
| Sjever | ugao pravog sjevera |
| Sistem | EPSG kod koordinatnog sistema (npr. `EPSG:3908`); kontrolne tačke su u `SDPGEOREF` |
| Mjerilo | mjerilo anotacija 1:N |
| Tekst | visina teksta na papiru u mm (npr. 2,5) |
| Zona | oznaka zone u numeraciji (npr. `A` daje `A-T-001`); `-` = bez zone |
| Numeracija | brojevi po **Prefiksu** (svaki prefiks svoj niz: T-001, B-001) ili po **Kategoriji**; broj cifara (3 → 001) |
| Izlaz | čuva i izlazi |

#### SDPLAYERS – standard slojeva
**Opcije:**
* **Napravi** – pravi slojeve `VU-…` koji nedostaju; postojeće ne dira.
* **Popravi** – pravi nedostajuće i vraća boju, tip linije i debljinu postojećih `VU-…` slojeva na standard.
* **Provjeri** – samo ispisuje odstupanja, ništa ne mijenja.
* **Stari** – preimenuje slojeve verzije 0.1 (engleski nazivi, npr. `L-PLNT-TREE`) u nove `VU-…`:
  * prikazuje listu i pita *Preimenovati navedene slojeve? [Da/Ne]* (podrazumijevano **Ne**);
  * koristite samo ako je te slojeve napravio ovaj dodatak, jer ih Vaš crtež možda koristi po svom standardu.

Slojevi ulaznog DWG-a se nikad ne preimenuju.

#### SDPAUDIT – revizija elemenata
Pokrenite nakon kopiranja, `COPY`, `MIRROR`, ubacivanja iz drugog crteža ili kad provjera javi duple identifikatore.
* kopijama daje **nove identifikatore i nove oznake**;
* osvježava prikaz (šrafure, simbole) svih elemenata;
* prijavljuje nepoznate tipove (nedostaje korisnički katalog) i probleme geometrije.

Na kraju ispisuje broj kopija, duplikata, nepoznatih tipova i problema geometrije.

#### SDPSAMPLE – primjer projekta
*Primjer [Elementi/Izvorni/Oba]*:
* **Elementi** – gotov stambeni kompleks (139 elemenata) za probu provjere, količina i izvoza;
* **Izvorni** – primjer „sirovog” DWG-a za probu toka rada (mapiranje, provjera, kote, teren);
* **Oba** – oboje.

Ako crtež već ima elemente, pita da li ipak dodati. Primjer postavlja **svoje** vrijednosti: mm, ishodište,
±0,00 = 520,15 m n.v. To su vrijednosti primjera, a ne podrazumijevane vrijednosti novih projekata. Probajte primjer
u praznom crtežu.

#### SDPRELOAD, SDPCATALOGFOLDER, SDPABOUT
* `SDPCATALOGFOLDER` – otvara `%APPDATA%\SiteDesignPro\catalogs`. Tu stavljate vlastite `*.json` kataloge
  (tipovi, slojevi, materijali).
* `SDPRELOAD` – ponovo učitava katalog s korisničkim katalozima; ispisuje broj tipova i eventualne greške u datotekama.
* `SDPABOUT` – verzija, verzija sheme, katalog, putanja dnevnika (log). Ovo pošaljite uz prijavu problema.

### 6.2 Uvoz DWG-a

#### SDPMAPIRANJE – mapiranje slojeva (dijalog)
**Šta radi:** govori programu šta se nalazi na kojem sloju Vašeg crteža. **Nazivi slojeva se ne mijenjaju.**

**Dijalog:**
* tabela: **Sloj** | **Objekata** (koliko objekata ima sloj) | **Uloga u projektu** (padajuća lista);
* uloge:

| Uloga | Šta na tom sloju treba da bude |
|---|---|
| Parcelna granica | zatvorena polilinija granice parcele |
| Tlocrt objekta | zatvorene polilinije tlocrta (ne oznake) |
| Oznaka položaja objekta | blok ili tekst koji samo označava objekat |
| Saobraćajnice | regulacija / ivice saobraćajnica |
| Linija partera | linija ili kontura partera |
| Oznake kota | tekstovi kota (`+0,00`, `-0,15`, `KT 520,00`, `502,90 mns`) |
| Tačke terena | tačke ili blokovi s Z visinom (geodetski snimak) |
| Izohipse | polilinije s visinom |
| Prelomne linije | 3D polilinije prelomnica |
| Postojeća infrastruktura | postojeći objekti kojima se mogu dodijeliti kote |
| Kontrolne tačke | tačke poznatih koordinata |

* **Jedan sloj = jedna uloga.** Sloj bez uloge se ne koristi.
* **Prijedlog prema nazivima** – popunjava samo prazne redove prema uobičajenim nazivima (`PARCELA`, `KOTE`,
  `OBJEKAT`…). **Uvijek provjerite prijedlog**, jer se može prevariti (npr. „Parcele koje nisu naše” prepozna kao
  parcelu, ili sloj sa simbolima kota umjesto sloja s tekstom kota).
* **Dozvoli rad s oznakama objekata** – uključite samo ako crtež nema tlocrte nego samo blokove-oznake. Tada
  `SDPPREUZMI` za svaku oznaku traži stvarne dimenzije; program ih ne pretpostavlja.
* **Sačuvaj predložak...** / **Učitaj predložak...** – mapiranje se čuva kao `*.sdpmap.json` u
  `%APPDATA%\SiteDesignPro\mappings` i koristi za sljedeće crteže istog projektanta ili geodete.
* **U redu** čuva mapiranje u crtežu; **Otkaži** ne mijenja ništa.

**Ako su objekti izmiješani na jednom sloju** (npr. tlocrti i ostale linije na sloju `0`): prvo ih AutoCAD naredbom
`LAYMCH` ili kroz svojstva prebacite na zaseban sloj, pa taj sloj mapirajte kao *Tlocrt objekta*.

#### SDPPROVJERADWG – provjera ulaznog crteža
**Šta radi:** prije bilo kakve izrade provjerava ulazni crtež prema mapiranju. Ništa ne mijenja.

**Ispis:**
* broj objekata, parcela, objekata, oznaka objekata, oznaka kota (i koliko ih je upotrebljivo), tačaka terena s
  visinom;
* površina parcele (`25.433,66 m²`);
* napomena o koordinatama (lokalne ili u državnom sistemu);
* slojevi bez uloge;
* nalazi sa šiframa `SDP-I…` ([poglavlje 11](#11-šifre-nalaza--šta-znače-i-šta-uraditi)) – i u paleti, kartica
  *Provjera*.

**Na kraju:** *Sačuvati izvještaj u tekstualnu datoteku? [Da/Ne]* – `Da` piše
`<naziv crteža>_provjera_ulaza.txt` pored crteža.

**Pravilo:** riješite sve **GREŠKE** prije `SDPPREUZMI`. Upozorenja pročitajte i odlučite.

#### SDPPREUZMI – preuzimanje parcele i objekata
**Šta radi:**
* od mapiranih granica parcele pravi elemente *Parcelna granica*;
* od tlocrta objekata pravi elemente *Tlocrt objekta* (referentni volumen za SketchUp);
* izvorni objekti ostaju netaknuti.

**Kako radi:**
* uzima samo **zatvorene polilinije**; otvorene preskače i javlja;
* pamti izvorni objekat, pa ponovno pokretanje **ne pravi duplikate** (*već preuzeto ranije: N*);
* ako je u mapiranju uključen rad s oznakama:
  * za svaku oznaku objekta pita *Napraviti tlocrt za ovu oznaku? [Da/Preskoci]*;
  * na `Da` traži *Dužinu objekta duž ose bloka (m)* i *Širinu (m)*;
  * pravi pravougaonik orijentisan po bloku.

**Rezultat:** *Preuzeto: N novih elemenata; već preuzeto ranije: N; preskočeno: N.* Jedan UNDO poništava sve.

#### SDPCONVERT – pretvaranje postojećih linija u elemente
1. Odaberite polilinije, linije, lukove ili krugove. Ako ste ih odabrali prije naredbe, koristi se taj odabir.
2. *Tip elementa za odabrane objekte* – samo linijski i površinski tipovi.

**Šta se dešava:**
* objekat prelazi na sloj tipa i dobija podatke elementa, oznaku i prikaz (šrafura, širina);
* za površinske tipove objekat mora biti zatvoren, inače se preskače uz savjet *PEDIT > Close* ili opciju *Unutar*
  u `SDPDRAW`;
* objekti koji su već elementi se preskaču (koristite `SDPEDIT`);
* geometrijske greške (samopresijecanje i slično) se javljaju odmah.

### 6.3 Georeferenciranje

#### SDPGEOREF – koordinatni sistem i kontrolne tačke
Prikazuje sistem, broj kontrolnih tačaka i trenutnu transformaciju, pa nudi opcije u petlji:

| Opcija | Šta radi |
|---|---|
| **Sistem** | upis EPSG koda; ispisuje podržane (3907/3908/3909 MGI Balkans 5/6/7, 31275–31277, 25833/25834, 32633/32634). Drugi kod se zapisuje, ali KML nije moguć. |
| **Dodaj** | kliknite tačku u crtežu (npr. ugao objekta), dajte naziv (`T1`…), upišite **E (y)** i **N (x)** iz geodetske podloge. Isti naziv zamjenjuje staru tačku. |
| **Lista** | ispis tačaka (crtež → E, N) i odstupanja nakon proračuna |
| **Obrisi** | brisanje tačke po nazivu |
| **Izracunaj** | proračun transformacije (v. dolje) |
| **Ponisti** | vraća na „koordinate crteža = koordinate projekta” (bilježi se u historiji) |
| **Izlaz** | čuva i izlazi |

**Izracunaj:**
* **1 tačka** – daje samo položaj. Program traži poznatu rotaciju crteža u stepenima.
* **2 ili više tačaka** – *Određivanje mjerila [Kruto/Slicnost]*:
  * **Kruto** (preporučeno): rotacija + pomak, mjerilo 1;
  * **Slicnost**: i mjerilo – kad crtež nije u stvarnoj veličini.
* Ispis: rotacija, pomak, **srednje odstupanje** i odstupanja dE/dN po tački. Upozorenja (npr. mjerilo jako
  različito od 1 → vjerovatno pogrešne jedinice) se ispisuju sa `!`.
* *Primijeniti ovu transformaciju na projekat?* – `Da` zapisuje transformaciju i dodaje zapis u **historiju**.
  **Geometrija crteža se nikad ne pomjera.**

**Savjet:** koristite 3–4 tačke raspoređene po uglovima projekta. Odstupanje veće od nekoliko cm znači pogrešno
upisanu tačku ili pogrešno kliknut ugao.

#### SDPKML – izvoz za Google Earth
* Zahtijeva poznat sistem (`SDPGEOREF > Sistem`).
* Izvozi konture parcele, objekata, površina i saobraćajnica kroz transformaciju u WGS84.
* Pita gdje da sačuva `.kml`.
* **KML služi samo za orijentaciju.** Nije geodetska podloga. Pretvaranje MGI 1901 → WGS84 je približno (nekoliko
  metara).
* Ako transformacija nije zadana i ishodište je 0, javlja da se koordinate crteža koriste direktno: ako je crtež u
  lokalnim koordinatama, rezultat u Google Earthu će biti na pogrešnom mjestu.

#### SDPCOORD – oznaka koordinata
Kliknite tačke jednu za drugom (Enter za kraj). Svaka dobija oznaku `Y = … / X = …`.

#### SDPNORTH – strelica sjevera
Kliknite položaj. Strelica je okrenuta prema **pravom sjeveru** iz postavki, ne prema +Y crteža.

### 6.4 Teren i kote

#### SDPDATUM – visinski sistem projekta
Prikazuje trenutno stanje i probleme (`!`), pa nudi opcije u petlji:

| Opcija | Šta zadajete |
|---|---|
| **NultaKota** | apsolutna nadmorska visina ±0,00 objekta (m n.v.), npr. `504,00`. Ako su Z koordinate apsolutne, nudi i da ishodište projekta postavi na tu visinu (u SketchUpu je tada ±0,00 = 0). |
| **Parter** | **JednakNultoj** (parter = ±0,00) ili **Razlicit** + kota partera u odnosu na ±0,00 (npr. `-0,15`) |
| **Saobracajnice** | koliko su saobraćajnice ispod partera, **u cm** (npr. `15`). **Enter na praznom = nije podešeno.** Program nema podrazumijevanu vrijednost. |
| **Datum** | naziv visinskog datuma (npr. `Trst 1900`) – zapisuje se u izvještaje i paket |
| **Zkoordinate** | šta Z u crtežu znači: **Apsolutne** (m n.v.) ili **Relativne** (u odnosu na ±0,00) |
| **Izlaz** | čuva i izlazi |

Primjer i računanje: [poglavlje 7](#7-kote--kako-program-računa).

#### SDPKOTA – kota jednog objekta
1. *Odaberite objekat* – kota važi **samo za taj objekat**.
2. *Način [Relativna/Apsolutna/Parter/Saobracajnica]*:
   * **Relativna** – u odnosu na ±0,00 (npr. `+0,25`);
   * **Apsolutna** – m n.v. (npr. `503,75`);
   * **Parter** – u odnosu na parter (+ iznad, − ispod);
   * **Saobracajnica** – spuštanje ispod partera u **cm**. Ponuđena je vrijednost iz `SDPDATUM`, ako je zadana.
3. **Pregled:** objekat se osvijetli i ispisuje se *Z staro → novo*, relativna i apsolutna kota.
4. *Primijeniti kotu na ovaj objekat? [Da/Ne]*.

**Šta se mijenja:**
* elementima kota u podacima + Z geometrije;
* običnim objektima Z geometrije i zapis u XData (`SITEDESIGNPRO_KOTA`).

**Napomene:**
* **3D polilinija** – svi vrhovi dobijaju istu kotu i postojeći nagib se gubi (program upozorava).
* Splajn, mreža i solid ne mogu dobiti kotu: *Ova vrsta objekta ne može dobiti kotu.*
* Bez zadane nulte kote naredba za apsolutne vrijednosti javlja da datum nije definisan.

#### SDPKOTEIZOZNAKA – kote iz tekstualnih oznaka
1. Čita tekstove na slojevima mapiranim kao *Oznake kota*.
2. Za svaku oznaku traži **najmanji zatvoreni objekat koji je sadrži** (tlocrt, saobraćajnica, parter,
   infrastruktura). Kod multileadera ima prednost objekat na koji pokazuje strelica.
3. Ispisuje listu sa statusom svake oznake:

| Status | Značenje | Šta uraditi |
|---|---|---|
| Prijedlog | jasno je kojem objektu pripada | potvrditi |
| Bez objekta | oznaka nije unutar zatvorenog objekta | `SDPKOTA` ručno |
| Više objekata | dva jednako moguća objekta | `SDPKOTA` ručno ili pomjerite oznaku |
| Neispravna oznaka | tekst nije kota ili je nejasan (npr. `5,20` bez predznaka) | ispravite tekst |
| Konflikt | dvije oznake istog objekta se ne slažu | provjerite crtež |

4. **Pregled:** cijan linije od svake oznake do njenog objekta (sloj `VU-KOTE-DODJELA`).
5. *Primijeniti N kota na označene objekte? [Da/Ne]* (podrazumijevano **Ne**). Linije pregleda se brišu u oba
   slučaja.

**Prepoznaje oznake:**
* relativne: `+0,00`, `-0,15`, `±0,00`, `%%p0.00`;
* apsolutne: `KT 520,00`, `520,15 m n.v.`, `502,90 mns`;
* MText s formatiranjem.

Broj veći od 50 bez predznaka smatra se apsolutnim.

**Rezultat:** jedan UNDO poništava cijelu dodjelu.

#### SDPSPOTLEVEL – oznaka visinske kote
* Kliknite tačku (Enter za kraj).
* *Kota [IzTacke/Unos]*:
  * **IzTacke** – koristi Z kliknute tačke;
  * **Unos** – upišite relativnu kotu.
* Oznaka prikazuje relativnu kotu i, ako je datum zadan, apsolutnu u zagradi: `+0,25 (504,25)`.

#### SDPSLOPE – strelica nagiba
* Kliknite **višu**, pa **nižu** tačku.
* *Nagib [IzTacaka/Unos]*:
  * **IzTacaka** – nagib iz Z razlike;
  * **Unos** – upišite procente.

#### SDPTEREN – model terena (TIN)
1. *Granica terena [Odaberi/Bez/Zadrzi]*:
   * **Odaberi** – zatvorena polilinija koja ograničava teren;
   * **Bez** – bez granice;
   * **Zadrzi** – ostaje prethodna granica.
2. *Koristiti oznake kota kao visinske tačke terena? [Da/Ne]* – `Da` samo ako su oznake stvarno kote **terena**,
   a ne ulaza ili saobraćajnica.
3. Program gradi mrežu trouglova iz mapiranih **tačaka terena** (Z), **izohipsi** i **prelomnih linija**, koje se
   poštuju kao ivice. Ispisuje napomene i nalaze `SDP-T…`.
4. Ako podataka **nema dovoljno**, ništa se ne crta i piše zašto, npr.:
   * manje od 3 tačke;
   * sve tačke na jednoj liniji;
   * sve na istoj visini (često Z = 0 u 2D crtežu).

**Rezultat:**
* mreža na sloju `VU-TEREN` (PolyFaceMesh, više komada za velike terene);
* krugovi „Najniža tačka” na `VU-TEREN-ANALIZA`;
* teren se izvozi u SketchUp (`SDPEXPORT`).

Ponovno pokretanje **zamjenjuje** stari teren.

#### SDPPROFIL – profil terena
1. Teren mora postojati (`SDPTEREN`).
2. Odaberite liniju ili poliliniju trase.
3. *Nadvišenje* – npr. `5` crta visine 5 puta naglašeno.
4. Kliknite tačku umetanja.

**Rezultat:** linija terena, bazna linija, vertikale sa stacionažama i kotama, naslov s baznom kotom.

#### SDPANALIZATERENA – provjera terena
Ispisuje:
* raspon nagiba;
* ravne dijelove (zadržavanje vode) i strme dijelove, prema pravilima projekta;
* lokalne najniže tačke – označava ih na `VU-TEREN-ANALIZA`.

**Ovo nije hidraulički proračun** – samo provjera geometrije.

### 6.5 Projektovanje

#### SDPPLACE – postavljanje tačkastog elementa
1. Tip (v. [1.3](#13-biranje-tipa-elementa)). Ako izaberete linijski ili površinski tip, automatski se prelazi na
   `SDPDRAW`.
2. **Simbol prati miš** – kliknite tačku umetanja. Rade OSNAP, ORTHO i POLAR.
3. *Rotacija <0°>* – upišite ugao ili kliknite drugu tačku smjera. Enter = ista rotacija kao prethodni.
4. Ponavlja se dok ne pritisnete **Enter**: možete postaviti mnogo stabala zaredom.

**Rezultat:** *Postavljeno: N × Listopadno drvo.* Svaki element dobija sljedeću oznaku (T-001, T-002…).

#### SDPDRAW – crtanje trase ili površine
1. Tip (samo linijski i površinski).
2. Za površinu – *Granica [Polilinija/Pravougaonik/Unutar/Odaberi]*; za trasu – *Trasa [Polilinija/Odaberi]*:
   * **Polilinija** – pokreće se standardni `PLINE` (lukovi, snapovi); Enter završava;
   * **Pravougaonik** – standardni `RECTANG`;
   * **Unutar** – klikajte **unutar** zatvorenih površina, a program sam nalazi granicu (kao šrafura); Enter za kraj;
   * **Odaberi** – odaberite postojeće krive.
3. Nacrtani ili odabrani objekat postaje element (kao `SDPCONVERT`).

#### SDPEDIT – izmjena elemenata
1. Odaberite elemente (može i prije naredbe).
2. Ispis svih parametara s brojevima **P1, P2…** i trenutnim vrijednostima (više vrijednosti: `a | b | ...`).
3. *Parametar za izmjenu [P1...Pn/Oznaka/Svojstvo/Izlaz]*:
   * **P1…Pn** – unos nove vrijednosti, zavisno od vrste parametra:
     * broj ili dužina u jedinici parametra;
     * `Da/Ne`;
     * **lista opcija:** ispisuje se `1 = …, 2 = …`, upišite redni broj;
     * **materijal:** ključ materijala; `?` ispisuje sve materijale;
   * **Oznaka** – nova oznaka (samo za jedan element; za više koristite `SDPNUMBER`);
   * **Svojstvo** – opisni podatak, npr. `botanicalName`, `manufacturer`, `notes`. Vrijednost `-` briše svojstvo.
   * **Izlaz**.
4. Izmjena se odmah primjenjuje i prikaz se osvježava. Vrijednost izvan dozvoljenog raspona se odbija s porukom.

Za različite tipove odjednom mijenjaju se samo zajednički parametri.

#### SDPSETPARAM
Pomoćna naredba palete i skripti: handle(ovi) elemenata odvojeni zarezom, naziv parametra, vrijednost. Obično je ne
koristite direktno.

#### SDPSELECT – odabir elemenata
*Odabir po [Tipu/Kategoriji/Oznaci/Id/Materijalu/Sve]*:

| Opcija | Šta odabire |
|---|---|
| Tipu | sve elemente izabranog tipa |
| Kategoriji | sve elemente kategorije (`landscape`, `furniture`…) |
| Oznaci | po oznaci ili prefiksu: `T-` (sva stabla), `T-012` |
| Id | po identifikatoru ili njegovom početku |
| Materijalu | po ključu materijala (npr. `granite_grey`) |
| Sve | sve elemente |

Rezultat ostaje odabran za sljedeću naredbu (npr. `SDPEDIT`, `SDPLABEL`, `ERASE`).

#### SDPZOOM
Zumira na element po handleu. Koristi je paleta; ručno rijetko.

#### SDPPATHARRAY – niz elemenata duž trase
1. Odaberite trasu (polilinija, linija, luk).
2. Tip tačkastog elementa (stablo, svjetiljka, stubić, stalak).
3. *Razmak (m)* – npr. `8`.
4. *Način razmaka [Fiksni/Uklopi]*:
   * **Fiksni** – tačno zadani razmak, a ostatak ostaje na kraju;
   * **Uklopi** – razmak se prilagodi da prvi i zadnji budu tačno na početku i kraju.
5. *Odmak od početka (m)*, *Odmak od kraja (m)* – prazan prostor na krajevima.
6. *Bočni odmak lijevo od trase (m)* – npr. `1,5` (negativno = desno). Tako se drvored postavi pored ose staze.
7. *Rotacija [Poravnaj/Fiksna]*:
   * **Poravnaj** – svaki element prati smjer trase, uz *dodatnu rotaciju*;
   * **Fiksna** – svi isti ugao.

**Rezultat:** *Duž trase postavljeno: N × …* – jedan UNDO.

#### SDPPARKING – niz parking mjesta
1. *Tip parking mjesta* – podrazumijevano `road.parking_bay`; može i za osobe s invaliditetom, s punjačem…
2. Kliknite **početak** i **kraj ivice prolaza** (ivica uz koju su mjesta).
3. Kliknite **tačku na strani** gdje su mjesta.
4. *Ugao parking mjesta prema prolazu (°)* – `90` okomito, `60` ili `45` koso.

**Rezultat:**
* mjesta širine iz tipa (npr. 2,50 m), numerisana 1, 2, 3… (parametar *broj mjesta*);
* jedan UNDO.

#### SDPGRID – raster sadnje u površini
1. Odaberite **zatvorenu** granicu.
2. Tip (grm, stablo, trajnica…).
3. *Razmak u redu (m)*, *Razmak redova (m)*.
4. *Smaknuti raspored (trouglasti) [Da/Ne]* – `Da` je gušći i prirodniji raspored.
5. *Udaljenost od granice (m)*.

Raster je okrenut po prvoj stranici granice.

#### SDPCLEARANCE – zone razmaka
Odaberite tačkaste elemente. Crtaju se zone servisa, sigurnosti i podizanja (npr. kontejner, igralište) **iz
vrijednosti unesenih u element**. Elementi bez tih vrijednosti se ne crtaju. Sloj: zone za pregled.

#### SDPLIGHTCOVER – ilustrativni domet rasvjete
1. Odaberite svjetiljke.
2. *Ilustrativni radijus kada nije zadan (m)*.

Crtaju se krugovi. **Nije fotometrijski proračun**, samo pomoć pri rasporedu.

### 6.6 Nacrti, oznake, količine

#### SDPLABEL – oznake elemenata
* Odaberite elemente.
* Elementi bez oznake prvo dobiju sljedeći broj.
* Postavlja se multileader s oznakom i ključnim podacima.
* Oznake se same osvježe nakon prenumeracije.

#### SDPNUMBER – numeracija
*Numeracija [Nedostajuce/Sve]*:
* **Nedostajuce** – brojeve dobijaju samo elementi bez oznake;
* **Sve** – prenumeracija svega, od sjevera prema jugu i od zapada prema istoku.

Prefiksi dolaze iz kataloga (T stabla, B objekti, P površine, PB parking…).

#### SDPAREALABEL, SDPWIDTHLABEL
* `SDPAREALABEL` – oznaka površinskih elemenata: šifra, tip, površina u m².
* `SDPWIDTHLABEL` – oznaka širine staza, saobraćajnica i zidova.

#### SDPLEGEND
Kliknite gornji lijevi ugao. Legenda sadrži **samo tipove koji postoje u crtežu**.

#### SDPTITLEBLOCK – okvir i sastavnica
1. Donji lijevi ugao lista.
2. *Format lista [A0/A1/A2/A3/A4]*.
3. *Naziv crteža*, *Broj lista* (npr. `VU-100`).

Okvir je u mjerilu projekta. U sastavnicu idu podaci iz `SDPSETTINGS` (projekat, investitor, autor, revizija).

#### SDPSCALEBAR, SDPSECTION, SDPDETAIL, SDPGRIDLINES, SDPREVCLOUD

| Naredba | Koraci |
|---|---|
| `SDPSCALEBAR` | početna tačka, dužina u m, broj podjela |
| `SDPSECTION` | početak i kraj linije presjeka, oznaka (`A`) |
| `SDPDETAIL` | položaj, broj detalja, list |
| `SDPGRIDLINES` | dva ugla, razmak mreže u m – ose s oznakama |
| `SDPREVCLOUD` | dva ugla, napomena revizije (podrazumijevano „A: izmijenjeno”) |

#### SDPSCHEDULE – tabele količina u crtežu
1. Ispisuje dostupne tabele: **T1, T2…** (drveće i zasadi, popločanje, mobilijar, rasvjeta, odvodnja, otpad,
   saobraćaj…). Nude se samo tabele za koje u crtežu postoje elementi.
2. *Tabela [T1...Tn/Sve]*.
3. Kliknite gornji lijevi ugao.

Više tabela se slaže jedna ispod druge. Svaka ima red **Ukupno**.

#### SDPSCHEDULECSV – tabele za Excel
* Pita za naziv datoteke.
* Zapisuje **jednu CSV datoteku po tabeli** (`naziv-trees.csv`…) sa separatorom `;` i decimalnim zarezom, pa se
  direktno otvara u Excelu s bosanskim postavkama.
* Uz svaki CSV ide i JSON.
* Kolona *ID elemenata* povezuje redove s objektima crteža.

#### SDPQUANTITIES – brze količine
Ispis po tipu u komandnoj liniji: komadi, dužine, površine, zapremine.
* **Izmjereno** iz geometrije: bez znaka.
* **Izračunato** iz parametara: npr. površina iz širine × dužine.
* **Procjena:** označena `~`.

Nedostajuće dimenzije se ne izmišljaju.

### 6.7 Provjera i razmjena

#### SDPVALIDATE – provjera projekta
Provjerava sve elemente:
* geometriju (`SDP-G`) i parametre (`SDP-P`);
* podatke (`SDP-D`) i jedinice (`SDP-U`);
* kolizije, preklapanja i razmake (`SDP-C`);
* kote (`SDP-E`) i stavke za stručni pregled (`SDP-R`).

Ispisuje prvih 40 nalaza; svi su u paleti (*Provjera*), gdje dvostruki klik vodi do objekta.

**To su upozorenja softvera, a ne potvrda usklađenosti s propisima.**

#### SDPEXPORT – izvoz paketa za SketchUp
1. Prvo provjera. Ako ima **grešaka**, pita *Ipak izvesti? [Da/Ne]* (podrazumijevano Ne). Elementi s greškom mogu
   biti preskočeni.
2. Izbor datoteke `.sdpkg`; nudi zadnju korištenu putanju.
3. Elementima izmijenjenim od zadnjeg izvoza **povećava reviziju**, pa SketchUp zna šta je novo.
4. Ako je teren uključen (`SDPTEREN`), dodaje i teren.

**Rezultat:** *Izvezeno elemenata: N i teren → putanja*. Preskočeni elementi se navode pojedinačno.

#### SDPAPPLYCHANGES – izmjene iz SketchUpa
1. Odaberite datoteku izmjena (`.json`) iz SketchUpa (*Izvoz izmjena za AutoCAD...*).
2. Ako pripada drugom projektu – pita da li nastaviti.
3. *Elementi izmijenjeni u AutoCAD-u nakon izvoza [Preskoci/Prepisi]*:
   * **Preskoci** (preporučeno) – Vaše novije AutoCAD izmjene ostaju; takvi elementi se javljaju kao *konflikt*;
   * **Prepisi** – SketchUp verzija ima prednost.
4. Ishodi:

| Ishod | Značenje |
|---|---|
| primijenjeno | izmjena je unesena |
| bez promjene | vrijednost je ista |
| konflikt | element je u međuvremenu mijenjan u AutoCAD-u |
| nedostaje | element je obrisan u AutoCAD-u |
| neispravno | vrijednost nije dozvoljena |

Na kraju ponovo pokrenite `SDPEXPORT`, da SketchUp dobije usklađeno stanje.

### 6.8 Sigurnost i pomoć
`SDPKOPIJA`, `SDPKOPIJEPOSTAVKE`, `SDPOPORAVAK` – v. [poglavlje 10](#10-sigurnosne-kopije-i-oporavak).
`SDPPOMOC` – otvara ovu dokumentaciju u pregledniku.

---

## 7. Kote – kako program računa

* **Nulta kota (±0,00)** – apsolutna visina poda prizemlja, npr. **504,00 m n.v.**
* **Apsolutna kota** = nulta kota + relativna kota.
* **Parter** – podrazumijevano jednak ±0,00. Može biti drugačiji (npr. −0,15).
* **Saobraćajnica** = parter − spuštanje. Spuštanje zadaje korisnik (npr. 15 cm); program ga **ne pretpostavlja**.

Primjer (±0,00 = 504,00; parter = ±0,00; saobraćajnice 15 cm ispod partera):

| Objekat | Zadano | Relativno | Apsolutno |
|---|---|---|---|
| Objekat | Relativna 0 | ±0,00 | 504,00 |
| Terasa | Parter +0,05 | +0,05 | 504,05 |
| Saobraćajnica | Saobracajnica 15 cm | −0,15 | 503,85 |
| Ulaz u garažu | Relativna −1,10 | −1,10 | 502,90 |

**Z koordinate u crtežu** (`SDPDATUM > Zkoordinate`):
* **Relativne** – Z = 0 znači ±0,00 (uobičajeno za situacije crtane u 2D);
* **Apsolutne** – Z = 504 znači 504 m n.v. (uobičajeno za geodetske podloge).

**Provjera oznaka:** kad su na istom mjestu apsolutna i relativna oznaka (npr. `502,90 mns` i `-1,10`), njihova
razlika mora biti jednaka nultoj koti. Ako nije, to je greška u crtežu ili u nultoj koti.

**Šta program nikad ne radi:**
* ne izmišlja kotu koje nema;
* ne dodjeljuje kotu cijelom području;
* ne pogađa nejasnu oznaku;
* ne koristi 15 cm ili 518,56 kao podrazumijevanu vrijednost.

---

## 8. SketchUp – svaka stavka menija i dijaloga

Meni **Extensions → Vanjsko uređenje PRO**, traka alata *Vanjsko uređenje PRO*, i desni klik na element.

### 8.1 Uvoz paketa...
1. Izbor datoteke: `.sdpkg` ili `manifest.json` iz raspakovanog paketa.
2. **Dijalog uvoza**:
   * **Nivo detalja**:
     * *Šematski prikaz (lagano)* – jednostavni volumeni, brzo, za velike projekte;
     * *Standardni kvalitet* – preporučeno;
     * *Visoki kvalitet (detaljno)* – više detalja, sporije;
   * **Lokalno izmijenjeni elementi**:
     * *Zadrži izmjene iz SketchUp-a, prijavi konflikt* (preporučeno);
     * *Prepiši verzijom iz AutoCAD-a*;
   * **Elementi obrisani u AutoCAD-u**:
     * *Premjesti na skrivenu oznaku „Obrisano u izvoru”* (preporučeno – ništa se ne gubi);
     * *Obriši iz modela*;
     * *Zadrži nepromijenjeno*;
   * ☑ **Izradi scene za pregled** – scene VU 01–10;
   * ☑ **Sigurnosna kopija prije uvoza** – samo ako je model sačuvan;
   * kartica **Planirane izmjene** – tabela svake izmjene (novo, izmijenjeno, obrisano, konflikt…) s filterom
     *Sve izmjene*;
   * kartica **Provjera** – nalazi iz paketa;
   * kartica **Paket i visine** – podaci paketa: projekat, jedinice, nulta kota, visinski datum, koordinatni sistem,
     teren.
3. **Uvezi** – gradi model; **Zatvori** – ništa ne mijenja.
4. Nakon uvoza: izvještaj (broj novih, ažuriranih, grupa, komponenti, trajanje). **Sačuvaj izvještaj...** zapisuje ga
   kao tekst.

Cijeli uvoz je jedna operacija **Edit → Undo**.

### 8.2 Ažuriranje iz paketa
Ponovo čita **zadnji uvezeni paket** (isti put) i prikazuje isti dijalog s planiranim izmjenama. Koristite nakon
novog `SDPEXPORT` u AutoCAD-u. Nepromijenjeni elementi se ne diraju. Teren se ažurira, a ne duplira.

### 8.3 Svojstva elementa
Odaberite jedan element (ili desni klik → *Vanjsko uređenje PRO → Svojstva elementa*).
* **Element** – oznaka, tip, kategorija, revizija, vrijeme uvoza.
* **Parametri** – po grupama, sa jedinicama. Izmjena + **Primijeni** ponovo gradi element.
  **Poništi izmjene** vraća prikazane vrijednosti.
* **Svojstva** – opisni podaci iz AutoCAD-a (botanički naziv, proizvođač…). Ovdje se samo čitaju.
* **Odaberi isti tip** – odabire sve elemente tog tipa.

Izmjene ostaju u SketchUpu dok ih ne pošaljete u AutoCAD (8.4).

### 8.4 Izvoz izmjena za AutoCAD...
Zapisuje datoteku izmjena (`sdp-izmjene.json`) sa:
* izmijenjenim parametrima;
* pomjerenim ili zarotiranim **tačkastim** elementima.

Promjene oblika staza i površina se **ne prenose**; javljaju se kao neprenosive. U AutoCAD-u: `SDPAPPLYCHANGES`.

### 8.5 Izrada scena za pregled

| Scena | Sadržaj |
|---|---|
| VU 01 Situacija | pogled odozgo, bez zona i podzemnog |
| VU 02 3D pregled | aksonometrija |
| VU 03 Pješačke površine | staze, površine, mobilijar, objekti |
| VU 04 Saobraćajnice i parking | saobraćaj i površine |
| VU 05 Zelenilo i teren | zelenilo, teren, voda |
| VU 06 Rasvjeta | rasvjeta i površine |
| VU 07 Voda i odvodnja | teren, voda, odvodnja |
| VU 08 Podzemne instalacije | pogled odozdo, X-ray |
| VU 09 Pregled materijala | sve osim zona |
| VU 10 Prezentacija | perspektiva bez pomoćnih slojeva |

Ponovno pokretanje osvježava postojeće scene.

### 8.6 Prikaz podzemnog
Uključuje ili isključuje: skriva površine i prikazuje ukopane instalacije (šahtovi, cijevi, podzemni kontejneri) u
X-ray prikazu.

### 8.7 Biblioteka 3D modela...
Lokalna biblioteka realističnih 3D modela za svaki objekt iz kataloga (253 modela u 14 kategorija). Radi bez interneta;
datoteke su u folderu proširenja `pb_site_design_pro/Assets/<Kategorija>/<ID modela>/`.

**Prozor:**
* **lijevo** – 14 kategorija (Infrastruktura i površine, Saobraćaj i mobilnost, Biciklistička infrastruktura, Vodeni
  elementi, Zelenilo i sadnja, Komunalna infrastruktura, Rasvjeta, Ograde i zidovi, Nadstrešnice i zasjena, Mobilijar i
  rekreacija, Eko tehnologije, Vanjski klimatski sistemi, Dekorativni elementi, Informativni i sigurnosni sistemi) s
  brojem modela;
* **sredina** – podkategorije (dugmad iznad), pretraga po nazivu ili ID-u (traži kroz sve kategorije), ☑ *U ovom
  modelu* i mreža pregleda (slike);
* **desno** – odabrani model: velika slika, kategorija/podkategorija, vrsta (tačkasti, linijski, površinski),
  dimenzije uzorka, **veza s AutoCAD-om** (ID modela = tip elementa koji nosi svaki AutoCAD element), vrsta modela, status, **izvor i licenca**, materijali,
  broj u modelu i obrazac **parametara** (dužina, visina, materijal…).

**Dugmad:**
* **Umetni u model**
  * *tačkasti* model (klupa, stub, kontejner…): pokazivač postaje alat za umetanje. Klik = umetni (ponavlja se dok ne
    pritisnete Esc), **strelice lijevo/desno** = rotacija ±15°, ili upišite ugao u stepenima pa Enter. Plavi okvir
    pokazuje tlocrt, crvena linija smjer (+X) modela.
  * *linijski* model (zid, ograda, živica, kanal…): ako su u modelu odabrane **ivice** koje čine jednu liniju, model se
    izrađuje duž njih; inače klik umeće uzorak zadane dužine.
  * *površinski* model (bazen, pergola, popločanje, gredica…): ako je odabrano **lice**, model dobija njegovu konturu
    (s otvorima); inače klik umeće pravougaonik zadane širine i dubine.
* **Zamijeni odabrano** – zamjenjuje odabrane elemente ovim modelom na istom mjestu, s istom rotacijom i oznakom. Radi
  za elemente umetnute iz biblioteke i za obične komponente/grupe. Elemente iz AutoCAD paketa **ne mijenja** (tip se
  mijenja u AutoCAD-u, ili tipu dodijelite komponentu u *Biblioteka i postavke*) – to se navodi u poruci.
* **Uvezi DAE** – uvozi `model.dae` SketchUpovim uvozom (kao nezavisnu komponentu, bez parametara).
* **Otvori folder** – otvara folder modela (`model.dae`, `asset.json`, `preview.jpg`).
* **Odaberi** (u redu *U ovom modelu*) – odabire sve elemente tog tipa.

**Važno:**
* Umetnuti model nosi tip, parametre i geometriju kao uvezeni element, pa ga mijenjate u **Svojstva elementa**.
* Elementi iz biblioteke nisu dio AutoCAD projekta i **ne šalju se** naredbom *Izvoz izmjena za AutoCAD*.
* Nivo detalja umetanja je onaj iz *Biblioteka i postavke → Postavke*.
* Ponovljeni dijelovi (biljke u gredici, stubovi ograde) su komponente, pa model ostaje malen.
* Ako datoteka modela nedostaje, to se piše u dnu prozora i dugme *Umetni* je onemogućeno – model se nikad ne
  zamjenjuje drugim.

### 8.8 Biblioteka i postavke...
* **Tipovi elemenata**:
  * pretraga;
  * filter: *Sve kategorije* / *U ovom modelu* / *Omiljeni* (☆ klik);
  * kolone: tip, generator, broj u modelu, 3D prikaz;
  * **Dodijeli komponentu...** – odaberite svoj `.skp` (npr. pravi model klupe proizvođača). Svi elementi tog tipa
    se prikazuju tom komponentom.
  * **Ukloni** – vraća parametarski model.
  * **Odaberi** – odabire elemente tog tipa u modelu.
* **Materijali** – ključ, naziv, boja, neprozirnost materijala iz kataloga.
* **Postavke**:
  * podrazumijevani nivo detalja i pravila ažuriranja;
  * ☑ *Izradi scene za pregled nakon uvoza*;
  * ☑ *Postavi sjever za sunce iz paketa*;
  * sigurnosne kopije: uključeno, broj kopija po modelu, najveća starost u danima (0 = bez ograničenja);
  * **Sačuvaj postavke**.

### 8.9 Izvještaj posljednjeg uvoza, Otvori dnevnik (log), O programu
* izvještaj ponovo prikazuje zadnji uvoz;
* dnevnik (`site_design_pro.log`) – pošaljite ga uz prijavu problema;
* *O programu* – verzija.

### 8.10 Sigurnosne kopije i oporavak...
Lista prekinutih operacija (npr. SketchUp se srušio tokom uvoza) i sigurnosnih kopija modela.

### 8.11 Organizacija modela
* Korijenska grupa projekta → grupa po modelskoj grupi (oznake/tagovi `VU Objekti`, `VU Zelenilo`, `VU Parter`…) →
  elementi.
* Ponovljeni elementi (stabla, parking) su **komponente**: jedna definicija, mnogo instanci, mala datoteka.
* Podaci elementa su u rječniku atributa `SiteDesignPro`.

---

## 9. Razmjena AutoCAD ↔ SketchUp – pravila

1. **AutoCAD je glavni izvor tlocrta.** Položaj staza, površina i objekata mijenjajte u AutoCAD-u, pa izvezite.
2. **SketchUp gradi 3D** iz parametara. Visine, debljine, krošnje i materijali dolaze iz parametara elementa.
3. **Natrag u AutoCAD** idu samo parametri i pomjeranje ili rotacija tačkastih elemenata.
4. **Ništa se ne gubi tiho:**
   * lokalne izmjene u SketchUpu se zadržavaju i prijavljuju kao konflikt;
   * obrisano u AutoCAD-u se skriva, a ne briše (osim ako odaberete brisanje).
5. **Kopiranje elemenata u SketchUpu** pravi „odvojene kopije”. One više ne prate AutoCAD.

---

## 10. Sigurnosne kopije i oporavak

**AutoCAD**
* `SDPKOPIJA` – odmah pravi provjerenu kopiju zadnje **sačuvane** verzije crteža i ispisuje listu kopija.
* `SDPKOPIJEPOSTAVKE`:
  * *Automatska sigurnosna kopija prije većih operacija [Ukljuceno/Iskljuceno]*;
  * *Broj kopija po crtežu* – najnovija se nikad ne briše;
  * *Najveća starost kopija u danima* – 0 = bez ograničenja.
* `SDPOPORAVAK` – lista prekinutih operacija i kopija, pa *Oporavak [Otvori/Folder/Oznaci/Izlaz]*:
  * **Otvori** – broj kopije s liste (1 = najnovija). Kopija se otvara **kao novi crtež**; trenutni crtež se nikad
    ne prepisuje.
  * **Folder** – otvara folder `_SDP_backup`.
  * **Oznaci** – prekinute operacije označava kao pregledane, pa se poruka pri pokretanju više ne pojavljuje.
* Pri pokretanju AutoCAD-a dodatak javlja ako je neka operacija prekinuta (npr. nestanak struje).

**Gdje su kopije:** folder `_SDP_backup` pored crteža, imena oblika `crtez_20261029-153012_<operacija>.dwg`. Svaka kopija se
provjerava (SHA-256) odmah nakon pisanja.

**SketchUp:** isto, kroz *Sigurnosne kopije i oporavak...* i postavke u *Biblioteka i postavke → Postavke*.

---

## 11. Šifre nalaza – šta znače i šta uraditi

Ozbiljnost:
* **GREŠKA** – mora se riješiti;
* **UPOZORENJE** – pogledajte;
* **ZA STRUČNI PREGLED** – provjera stručnjaka;
* **INFORMACIJA**.

### Ulazni DWG (`SDPPROVJERADWG`)
| Šifra | Značenje | Šta uraditi |
|---|---|---|
| SDP-I001 | mapirani sloj ne postoji ili je prazan | ispravite mapiranje |
| SDP-I002 | sloj ima više uloga | jedan sloj – jedna uloga |
| SDP-I003 | granica nacrtana pojedinačnim linijama | `JOIN` / `PEDIT` u zatvorenu poliliniju |
| SDP-I004 | granica siječe samu sebe | ispravite tjemena |
| SDP-I005 | početak = kraj, ali nije „Closed” | uključite Closed (program je tretira kao zatvorenu) |
| SDP-I006, I007 | granica nije zatvorena (razmak krajeva) | zatvorite – program ne spaja sam |
| SDP-I008 | duplikat geometrije | obrišite duplikat |
| SDP-I010, I011 | nema parcelne granice | mapirajte sloj parcele |
| SDP-I012, I013 | površina parcele nevjerovatna | provjerite jedinice crteža |
| SDP-I020 | nema tlocrta ni oznake objekta | mapirajte tlocrte |
| SDP-I021 | samo oznake objekata, bez tlocrta | dimenzije se unose ručno (`SDPPREUZMI`) |
| SDP-I022 | objekat izlazi izvan parcele | provjerite položaj |
| SDP-I030 | nema saobraćajnica | mapirajte ako postoje |
| SDP-I031 | nema linije partera | preporuka: poseban sloj partera |
| SDP-I040, I041 | oznaka kote nečitljiva ili nejasna | ispravite tekst |
| SDP-I042 | oznaka nije u skladu s nultom kotom | provjerite ±0,00 ili tekst |
| SDP-I043 | dvije oznake na istom mjestu se razlikuju | provjerite crtež |
| SDP-I044, I045 | tačke ili izohipse nemaju Z (sve 0) | podloga je 2D – potreban 3D snimak |
| SDP-I046 | nema nikakvih podataka o visinama | dodajte kote ili snimak |
| SDP-I047 | nulta kota nije definisana | `SDPDATUM` |
| SDP-I050 | objekat nije u ravni XY | `FLATTEN` ili precrtajte |
| SDP-I060 | koordinatni sistem nije naveden / lokalne koordinate | `SDPGEOREF` |

### Geometrija, parametri, podaci, jedinice
| Šifra | Značenje |
|---|---|
| SDP-G001–G012 | neispravna geometrija: nula dužina ili površina, dupla tjemena, < 3 tjemena, samopresijecanje (G007), otvor izvan granice (G008), aproksimacija pravougaonikom (G009), visina izvan raspona (G012 – često pogrešne jedinice) |
| SDP-G020 | objekat nije polilinija u ravni XY |
| SDP-G021 | simbol bloka skaliran ili preslikan – dimenzije mijenjajte kroz `SDPEDIT` |
| SDP-P001–P008 | parametar nedostaje, izvan raspona, pogrešne vrste, nepoznat materijal |
| SDP-P010–P019 | logičke greške parametara (npr. naslon niži od sjedišta, voda dublja od bazena) |
| SDP-D001–D003 | neispravan ili dupli identifikator (kopija – `SDPAUDIT`), nepoznat tip |
| SDP-D010 | ista oznaka na više elemenata – `SDPNUMBER` |
| SDP-U001, U002 | INSUNITS se razlikuje od jedinice projekta ili nije postavljen – `SDPSETTINGS > Jedinice` |

### Kolizije, lokacija, kote
| Šifra | Značenje |
|---|---|
| SDP-C001 | moguća kolizija tačkastih elemenata (npr. dva parking mjesta jedno preko drugog) |
| SDP-C002 | element unutar zadanog razmaka drugog elementa |
| SDP-C003 | površine se preklapaju |
| SDP-C004 | površina preklapa tlocrt objekta |
| SDP-C005 | krošnja u zrelosti dopire do objekta ili elementa |
| SDP-C010 | širina manja od zadane potrebne |
| SDP-C011 | nagib rampe veći od zadanog najvećeg |
| SDP-C020 | element izlazi izvan parcelne granice |
| SDP-E000 | problem visinskog sistema projekta |
| SDP-E010 | saobraćajnica iznad partera |
| SDP-E011 | kota dodijeljena, a nulta kota više nije definisana |
| SDP-E012 | objekat pomjeren po visini nakon dodjele kote |
| SDP-R001 | podatak za stručni pregled nije unesen (npr. botanički naziv) – zadane vrijednosti su orijentacione |

### Teren
| Šifra | Značenje |
|---|---|
| SDP-T001 | tačke bez visine se ne koriste |
| SDP-T002 | manje od 3 upotrebljive tačke |
| SDP-T003 | sve tačke na jednom pravcu |
| SDP-T004 | sve tačke na istoj visini (često Z = 0) |
| SDP-T005 | podaci pokrivaju mali dio granice – ostalo je nepouzdano |
| SDP-T010 | dvije tačke na istom mjestu s različitim visinama |
| SDP-T011 | prelomne linije ili izohipse se sijeku |
| SDP-T012 | prelomnica nije ugrađena u mrežu |
| SDP-T020 | ravni dijelovi – moguće zadržavanje vode |
| SDP-T021 | strmi dijelovi |
| SDP-T022 | trouglovi ravno na jednoj izohipsi – dodajte kote vrhova ili dna |
| SDP-T023 | lokalna najniža tačka – voda nema odvod |
| SDP-T024 | rijetki podaci – teren je interpoliran |

### Paket razmjene (SketchUp)
| Šifra | Značenje |
|---|---|
| SDP-F001 | paket se ne može pročitati (oštećen, nije paket) |
| SDP-F002 | paket je noviji od programa – nadogradite proširenje |
| SDP-F010–F014 | neispravni ili nepotpuni zapisi geometrije |
| SDP-F020–F022 | problem s datotekom modela (komponente) u paketu |
| SDP-F030 | paket navodi teren, ali datoteka terena nedostaje |
| SDP-B001 | model nije sačuvan, pa sigurnosna kopija nije napravljena (uvoz se poništava s Undo) |

---

## 12. Česti problemi i rješenja

**Kartica VANJSKO UREĐENJE PRO se ne pojavljuje.**
* Pokrenite `SDPABOUT`. Ako je naredba nepoznata, dodatak nije učitan.
* Provjerite instalaciju (`INSTALIRAJ.bat`) i *Trusted Locations*.
* Pokrenite ponovo AutoCAD.

**Površine i dužine su 100 ili 1000 puta pogrešne.**
Jedinica crteža nije dobra. `SDPSETTINGS > Jedinice` – odaberite jedinicu u kojoj je stvarno crtano. Provjera: poznata
dužina (parking 5 m) mora imati 5000 jedinica u mm, 500 u cm, 5 u m.

**`SDPPREUZMI` preskače parcelu ili objekte.**
Polilinije nisu zatvorene. `SDPPROVJERADWG` pokazuje koje (SDP-I006/I007). Zatvorite ih s `PEDIT → Close` ili `JOIN`.

**`SDPKOTEIZOZNAKA` kaže „Bez objekta” za sve oznake.**
Oznake nisu unutar zatvorenih kontura mapiranih objekata (npr. stoje na ivici ceste). Kote dodijelite ručno
(`SDPKOTA`) ili zatvorite konture saobraćajnica i partera i mapirajte ih.

**Teren se ne pravi.**
Pogledajte poruke `SDP-T002/T003/T004`. Najčešće crtež nema geodetski snimak s Z visinama. Oznake kota ulaza i
saobraćajnica nisu teren.

**Mnogo SDP-C020 (izvan parcele).**
Uobičajeno za stabla i elemente u javnoj površini uz ulicu. Provjerite da li je to namjerno ili je pogrešna granica.

**SDP-C001 kod parking mjesta.**
U DWG-u su blokovi mjesta postavljeni jedan preko drugog, npr. invalidsko preko običnog ili dva na istom mjestu.
Obrišite višak.

**Nakon kopiranja elemenata provjera javlja duple identifikatore (SDP-D002).**
Pokrenite `SDPAUDIT`.

**SketchUp: paket se uvozi, ali modeli su daleko od ishodišta.**
Postavite ishodište projekta blizu parcele (`SDPSETTINGS > Ishodiste`) i ponovo izvezite.

**SketchUp: promijenio sam stazu, a izmjena nije otišla u AutoCAD.**
Oblik staza i površina se mijenja samo u AutoCAD-u ([poglavlje 9](#9-razmjena-autocad--sketchup--pravila)).

**Šta poslati uz prijavu problema:**
* ispis `SDPABOUT`;
* tekst iz komandne linije;
* dnevnik iz `%LOCALAPPDATA%\SiteDesignPro\logs\`;
* po mogućnosti, crtež.

---

## 13. Pojmovnik

| Pojam | Značenje |
|---|---|
| Element | objekat crteža s tipom, parametrima i oznakom koji dodatak prati |
| Tip elementa | definicija iz kataloga (npr. `landscape.tree_deciduous`) – određuje sloj, simbol, parametre i 3D model |
| Parametar | mjera ili izbor elementa (visina, širina, materijal) |
| Svojstvo | opisni podatak (botanički naziv, proizvođač, napomena) |
| Oznaka | broj elementa u crtežu (T-001) |
| Mapiranje | veza sloj ulaznog crteža → uloga (parcela, objekti, kote…) |
| ±0,00 / nulta kota | apsolutna visina poda prizemlja objekta |
| Parter | uređena površina oko objekta |
| m n.v. / mns | metara nadmorske visine |
| TIN | mreža trouglova terena |
| Prelomna linija | linija nagle promjene nagiba (ivica, nasip) koju teren poštuje |
| Ishodište projekta | tačka crteža koja u SketchUpu postaje 0,0,0 |
| Paket (.sdpkg) | datoteka razmjene AutoCAD → SketchUp |
| Nivo detalja (LOD) | šematski / standardni / visoki kvalitet 3D modela |
| Komponenta | SketchUp definicija koju dijeli više istih elemenata |
| Revizija | brojač izmjena elementa između izvoza |
| EPSG | međunarodna šifra koordinatnog sistema (npr. 3908) |
