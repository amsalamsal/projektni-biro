# Prvi projekat – uputstvo za početnike

Ovo uputstvo vodi korak po korak od praznog računara do gotovog 3D modela vanjskog uređenja u SketchUpu.
Pisano je za osobu koja **nikad nije koristila** Vanjsko uređenje PRO. Svaki korak kaže **šta kucate**, **šta
program pita** i **šta odgovarate**. Detalji svake naredbe su u [Priručniku](PRIRUCNIK.md).

---

## Sadržaj

1. [Šta program radi (2 minute čitanja)](#1-šta-program-radi-2-minute-čitanja)
2. [Prije početka – provjera instalacije](#2-prije-početka--provjera-instalacije)
3. [Osnove koje trebate znati](#3-osnove-koje-trebate-znati)
4. [Vježba: probni projekat za 15 minuta](#4-vježba-probni-projekat-za-15-minuta)
5. [Pravi projekat iz vašeg DWG-a – korak po korak](#5-pravi-projekat-iz-vašeg-dwg-a--korak-po-korak)
6. [Prenos u SketchUp i 3D model](#6-prenos-u-sketchup-i-3d-model)
7. [Izmjene: šta kad se projekat promijeni](#7-izmjene-šta-kad-se-projekat-promijeni)
8. [Najčešće greške početnika](#8-najčešće-greške-početnika)
9. [Kontrolna lista za odštampati](#9-kontrolna-lista-za-odštampati)

---

## 1. Šta program radi (2 minute čitanja)

Vanjsko uređenje PRO ima dva dijela:

* **AutoCAD dio** – u svom crtežu situacije crtate vanjsko uređenje (staze, parking, zelenilo, rasvjetu,
  klupe, ograde …). Svaki nacrtani objekat je **element** koji zna šta je (npr. „klupa s naslonom", dužine 1,8 m).
  Program pravi oznake, tabele količina i provjerava projekat.
* **SketchUp dio** – iz AutoCAD-a izvezete jednu datoteku (**paket `.sdpkg`**), a SketchUp od nje automatski
  napravi **3D model**: realistične klupe, stabla, svjetiljke, popločanja s teksturom, bazene …

Tok rada je uvijek isti:

```
Vaš DWG  →  AutoCAD: pripremi i projektuj  →  SDPEXPORT (.sdpkg)  →  SketchUp: Uvoz paketa  →  3D model
```

Važno pravilo programa: **ništa se ne izmišlja.** Ako podatak nedostaje (npr. kota objekta), program to kaže i
čeka da ga vi unesete – ne pogađa.

---

## 2. Prije početka – provjera instalacije

### AutoCAD

1. Pokrenite AutoCAD (2020–2027).
2. Na traci (ribbon) gore treba postojati kartica **VANJSKO UREĐENJE PRO**.
3. U komandnoj liniji (dolje) treba pisati: *„Vanjsko uređenje PRO … je učitano"*.

Ako kartice nema: zatvorite AutoCAD i ponovo pokrenite `INSTALIRAJ.bat` iz instalacionog paketa.
Ako je kartica tu, ali je ne vidite, kucajte `RIBBON` i Enter.

### SketchUp

1. Pokrenite SketchUp (2022 ili noviji).
2. Meni **Extensions** (Proširenja) → treba postojati **Vanjsko uređenje PRO**.
3. Traka s alatima: meni **View → Toolbars…** → označite **Vanjsko uređenje PRO** → **Close**.

Ako proširenja nema: **Extensions → Extension Manager → Install Extension** → odaberite `pb_site_design_pro.rbz`.

---

## 3. Osnove koje trebate znati

### Kako se pokreće naredba

Tri načina, svi rade isto:

* klik na dugme na kartici **VANJSKO UREĐENJE PRO**;
* kucanje naziva u komandnu liniju, npr. `SDPNEW` pa **Enter**;
* dvoklik u paleti (otvara se naredbom `SDPBROWSER`).

### Kako se odgovara na pitanja

Program postavlja pitanja u **komandnoj liniji** (dolje na ekranu). Primjer:

```
Jedinica crteža [Milimetri/Centimetri/Metri] <Milimetri>:
```

* riječi u **uglastim zagradama** su izbori – kucajte prvo slovo (npr. `C` za Centimetri) i Enter, ili kliknite
  na riječ mišem;
* vrijednost u **< >** je zadana – samo **Enter** je prihvata;
* brojevi se kucaju s **zarezom ili tačkom** (`1,8` i `1.8` su isto);
* **Esc** prekida naredbu, a ništa nije promijenjeno.

### Jedan UNDO poništava cijelu naredbu

Ako napravite nešto pogrešno, kucajte `U` (ili Ctrl+Z) – poništava se cijela posljednja naredba.

### Gdje su poruke i greške

Sve poruke su u komandnoj liniji. Ako nešto ne uspije, piše *„Vanjsko uređenje PRO – greška: …"*. Detalji
greške su u dnevniku (`%LOCALAPPDATA%\SiteDesignPro\logs`). Pomoć u svakom trenutku: `SDPPOMOC`.

---

## 4. Vježba: probni projekat za 15 minuta

Prije vašeg pravog projekta prođite ovu vježbu. Program ima ugrađen primjer, pa ne morate ništa crtati.

| # | Kucajte | Program pita / radi | Vi odgovarate |
|---|---|---|---|
| 1 | `NEW` | novi crtež (šablon `acadiso.dwt`) | Open |
| 2 | `SDPSAMPLE` | *Primjer [Elementi/Izvorni/Oba]* | `O` (Oba) i Enter |
| 3 | `ZOOM` pa `E` | prikaz cijelog crteža | – |
| 4 | `SDPPROVJERADWG` | ispisuje izvještaj ulaznog crteža; *Sačuvati izvještaj …? [Da/Ne]* | `N` |
| 5 | `SDPDATUM` | prikazuje nultu kotu primjera (±0,00 = 520,15 m n.v.); *Izmjena [NultaKota/…/Izlaz]* | Enter (Izlaz) |
| 6 | `SDPKOTEIZOZNAKA` | prikazuje prijedloge kota; *Primijeniti … kota na označene objekte? [Da/Ne]* | `D` |
| 7 | `SDPTEREN` | *Granica terena [Odaberi/Bez/Zadrzi]* | `B` (Bez) |
|   |   | *Koristiti oznake kota kao visinske tačke terena? [Da/Ne]* | `N` |
| 8 | `SDPVALIDATE` | provjera; rezultati u paleti, kartica **Provjera** | – |
| 9 | `SDPSCHEDULE` | *Tabela [T1…/Sve]* → *Tačka umetanja tabele* | `S` (Sve), pa klik desno od crteža |
| 10 | `SDPEXPORT` | prozor za snimanje paketa | snimite npr. `vjezba.sdpkg` na Desktop |
| 11 | u SketchUpu: **Extensions → Vanjsko uređenje PRO → Uvoz paketa…** | prozor s pregledom | odaberite `vjezba.sdpkg` → **Uvezi** |

Nakon 11. koraka u SketchUpu imate 3D stambeni kompleks sa stazama, parkingom, zelenilom, rasvjetom i terenom.
Pogledajte scene (kartice gore: *VU 01 Situacija*, *VU 02 3D pregled*, *VU 05 Zelenilo i teren* …).

---

## 5. Pravi projekat iz vašeg DWG-a – korak po korak

### Korak 0 – Kopija crteža

Nikad ne radite na originalu. U Windowsu kopirajte DWG (npr. `Situacija.dwg` → `Situacija_VU.dwg`) i otvorite
kopiju. Program ionako pravi sigurnosnu kopiju prije velikih operacija, ali kopija je najsigurnija.

### Korak 1 – Saznajte u kojoj je jedinici crtež

Ovo je **najvažniji korak** – pogrešna jedinica znači da će klupa biti 1000 puta veća ili manja.

1. Kucajte `DIST` i izmjerite nešto čiju stvarnu veličinu znate (npr. parking mjesto 5 m ili širinu ceste).
2. Rezultat:
   * oko `5` → crtež je u **metrima**;
   * oko `500` → u **centimetrima**;
   * oko `5000` → u **milimetrima**.

Ne vjerujte postavci jedinica u AutoCAD-u (`UNITS`) – vjerujte izmjerenoj geometriji.

### Korak 2 – Novi projekat: `SDPNEW`

Kucajte `SDPNEW`. Program redom pita:

| Pitanje | Šta upisati |
|---|---|
| *Naziv projekta* | npr. `Stambeni objekat Ilidža` |
| *Broj projekta* | broj iz vašeg ureda ili Enter |
| *Investitor* | naziv investitora ili Enter |
| *Lokacija* | npr. `Ilidža, k.č. 1234/5` |
| *Jedinica crteža [Milimetri/Centimetri/Metri]* | ono što ste izmjerili u koraku 1 |
| *Ishodište projekta (u SketchUp-u postaje 0,0,0) <0,0,0>* | kliknite tačku **u sredini parcele** (npr. ugao objekta). Ne ostavljajte 0,0,0 ako je crtež u državnim koordinatama – SketchUp ne voli udaljene koordinate. |
| *Ugao pravog sjevera …* | ako je sjever gore na crtežu: `0` |
| *Mjerilo anotacija 1:* | npr. `250` ili `500` – veličina oznaka i teksta |

Program napravi standardne slojeve **VU-…** i ispiše: *„Visine nisu zadane: definišite nultu kotu objekta
(SDPDATUM), zatim mapirajte slojeve ulaznog crteža (SDPMAPIRANJE)."* – to je normalno, to su sljedeći koraci.

Kasnije sve ovo možete promijeniti naredbom `SDPSETTINGS`.

### Korak 3 – Recite programu šta je na kojem sloju: `SDPMAPIRANJE`

Vaš crtež ima svoje slojeve (npr. `KP_GRANICA`, `OBJEKAT`, `KOTE`). Program mora znati šta koji sloj predstavlja.

1. Kucajte `SDPMAPIRANJE`. Otvara se prozor s tabelom: **Sloj | Objekata | Uloga u projektu**.
2. Kliknite **Prijedlog prema nazivima** – program predloži uloge prema nazivima slojeva.
3. Pregledajte svaki red i u koloni **Uloga u projektu** odaberite ispravnu ulogu:

| Uloga | Šta je to | Obavezno? |
|---|---|---|
| **Parcelna granica** | zatvorena polilinija granice parcele | da |
| **Tlocrt objekta** | zatvorene polilinije tlocrta zgrada | da |
| **Oznake kota** | tekstovi s kotama (+0,25; KT 518,81) | da |
| Oznaka položaja objekta | blok koji samo označava objekat (nije tlocrt) | ne |
| Saobraćajnice | ivice i osovine cesta | ne |
| Linija partera | granica partera | ne |
| Tačke terena | geodetske tačke s visinom | za teren |
| Izohipse | izohipse s visinom | za teren |
| Prelomne linije | 3D linije loma terena | ne |
| Postojeća infrastruktura | vodovi, šahtovi, stubovi | ne |
| Kontrolne (geodetske) tačke | tačke s poznatim koordinatama | za georeferenciranje |

4. Slojeve koji nisu bitni (šrafure, tekstovi naslova …) ostavite **bez uloge**.
5. Kliknite **U redu**. Ako ovo radite za više projekata istog geodete, kliknite **Sačuvaj predložak…** i sljedeći put
   **Učitaj predložak…**.

Nazivi vaših slojeva se **ne mijenjaju**.

### Korak 4 – Provjera crteža: `SDPPROVJERADWG`

Kucajte `SDPPROVJERADWG`. Program pregleda mapirane slojeve i ispiše izvještaj: broj objekata, površinu parcele,
probleme. Svaki problem ima šifru (npr. `SDP-I…`) i preporuku šta uraditi.

Najčešći problemi i rješenja:

| Problem | Rješenje u AutoCAD-u |
|---|---|
| granica nije zatvorena | `PEDIT` → odaberite liniju → `Close` (ili `JOIN` za više komada) |
| skoro zatvorena (mali razmak) | `PEDIT` → `Close` |
| duple linije | `OVERKILL` |
| objekat izvan parcele | provjerite da li je pravi sloj mapiran |
| sumnja na jedinice | vratite se na korak 1 |

Ispravite sve **GREŠKE** i ponovo pokrenite `SDPPROVJERADWG` dok ih ne bude. Upozorenja pročitajte, ali ne
moraju se sva rješavati.

### Korak 5 – Nulta kota (±0,00): `SDPDATUM`

Da bi program znao visine, mora znati koliko je **±0,00** objekta u metrima nadmorske visine.

Kucajte `SDPDATUM` → *Izmjena [NultaKota/Parter/Saobracajnice/Datum/Zkoordinate/Izlaz]*:

1. `N` (NultaKota) → upišite nultu kotu iz projekta, npr. `520,15` → Enter.
2. Program pita *„Postaviti visinu ishodišta projekta na nultu kotu (u SketchUp-u je tada ±0,00 = 0)? [Da/Ne]"*
   → `D`.
3. `P` (Parter) → *Glavni parter [JednakNultoj/Razlicit]* → najčešće `J`; ako parter nije na ±0,00, `R` i upišite
   kotu partera.
4. `S` (Saobracajnice) → koliko su saobraćajnice niže od partera **u centimetrima** (npr. `15`), ili Enter ako
   ne znate.
5. `Z` (Zkoordinate) → *Z koordinate crteža su [Apsolutne/Relativne]* – ako tačke terena imaju visine kao
   520,30 → `A`; ako su oko 0 → `R`.
6. Enter (Izlaz).

Nultu kotu dobijate od arhitekte objekta ili iz glavnog projekta. Ako je ne znate, **ne izmišljajte** – program
radi i bez nje, ali neće računati apsolutne kote.

### Korak 6 – Preuzmite parcelu i objekte: `SDPPREUZMI`

Kucajte `SDPPREUZMI`. Program od mapiranih granica i tlocrta napravi **elemente** (parcela, objekti). Vaše
originalne linije ostaju netaknute.

Ako na sloju „Oznaka položaja objekta" postoje blokovi, za svaki pita *„Napraviti tlocrt za ovu oznaku?
[Da/Preskoci]"* – za `D` upišete stvarnu dužinu i širinu objekta u metrima.

### Korak 7 – Kote objekata: `SDPKOTEIZOZNAKA` i `SDPKOTA`

**Automatski iz tekstova:** kucajte `SDPKOTEIZOZNAKA`. Program pronađe tekstove kota i predloži koji pripada kojem
objektu (privremene linije to pokazuju). Pregledajte listu i na *„Primijeniti … kota na označene objekte?
[Da/Ne]"* odgovorite `D`. Nejasne oznake program ne dodjeljuje – njih uradite ručno.

**Ručno za jedan objekat:** kucajte `SDPKOTA` → kliknite objekat → *Način zadavanja kote
[Relativna/Apsolutna/Parter/Saobracajnica]*:

* `R` – upisujete u odnosu na ±0,00 (npr. `-0,15`);
* `A` – upisujete m n.v. (npr. `519,95`);
* `P` – objekat dobija kotu partera;
* `S` – objekat dobija kotu saobraćajnica.

Program prikaže staru i novu kotu i pita *„Primijeniti kotu na ovaj objekat? [Da/Ne]"*.

### Korak 8 – Teren (ako imate geodetski snimak): `SDPTEREN`

Ako ste mapirali tačke terena ili izohipse:

1. Kucajte `SDPTEREN`.
2. *Granica terena [Odaberi/Bez/Zadrzi]* → `O` i kliknite parcelu (ili `B` za cijeli snimak).
3. *Koristiti oznake kota kao visinske tačke terena? [Da/Ne]* → `D` ako su kote na terenu, inače `N`.

Teren se nacrta na sloju `VU-TEREN`. Ako podataka nema dovoljno, program to kaže i **ništa ne izmišlja**.
Profil: `SDPPROFIL`; provjera ravnih dijelova i lokalnih udubljenja: `SDPANALIZATERENA`.

Bez geodetskog snimka preskočite ovaj korak – SketchUp model će biti na ravnoj podlozi.

### Korak 9 – Projektovanje vanjskog uređenja

Sad crtate ono što projektujete. Otvorite paletu: `SDPBROWSER`.

**Paleta, kartica Biblioteka** – svi tipovi elemenata po kategorijama (Površine, Saobraćaj, Zelenilo, Rasvjeta,
Mobilijar …). Pretraga gore. **Dvoklik** na tip pokreće crtanje. Desni klik = omiljeni.

**Tačkasti elementi** (stablo, klupa, svjetiljka, kanta, stubić …) – `SDPPLACE`:

1. *Tip elementa (id tipa ili dio naziva, ? za popis)* – kucajte dio naziva, npr. `klupa` (ili dvoklik u paleti).
2. *Tačka umetanja [Enter za kraj]* – klikajte mjesta; možete postaviti više komada.
3. *Rotacija* – kliknite smjer ili upišite ugao.

**Linijski elementi** (staza, ivičnjak, ograda, zid, živica, kanal, cijev …) i **površine** (popločanje, travnjak,
gredica, parking površina, bazen, pergola …) – `SDPDRAW`:

1. *Tip elementa* – npr. `pjesacka` ili `travnjak`.
2. Za površinu: *Granica [Polilinija/Pravougaonik/Unutar/Odaberi]*; za trasu: *Trasa [Polilinija/Odaberi]*:
   * **Polilinija** – klikajte tačke; `Z` (Zatvori) zatvara, `P` (Ponisti) briše zadnju tačku, **Enter** završava;
   * **Pravougaonik** – prvi ugao, pa suprotni ugao;
   * **Unutar** – kliknite **unutar** već nacrtane zatvorene površine (kao šrafura) – najbrži način za popločanja
     između postojećih linija; Enter za kraj;
   * **Odaberi** – odaberite postojeće linije (npr. ivice staze koje već imate).

**Postojeće linije pretvorite u elemente:** `SDPCONVERT` (npr. sve linije ograde odjednom).

**Brzi generatori:**

* `SDPPATHARRAY` – niz elemenata duž trase (npr. svjetiljke svakih 20 m duž staze);
* `SDPPARKING` – niz parking mjesta;
* `SDPGRID` – raster sadnje u površini (npr. stabla u voćnjaku).

**Izmjena elementa:** kliknite element → paleta, kartica **Svojstva** → promijenite vrijednost (dužina, materijal,
visina …) → **Primijeni izmjene**. Ili `SDPEDIT`.

**Odabir svih istih:** `SDPSELECT`.

### Korak 10 – Oznake, numeracija, legenda: za nacrte

| Kucajte | Šta radi |
|---|---|
| `SDPNUMBER` | numeriše elemente (npr. S-01, S-02 za stabla) |
| `SDPLABEL` | postavlja oznake uz elemente |
| `SDPAREALABEL`, `SDPWIDTHLABEL` | površine i širine |
| `SDPSPOTLEVEL`, `SDPSLOPE` | visinske kote i strelice nagiba |
| `SDPLEGEND` | legenda |
| `SDPTITLEBLOCK` | okvir i sastavnica |
| `SDPNORTH`, `SDPSCALEBAR` | sjever i grafičko mjerilo |

### Korak 11 – Provjera projekta: `SDPVALIDATE`

Kucajte `SDPVALIDATE`. Rezultati su u paleti, kartica **Provjera**: **greške** (crveno), **upozorenja** (narandžasto)
i stavke **za stručni pregled** (ljubičasto – vrijednosti koje treba potvrditi prema propisima). Dvoklik na red
zumira na element.

### Korak 12 – Tabele količina: `SDPSCHEDULE`

`SDPSCHEDULE` → `S` (Sve) → kliknite gdje da se postave tabele (površine, dužine, komadi po tipu). Za Excel:
`SDPSCHEDULECSV`.

### Korak 13 – Izvoz za SketchUp: `SDPEXPORT`

1. Snimite crtež (`QSAVE`).
2. Kucajte `SDPEXPORT`.
3. Ako postoje greške, program pita *„Ipak izvesti? [Da/Ne]"* – bolje `N`, ispravite greške (korak 11), pa ponovo.
4. Otvara se prozor – snimite paket, najbolje **pored DWG-a** s istim imenom (npr. `Situacija_VU.sdpkg`).

Na kraju piše *„Izvezeno elemenata: …"*.

---

## 6. Prenos u SketchUp i 3D model

### Uvoz paketa

1. Otvorite SketchUp → novi model (šablon **Architectural – Meters** ili bilo koji metrički).
2. **Extensions → Vanjsko uređenje PRO → Uvoz paketa…** (ili prvo dugme na traci).
3. Odaberite `.sdpkg` datoteku.
4. Otvara se **pregled prije uvoza**:
   * kartice pokazuju koliko će se elemenata **napraviti**, **ažurirati**, **obrisati**;
   * **Nivo detalja**: *Šematski prikaz* (brzo, jednostavno), *Standardni kvalitet* (preporučeno), *Visoki
     kvalitet* (za prezentacije – sporije, više poligona);
   * ☑ *Izradi scene za pregled* – preporučeno;
   * ☑ *Sigurnosna kopija prije uvoza* – preporučeno.
5. Kliknite **Uvezi**. Na kraju se pokaže **izvještaj uvoza**.

Rezultat: realistični 3D modeli svih elemenata, organizovani u grupe i oznake (tagove): `VU Zelenilo`,
`VU Rasvjeta`, `VU Parter` … Teren je ispod.

### Pregled modela

* Scene gore (kartice): *VU 01 Situacija*, *VU 02 3D pregled*, *VU 05 Zelenilo i teren*, *VU 06 Rasvjeta*,
  *VU 08 Podzemne instalacije*, *VU 10 Prezentacija* …
* **Prikaz podzemnog** (dugme) – sakrije površine i pokaže instalacije ispod terena.
* Sjene: **View → Shadows** – sjever je postavljen iz projekta.

### Dodavanje modela direktno u SketchUpu: Biblioteka 3D modela

Za dodatne objekte (npr. još jednu klupu za vizualizaciju):

1. **Extensions → Vanjsko uređenje PRO → Biblioteka 3D modela…**
2. Lijevo kategorija (14 kategorija), sredina pregledi, desno detalji i parametri.
3. Odaberite model, po želji promijenite parametre → **Umetni u model** → kliknite u model.
   * strelice **lijevo/desno** okreću model za 15° prije klika;
   * ili upišite ugao i Enter;
   * **Esc** završava.
4. Za ogradu, zid, živicu: prvo nacrtajte liniju **Line** alatom i odaberite je, pa **Umetni u model** – model
   se napravi duž linije. Za bazen, popločanje, gredicu: nacrtajte pravougaonik i odaberite **lice**.
5. **Zamijeni odabrano** – zamijeni odabrani model drugim (npr. klupu drugom klupom) na istom mjestu.

Napomena: modeli dodani u SketchUpu ne vraćaju se u AutoCAD – za projekat ih dodajte u AutoCAD-u.

### Izmjena elementa u SketchUpu

Odaberite element → desni klik → **Vanjsko uređenje PRO → Svojstva elementa** → promijenite parametar →
**Primijeni**. Element se ponovo izgradi.

---

## 7. Izmjene: šta kad se projekat promijeni

### Izmjena u AutoCAD-u (najčešće)

1. Promijenite projekat u AutoCAD-u.
2. `SDPEXPORT` → snimite preko istog `.sdpkg`.
3. U SketchUpu: **Ažuriranje iz paketa**. Pregled pokazuje šta je novo, izmijenjeno i obrisano.
4. **Uvezi**. Program mijenja samo izmijenjene elemente; vaše kamere, materijali i dodaci u SketchUpu ostaju.

Ako ste neki element mijenjali u SketchUpu, a i u AutoCAD-u, program to prijavi kao **konflikt** i po zadanim
postavkama zadrži SketchUp verziju.

### Izmjena u SketchUpu, prenos nazad u AutoCAD

1. U SketchUpu mijenjajte parametre (Svojstva elementa) ili pomjerite/zarotirajte stabla, klupe i slično.
2. **Izvoz izmjena za AutoCAD…** → snimite `sdp-izmjene.json`.
3. U AutoCAD-u: `SDPAPPLYCHANGES` → odaberite tu datoteku → pregledajte → primijenite.
4. Zatim ponovo `SDPEXPORT`.

---

## 8. Najčešće greške početnika

| Simptom | Uzrok | Rješenje |
|---|---|---|
| Klupa je ogromna ili sitna | pogrešna jedinica crteža | `SDPSETTINGS` → `Jedinice` (korak 1) |
| SketchUp model je kilometrima od ishodišta | ishodište ostavljeno na 0,0,0 u državnim koordinatama | `SDPSETTINGS` → `Ishodiste` → tačka na parceli, pa ponovo `SDPEXPORT` i uvoz |
| `SDPPROVJERADWG` kaže da nema parcele | sloj granice nije mapiran ili linija nije zatvorena | `SDPMAPIRANJE`; `PEDIT` → `Close` |
| Kote se ne računaju | nije zadana nulta kota | `SDPDATUM` → `NultaKota` |
| Teren se ne pravi | nema tačaka/izohipsi s visinom | mapirajte slojeve snimka; bez snimka teren ne postoji |
| *Unknown command* | dodatak nije učitan ili stara verzija | ponovo `INSTALIRAJ.bat`, restart AutoCAD-a |
| U SketchUpu nema trake | traka isključena | **View → Toolbars…** → Vanjsko uređenje PRO |
| Izmjena u AutoCAD-u se ne vidi u SketchUpu | paket nije ponovo izvezen | `SDPEXPORT`, pa **Ažuriranje iz paketa** |
| Nešto je pošlo krivo | – | `U` (UNDO) poništava cijelu naredbu; sigurnosne kopije: `SDPOPORAVAK` |

---

## 9. Kontrolna lista za odštampati

```
PRIPREMA
[ ] Kopija DWG-a otvorena
[ ] Jedinica izmjerena (DIST): mm / cm / m  = ________
[ ] SDPNEW  – naziv, jedinica, ishodište na parceli, sjever, mjerilo
[ ] SDPMAPIRANJE – parcela, tlocrti objekata, oznake kota (+ teren ako postoji)
[ ] SDPPROVJERADWG – nema GREŠAKA
[ ] SDPDATUM – nulta kota ±0,00 = ________ m n.v., parter, saobraćajnice
[ ] SDPPREUZMI – parcela i objekti preuzeti
[ ] SDPKOTEIZOZNAKA / SDPKOTA – kote objekata
[ ] SDPTEREN – samo ako postoji geodetski snimak

PROJEKTOVANJE
[ ] Površine (SDPDRAW): popločanja, travnjaci, parking, saobraćajnice
[ ] Linije (SDPDRAW): ivičnjaci, staze, ograde, zidovi, živice, kanali
[ ] Tačke (SDPPLACE): stabla, grmlje, rasvjeta, mobilijar, oprema
[ ] Oznake i numeracija (SDPNUMBER, SDPLABEL), legenda, okvir

KONTROLA I IZVOZ
[ ] SDPVALIDATE – greške riješene, upozorenja pregledana
[ ] SDPSCHEDULE – tabele količina
[ ] QSAVE, pa SDPEXPORT → ________.sdpkg

SKETCHUP
[ ] Uvoz paketa → Standardni kvalitet → Uvezi
[ ] Scene i sjene pregledane
[ ] Za izmjene: SDPEXPORT → Ažuriranje iz paketa
```

---

Sljedeće čitanje: [Priručnik](PRIRUCNIK.md) – svako dugme i svaka naredba detaljno; šifre poruka i šta s njima.
