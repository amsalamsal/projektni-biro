# Prvi projekat – uputstvo za početnike

Ovo uputstvo vodi korak po korak od praznog računara do gotovog 3D modela vanjskog uređenja u SketchUpu.
Pisano je za osobu koja **nikad nije koristila** Vanjsko uređenje PRO. Svaki korak kaže **gdje kliknete na traci**
(ribbonu), **šta program pita** i **šta odgovarate**. Naziv naredbe je naveden u zagradi – ko voli kucati, može
ga upisati u komandnu liniju umjesto klika. Detalji svake naredbe su u [Priručniku](PRIRUCNIK.md).

---

## Sadržaj

1. [Šta program radi (2 minute čitanja)](#1-šta-program-radi-2-minute-čitanja)
2. [Prije početka – provjera instalacije](#2-prije-početka--provjera-instalacije)
3. [Osnove koje trebate znati](#3-osnove-koje-trebate-znati)
   * [Mapa trake – gdje je šta](#mapa-trake--gdje-je-šta)
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

Najlakše je **klikom na traci (ribbonu)**:

1. Gore u AutoCAD-u kliknite karticu **VANJSKO UREĐENJE PRO**.
2. Kartica je podijeljena na **ploče** (Projekt, Uvoz DWG-a, Teren i kote …). Naziv ploče piše **ispod** dugmadi.
3. Kliknite dugme. Veliko dugme je glavna radnja ploče, mala dugmad pored njega su dodatne radnje.

Isto se može uraditi i:

* kucanjem naziva naredbe u komandnu liniju (npr. `SDPNEW` pa **Enter**) – naziv je u ovom uputstvu u zagradi;
* iz **palete** (dugme **Biblioteka** na ploči *Projekt*) – dvoklik na element.

Ako ne vidite cijelu traku (mali ekran), ploče se skupe u ikone – kliknite ploču i otvori se.

### Mapa trake – gdje je šta

Ploče se redaju s lijeva na desno ovim redom:

| Ploča | Dugmad (veliko dugme podebljano) | Za šta služi |
|---|---|---|
| **Projekt** | **Novi projekat**, Postavke, Slojevi, Biblioteka, Primjer projekta | početak projekta, postavke, paleta |
| **Uvoz DWG-a** | **Mapiranje slojeva**, Provjera crteža, Preuzmi objekte, Pretvori krive | priprema vašeg postojećeg crteža |
| **Georeferenciranje** | **Kontrolne tačke**, Izvoz KML, Koordinate, Sjever | koordinate i sjever |
| **Teren i kote** | **Nulta kota**, Dodijeli kotu, Kote iz oznaka, Visinska kota, Model terena, Profil, Analiza terena, Strelica nagiba | visine i teren |
| **Parter i popločanje** | **Pregled**, padajuće liste: Popločanje, Rastresiti materijali, Zelene površine, Podovi i terase, Ivice i prelazi, Visinske razlike, Ograde, Zidovi, Kapije …; Površina, Oznake površina | popločanja, ivičnjaci, stepenice, ograde, zidovi |
| **Zelenilo** | **Pregled**, liste: Drveće, Grmlje i žive ograde, Gredice, Travnjaci i livade, Vertikalno ozelenjavanje, Žardinjere, Zaštita stabala, Navodnjavanje; Drvored, Raster sadnje | sve biljke |
| **Saobraćaj i parking** | **Pregled**, liste: Saobraćajnice, Parking, Oznake, Smirivanje saobraćaja, Barijere, Staze i trake …; Niz parking mjesta, Oznake širina, Stalci duž staze | ceste, parking, biciklističke staze |
| **Infrastruktura** | **Pregled**, liste: Otpad, Rezervoari, Okna, Bazeni, Površinska odvodnja, Kišnica, Solarna energija, Punjenje električnih vozila, Grijanje, Zasjena … | instalacije, voda, energija, klima |
| **Rasvjeta** | **Pregled**, liste: Stubovi, Niska rasvjeta, Zidna montaža, Dekorativno, Upravljanje; Svjetiljke duž puta, Pokrivenost | svjetiljke |
| **Urbani mobilijar** | **Pregled**, liste: Sjedenje, Stolovi, Korpe i kante, Dječija igrališta, Sport, Pergole, Nadstrešnice, Informacije …; Raspored duž staze | klupe, igrališta, pergole, table |
| **3D modeli** | **Uredi element**, Zone sigurnosti, Odabir, Korisnički katalozi | izmjena nacrtanih elemenata |
| **SketchUp razmjena** | **Izvoz paketa**, Primijeni izmjene, Provjera prije izvoza | prenos u SketchUp i nazad |
| **Količinski obračun** | **Tabele**, Izvoz CSV, Količine | količine |
| **Nacrti i oznake** | **Oznaka**, Numeracija, Legenda, Sastavnica, Razmjernik, Presjek, Detalj, Rasterske ose, Oblak revizije | crtanje nacrta |
| **Provjera projekta** | **Provjeri projekat**, Provjera crteža, Revizija | kontrola |
| **Postavke i pomoć** | **Pomoć**, Sigurnosna kopija, Postavke kopija, Oporavak, Učitaj katalog, O programu | pomoć i sigurnost |

#### Padajuće liste na pločama s elementima

Na pločama *Parter i popločanje*, *Zelenilo*, *Saobraćaj i parking*, *Infrastruktura*, *Rasvjeta* i *Urbani
mobilijar* svaka grupa (npr. **Drveće**) je **padajuća lista**:

1. kliknite **malu strelicu ▾** desno od naziva grupe – otvori se spisak (npr. Listopadno drvo, Zimzeleno drvo,
   Voćka …);
2. kliknite element u spisku – odmah počinje postavljanje ili crtanje tog elementa;
3. dugme grupe poslije pamti zadnji izabrani element – sljedeći put je dovoljan klik na sam naziv.

Ako ploča ima više grupa nego što stane, kliknite **naziv ploče** (traka ispod dugmadi, sa strelicom ▼) – otvori se
ostatak ploče. Kliknite pribadaču 📌 da ostane otvoren.

**Pregled** (veliko dugme na tim pločama) otvara paletu sa svim elementima te kategorije i sličicama.

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

| # | Kliknite (kartica VANJSKO UREĐENJE PRO) | Program pita / radi | Vi odgovarate |
|---|---|---|---|
| 1 | AutoCAD: **New** (novi crtež) | šablon `acadiso.dwt` | Open |
| 2 | ploča **Projekt** → **Primjer projekta** (`SDPSAMPLE`) | *Primjer [Elementi/Izvorni/Oba]* | kliknite **Oba** |
| 3 | kotačić miša dvoklik (ili `ZOOM` → `E`) | prikaz cijelog crteža | – |
| 4 | ploča **Uvoz DWG-a** → **Provjera crteža** (`SDPPROVJERADWG`) | izvještaj ulaznog crteža; *Sačuvati izvještaj …? [Da/Ne]* | **Ne** |
| 5 | ploča **Teren i kote** → **Nulta kota** (`SDPDATUM`) | prikaže ±0,00 = 520,15 m n.v.; *Izmjena [NultaKota/…/Izlaz]* | Enter (Izlaz) |
| 6 | ploča **Teren i kote** → **Kote iz oznaka** (`SDPKOTEIZOZNAKA`) | prijedlozi kota; *Primijeniti … kota …? [Da/Ne]* | **Da** |
| 7 | ploča **Teren i kote** → **Model terena** (`SDPTEREN`) | *Granica terena [Odaberi/Bez/Zadrzi]* | **Bez** |
|   |   | *Koristiti oznake kota kao visinske tačke terena? [Da/Ne]* | **Ne** |
| 8 | ploča **Provjera projekta** → **Provjeri projekat** (`SDPVALIDATE`) | rezultati u paleti, kartica **Provjera** | – |
| 9 | ploča **Količinski obračun** → **Tabele** (`SDPSCHEDULE`) | *Tabela [T1…/Sve]* → *Tačka umetanja tabele* | **Sve**, pa klik desno od crteža |
| 10 | ploča **SketchUp razmjena** → **Izvoz paketa** (`SDPEXPORT`) | prozor za snimanje paketa | snimite `vjezba.sdpkg` na Desktop |
| 11 | SketchUp: meni **Extensions → Vanjsko uređenje PRO → Uvoz paketa…** | prozor s pregledom | odaberite `vjezba.sdpkg` → **Uvezi** |

Izbore u komandnoj liniji (npr. **Oba**, **Da**, **Bez**) možete kliknuti mišem direktno u komandnoj liniji ili
upisati prvo slovo i Enter.

Nakon 11. koraka u SketchUpu imate 3D stambeni kompleks sa stazama, parkingom, zelenilom, rasvjetom i terenom.
Pogledajte scene (kartice gore: *VU 01 Situacija*, *VU 02 3D pregled*, *VU 05 Zelenilo i teren* …).

---

## 5. Pravi projekat iz vašeg DWG-a – korak po korak

### Korak 0 – Kopija crteža

Nikad ne radite na originalu. U Windowsu kopirajte DWG (npr. `Situacija.dwg` → `Situacija_VU.dwg`) i otvorite
kopiju. Program ionako pravi sigurnosnu kopiju prije velikih operacija, ali kopija je najsigurnija.

### Korak 1 – Saznajte u kojoj je jedinici crtež

Ovo je **najvažniji korak** – pogrešna jedinica znači da će klupa biti 1000 puta veća ili manja.

1. Kartica **Home** → ploča **Utilities** → **Measure → Distance** (ili kucajte `DIST`) i izmjerite nešto čiju stvarnu veličinu znate (npr. parking mjesto 5 m ili širinu ceste).
2. Rezultat:
   * oko `5` → crtež je u **metrima**;
   * oko `500` → u **centimetrima**;
   * oko `5000` → u **milimetrima**.

Ne vjerujte postavci jedinica u AutoCAD-u (`UNITS`) – vjerujte izmjerenoj geometriji.

### Korak 2 – Novi projekat

Kliknite: ploča **Projekt** → veliko dugme **Novi projekat** (`SDPNEW`). Program redom pita u komandnoj liniji:

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

Kasnije sve ovo mijenjate klikom: ploča **Projekt** → **Postavke** (`SDPSETTINGS`).

### Korak 3 – Recite programu šta je na kojem sloju

Vaš crtež ima svoje slojeve (npr. `KP_GRANICA`, `OBJEKAT`, `KOTE`). Program mora znati šta koji sloj predstavlja.

1. Kliknite: ploča **Uvoz DWG-a** → veliko dugme **Mapiranje slojeva** (`SDPMAPIRANJE`). Otvara se prozor s tabelom: **Sloj | Objekata | Uloga u projektu**.
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

### Korak 4 – Provjera crteža

Kliknite: ploča **Uvoz DWG-a** → **Provjera crteža** (`SDPPROVJERADWG`). Program pregleda mapirane slojeve i ispiše izvještaj: broj objekata, površinu parcele,
probleme. Svaki problem ima šifru (npr. `SDP-I…`) i preporuku šta uraditi.

Najčešći problemi i rješenja:

| Problem | Rješenje u AutoCAD-u |
|---|---|
| granica nije zatvorena | `PEDIT` → odaberite liniju → `Close` (ili `JOIN` za više komada) |
| skoro zatvorena (mali razmak) | `PEDIT` → `Close` |
| duple linije | `OVERKILL` |
| objekat izvan parcele | provjerite da li je pravi sloj mapiran |
| sumnja na jedinice | vratite se na korak 1 |

Ispravite sve **GREŠKE** i ponovo kliknite **Provjera crteža** dok ih ne bude. Upozorenja pročitajte, ali ne
moraju se sva rješavati.

### Korak 5 – Nulta kota (±0,00)

Da bi program znao visine, mora znati koliko je **±0,00** objekta u metrima nadmorske visine.

Kliknite: ploča **Teren i kote** → veliko dugme **Nulta kota** (`SDPDATUM`). U komandnoj liniji piše
*Izmjena [NultaKota/Parter/Saobracajnice/Datum/Zkoordinate/Izlaz]* – kliknite izbor mišem u komandnoj liniji:

1. **NultaKota** → upišite nultu kotu iz projekta, npr. `520,15` → Enter.
2. Program pita *„Postaviti visinu ishodišta projekta na nultu kotu (u SketchUp-u je tada ±0,00 = 0)? [Da/Ne]"*
   → **Da**.
3. **Parter** → *Glavni parter [JednakNultoj/Razlicit]* → najčešće **JednakNultoj**; ako parter nije na ±0,00, **Razlicit** i
   upišite kotu partera.
4. **Saobracajnice** → koliko su saobraćajnice niže od partera **u centimetrima** (npr. `15`), ili Enter ako
   ne znate.
5. **Zkoordinate** → *Z koordinate crteža su [Apsolutne/Relativne]* – ako tačke terena imaju visine kao
   520,30 → **Apsolutne**; ako su oko 0 → **Relativne**.
6. **Izlaz** (ili Enter).

Nultu kotu dobijate od arhitekte objekta ili iz glavnog projekta. Ako je ne znate, **ne izmišljajte** – program
radi i bez nje, ali neće računati apsolutne kote.

### Korak 6 – Preuzmite parcelu i objekte

Kliknite: ploča **Uvoz DWG-a** → **Preuzmi objekte** (`SDPPREUZMI`). Program od mapiranih granica i tlocrta napravi **elemente** (parcela, objekti). Vaše
originalne linije ostaju netaknute.

Ako na sloju „Oznaka položaja objekta" postoje blokovi, za svaki pita *„Napraviti tlocrt za ovu oznaku?
[Da/Preskoci]"* – za **Da** upišete stvarnu dužinu i širinu objekta u metrima.

### Korak 7 – Kote objekata

**Automatski iz tekstova:** ploča **Teren i kote** → **Kote iz oznaka** (`SDPKOTEIZOZNAKA`). Program pronađe tekstove kota i predloži koji pripada kojem
objektu (privremene linije to pokazuju). Pregledajte listu i na *„Primijeniti … kota na označene objekte?
[Da/Ne]"* kliknite **Da**. Nejasne oznake program ne dodjeljuje – njih uradite ručno.

**Ručno za jedan objekat:** ploča **Teren i kote** → **Dodijeli kotu** (`SDPKOTA`) → kliknite objekat u crtežu → *Način zadavanja kote
[Relativna/Apsolutna/Parter/Saobracajnica]*:

* **Relativna** – upisujete u odnosu na ±0,00 (npr. `-0,15`);
* **Apsolutna** – upisujete m n.v. (npr. `519,95`);
* **Parter** – objekat dobija kotu partera;
* **Saobracajnica** – objekat dobija kotu saobraćajnica.

Program prikaže staru i novu kotu i pita *„Primijeniti kotu na ovaj objekat? [Da/Ne]"*.

### Korak 8 – Teren (ako imate geodetski snimak)

Ako ste mapirali tačke terena ili izohipse:

1. Kliknite: ploča **Teren i kote** → **Model terena** (`SDPTEREN`).
2. *Granica terena [Odaberi/Bez/Zadrzi]* → **Odaberi** i kliknite parcelu (ili **Bez** za cijeli snimak).
3. *Koristiti oznake kota kao visinske tačke terena? [Da/Ne]* → **Da** ako su kote na terenu, inače **Ne**.

Teren se nacrta na sloju `VU-TEREN`. Ako podataka nema dovoljno, program to kaže i **ništa ne izmišlja**.
Na istoj ploči: **Profil** (`SDPPROFIL`) i **Analiza terena** (`SDPANALIZATERENA`) – ravni dijelovi i lokalna
udubljenja.

Bez geodetskog snimka preskočite ovaj korak – SketchUp model će biti na ravnoj podlozi.

### Korak 9 – Projektovanje vanjskog uređenja

Sad crtate ono što projektujete. Sve je na pločama s elementima: **Parter i popločanje**, **Zelenilo**,
**Saobraćaj i parking**, **Infrastruktura**, **Rasvjeta**, **Urbani mobilijar**.

#### Primjer 1 – stablo (tačkasti element)

1. Ploča **Zelenilo** → strelica ▾ pored **Drveće** → kliknite **Listopadno drvo**.
2. Komandna linija: *Tačka umetanja [Enter za kraj]* – kliknite u crtežu gdje stablo stoji. Možete kliknuti više
   puta – svaki klik je jedno stablo.
3. *Rotacija* – kliknite smjer ili Enter.
4. **Enter** kad završite.

Isto za sve „tačkaste" stvari: klupe (*Urbani mobilijar → Sjedenje*), svjetiljke (*Rasvjeta → Stubovi*), kante
(*Urbani mobilijar → Korpe i kante*), stubiće (*Saobraćaj i parking → Barijere*), okna (*Infrastruktura → Okna*) …

#### Primjer 2 – popločanje (površina)

1. Ploča **Parter i popločanje** → strelica ▾ pored **Popločanje** → **Betonsko popločanje**.
2. Komandna linija: *Granica [Polilinija/Pravougaonik/Unutar/Odaberi]* – kliknite jedan izbor:
   * **Unutar** – najlakše: kliknite **unutar** već nacrtane zatvorene površine (kao šrafura); Enter za kraj;
   * **Polilinija** – klikajte tačke granice; u komandnoj liniji **Zatvori** zatvara, **Ponisti** briše zadnju
     tačku, Enter završava;
   * **Pravougaonik** – prvi ugao, pa suprotni ugao;
   * **Odaberi** – kliknite postojeće linije granice.

Isto za travnjake (*Zelenilo → Travnjaci i livade*), gredice (*Zelenilo → Gredice*), parking površinu, bazen
(*Infrastruktura → Bazeni*), pergolu (*Urbani mobilijar → Pergole*) …

#### Primjer 3 – ograda, ivičnjak, staza (linija)

1. Ploča **Parter i popločanje** → ▾ **Ograde** → **Ograda od zavarene mreže** (ili ▾ **Ivice i prelazi** →
   **Ivičnjak**; staze su na ploči **Saobraćaj i parking** → ▾ **Staze i trake**).
2. *Trasa [Polilinija/Odaberi]* → **Polilinija** i klikajte tačke, ili **Odaberi** i kliknite postojeću liniju.

#### Brzi generatori (mala dugmad na pločama)

| Dugme | Ploča | Šta radi |
|---|---|---|
| **Drvored** | Zelenilo | stabla duž linije na zadanom razmaku |
| **Raster sadnje** | Zelenilo | stabla/grmlje u rasteru unutar površine |
| **Niz parking mjesta** | Saobraćaj i parking | niz parking mjesta uz rub |
| **Svjetiljke duž puta** | Rasvjeta | svjetiljke duž staze ili ceste |
| **Raspored duž staze** | Urbani mobilijar | klupe ili kante duž staze |
| **Pretvori krive** | Uvoz DWG-a | postojeće linije pretvara u elemente (npr. sve linije ograde odjednom) |

#### Paleta (lakše traženje)

Ploča **Projekt** → **Biblioteka** (`SDPBROWSER`) otvara paletu. Kartica **Biblioteka**: pretraga gore,
kategorije, sličice; **dvoklik** na element pokreće postavljanje. Desni klik = omiljeni.

#### Izmjena nacrtanog elementa

* Kliknite element u crtežu → u paleti, kartica **Svojstva**, promijenite vrijednost (dužina, visina, materijal …)
  → **Primijeni izmjene**; ili
* ploča **3D modeli** → **Uredi element** (`SDPEDIT`).

Svi elementi istog tipa: ploča **3D modeli** → **Odabir** (`SDPSELECT`).

### Korak 10 – Oznake, numeracija, legenda: za nacrte

Sve na ploči **Nacrti i oznake** (osim gdje piše drugačije):

| Dugme | Šta radi |
|---|---|
| **Numeracija** | numeriše elemente (npr. S-01, S-02 za stabla) |
| **Oznaka** | oznake uz elemente (broj i podaci) |
| **Legenda** | legenda tipova elemenata |
| **Sastavnica** | okvir lista i sastavnica |
| **Razmjernik**, **Presjek**, **Detalj**, **Rasterske ose**, **Oblak revizije** | elementi nacrta |
| ploča *Parter i popločanje* → **Oznake površina** | površine u m² |
| ploča *Saobraćaj i parking* → **Oznake širina** | širine staza i cesta |
| ploča *Teren i kote* → **Visinska kota**, **Strelica nagiba** | kote i nagibi |
| ploča *Georeferenciranje* → **Sjever**, **Koordinate** | sjever i koordinate |

### Korak 11 – Provjera projekta

Kliknite: ploča **Provjera projekta** → **Provjeri projekat** (`SDPVALIDATE`). Rezultati su u paleti, kartica **Provjera**: **greške** (crveno), **upozorenja** (narandžasto)
i stavke **za stručni pregled** (ljubičasto – vrijednosti koje treba potvrditi prema propisima). Dvoklik na red
zumira na element.

### Korak 12 – Tabele količina

Ploča **Količinski obračun** → **Tabele** (`SDPSCHEDULE`) → u komandnoj liniji **Sve** → kliknite gdje da se
postave tabele (površine, dužine, komadi po tipu). Za Excel: ista ploča → **Izvoz CSV**.

### Korak 13 – Izvoz za SketchUp

1. Snimite crtež (Ctrl+S).
2. Kliknite: ploča **SketchUp razmjena** → **Izvoz paketa** (`SDPEXPORT`).
3. Ako postoje greške, program pita *„Ipak izvesti? [Da/Ne]"* – bolje **Ne**, ispravite greške (korak 11), pa ponovo.
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
2. Ploča **SketchUp razmjena** → **Izvoz paketa** → snimite preko istog `.sdpkg`.
3. U SketchUpu: **Extensions → Vanjsko uređenje PRO → Ažuriranje iz paketa** (ili drugo dugme na traci alata). Pregled pokazuje šta je novo, izmijenjeno i obrisano.
4. **Uvezi**. Program mijenja samo izmijenjene elemente; vaše kamere, materijali i dodaci u SketchUpu ostaju.

Ako ste neki element mijenjali u SketchUpu, a i u AutoCAD-u, program to prijavi kao **konflikt** i po zadanim
postavkama zadrži SketchUp verziju.

### Izmjena u SketchUpu, prenos nazad u AutoCAD

1. U SketchUpu mijenjajte parametre (Svojstva elementa) ili pomjerite/zarotirajte stabla, klupe i slično.
2. **Izvoz izmjena za AutoCAD…** → snimite `sdp-izmjene.json`.
3. U AutoCAD-u: ploča **SketchUp razmjena** → **Primijeni izmjene** (`SDPAPPLYCHANGES`) → odaberite tu datoteku →
   pregledajte → primijenite.
4. Zatim ponovo **Izvoz paketa**.

---

## 8. Najčešće greške početnika

| Simptom | Uzrok | Rješenje |
|---|---|---|
| Klupa je ogromna ili sitna | pogrešna jedinica crteža | ploča **Projekt** → **Postavke** → **Jedinice** (korak 1) |
| SketchUp model je kilometrima od ishodišta | ishodište ostavljeno na 0,0,0 u državnim koordinatama | **Projekt → Postavke → Ishodiste** → tačka na parceli, pa ponovo **Izvoz paketa** i uvoz |
| `SDPPROVJERADWG` kaže da nema parcele | sloj granice nije mapiran ili linija nije zatvorena | **Uvoz DWG-a → Mapiranje slojeva**; `PEDIT` → `Close` |
| Kote se ne računaju | nije zadana nulta kota | **Teren i kote → Nulta kota → NultaKota** |
| Teren se ne pravi | nema tačaka/izohipsi s visinom | mapirajte slojeve snimka; bez snimka teren ne postoji |
| nema kartice VANJSKO UREĐENJE PRO ili *Unknown command* | dodatak nije učitan ili stara verzija | ponovo `INSTALIRAJ.bat`, restart AutoCAD-a; traka skrivena: kucajte `RIBBON` |
| U SketchUpu nema trake | traka isključena | **View → Toolbars…** → Vanjsko uređenje PRO |
| Izmjena u AutoCAD-u se ne vidi u SketchUpu | paket nije ponovo izvezen | **SketchUp razmjena → Izvoz paketa**, pa u SketchUpu **Ažuriranje iz paketa** |
| Nešto je pošlo krivo | – | Ctrl+Z poništava cijelu naredbu; sigurnosne kopije: **Postavke i pomoć → Oporavak** |

---

## 9. Kontrolna lista za odštampati

```
PRIPREMA                                   (kartica VANJSKO UREĐENJE PRO)
[ ] Kopija DWG-a otvorena
[ ] Jedinica izmjerena (Measure / DIST): mm / cm / m = ________
[ ] Projekt → Novi projekat        – naziv, jedinica, ishodište na parceli, sjever, mjerilo
[ ] Uvoz DWG-a → Mapiranje slojeva – parcela, tlocrti objekata, oznake kota (+ teren)
[ ] Uvoz DWG-a → Provjera crteža   – nema GREŠAKA
[ ] Teren i kote → Nulta kota      – ±0,00 = ________ m n.v., parter, saobraćajnice
[ ] Uvoz DWG-a → Preuzmi objekte   – parcela i objekti preuzeti
[ ] Teren i kote → Kote iz oznaka / Dodijeli kotu
[ ] Teren i kote → Model terena    – samo ako postoji geodetski snimak

PROJEKTOVANJE
[ ] Parter i popločanje            – popločanja, ivičnjaci, ograde, zidovi, stepenice
[ ] Saobraćaj i parking            – ceste, parking, staze
[ ] Zelenilo                       – stabla, grmlje, gredice, travnjaci
[ ] Rasvjeta / Urbani mobilijar / Infrastruktura
[ ] Nacrti i oznake                – Numeracija, Oznaka, Legenda, Sastavnica

KONTROLA I IZVOZ
[ ] Provjera projekta → Provjeri projekat
[ ] Količinski obračun → Tabele
[ ] Ctrl+S, pa SketchUp razmjena → Izvoz paketa → ________.sdpkg

SKETCHUP
[ ] Extensions → Vanjsko uređenje PRO → Uvoz paketa → Standardni kvalitet → Uvezi
[ ] Scene i sjene pregledane
[ ] Za izmjene: Izvoz paketa (AutoCAD) → Ažuriranje iz paketa (SketchUp)
```

---

Sljedeće čitanje: [Priručnik](PRIRUCNIK.md) – svako dugme i svaka naredba detaljno; šifre poruka i šta s njima.
