# Uputstvo za korištenje – Vanjsko uređenje PRO

Vanjsko uređenje PRO ima dva dijela:

* **dodatak za AutoCAD** – kartica trake *VANJSKO UREĐENJE PRO*, paleta i naredbe `SDP…`;
* **proširenje za SketchUp** – meni *Extensions → Vanjsko uređenje PRO*.

AutoCAD je glavni izvor tlocrta. SketchUp od paketa iz AutoCAD-a gradi 3D model.

**Prvi put koristite program?** Počnite s uputstvom [Prvi projekat](PRVI_PROJEKAT.md) – korak po korak, od
instalacije do 3D modela.

Ovo je kratki pregled. Svaka naredba, opcija i dijalog detaljno su opisani u [Priručniku](PRIRUCNIK.md).

> Status: kod je napisan, kompajlira se i prolazi automatske testove, ali **nije provjeren u AutoCAD-u ni u
> SketchUpu**. Pogledajte [Kompatibilnost](KOMPATIBILNOST.md) i [Ograničenja](OGRANICENJA.md).

## Osnovna pravila

* **Jedinice:** metričke (mm, cm, m). U korisničkom interfejsu se koristi decimalni zarez: `0,15 m`, `1.234,50 m²`.
  Komandna linija AutoCAD-a prihvata i tačku i zarez.
* **Nazivi slojeva izvornog crteža se ne mijenjaju.** Uloge slojeva (parcela, objekti, kote…) zadaju se mapiranjem.
  Elementi dodatka idu na slojeve `VU-…`.
* **Ništa se ne izmišlja.** Geometrija, kote i koordinate kojih nema u crtežu se ne dodaju. Kada podataka nema
  dovoljno, izvještaj to kaže.
* **Oznaka objekta nije tlocrt.** Blok ili tekst koji označava objekat ne pretvara se automatski u tlocrt; potrebna
  je zatvorena kontura ili Vaša potvrda.
* **Provjere su pomoć pri projektovanju**, a ne potvrda usklađenosti s propisima. Stavke označene za reviziju mora
  pregledati odgovorni projektant ili specijalista.

## Tok rada od DWG-a do SketchUpa

### 1. Projekat
`SDPNEW` – naziv, jedinica crteža, ishodište (Y/X/kota), stvarni sjever, razmjera oznaka. `SDPSETTINGS` mijenja
postavke kasnije. Za učenje: u praznom crtežu `SDPSAMPLE` → *Oba* nacrta primjer izvornog DWG-a i primjer
stambenog kompleksa.

### 2. Uvoz DWG-a (panel *Uvoz DWG-a*)
1. `SDPMAPIRANJE` – tabela svih slojeva crteža s brojem objekata. Svakom sloju dodijelite ulogu: granica parcele,
   tlocrt objekta, oznaka objekta, saobraćajnice, linija partera, visinske oznake, tačke terena, izohipse,
   prelomnice… Dugme *Prijedlog prema nazivima* predlaže uloge po nazivima slojeva, ali ga uvijek provjerite. Mapiranje se može sačuvati kao
   predložak (`%APPDATA%\SiteDesignPro\mappings\*.sdpmap.json`) i koristiti u drugim projektima.
2. `SDPPROVJERADWG` – izvještaj o ulaznom crtežu: jedinice, koordinatni sistem, parcela i njena površina, broj
   objekata, upotrebljive visinske oznake, tačke terena s kotom, otvorene konture, duplikati, objekti izvan mapiranih
   slojeva… Svaki nalaz ima šifru `SDP-I…`.
3. `SDPPREUZMI` – mapiranu granicu parcele i zatvorene tlocrte pretvara u elemente projekta. Oznake objekata (blokovi,
   tekst) preuzimaju se samo uz potvrdu. Već preuzeti objekti se preskaču.

### 3. Georeferenciranje (panel *Georeferenciranje*)
* `SDPGEOREF` – koordinatni sistem (npr. EPSG:3908 MGI 1901 / Balkans zona 6) i kontrolne tačke (lokalna tačka →
  Y/X). Jedna tačka daje **samo položaj**; rotaciju tada zadajete Vi. Od dvije tačke naviše računa se kruta ili
  sličnosna transformacija metodom najmanjih kvadrata, s odstupanjima po tački i upozorenjem na razmjeru (ppm).
  Svaka promjena se bilježi u historiji transformacija.
* `SDPKML` – KML za Google Earth. Služi **samo za orijentaciju** i nije geodetska podloga. Pretvorba MGI 1901 →
  WGS84 koristi približne parametre (greška reda veličine nekoliko metara).

### 4. Kote (panel *Teren i kote*)
* `SDPDATUM`:
  * nulta kota objekta (±0,00) kao apsolutna nadmorska visina i naziv visinskog datuma;
  * kota partera (podrazumijevano jednaka ±0,00, ako ne odaberete drugačije);
  * spuštanje saobraćajnica ispod partera.

  **Spuštanje nema podrazumijevanu vrijednost.** Zadaje ga korisnik; 15 cm je samo primjer. Vrijedi pravilo
  apsolutna kota = datum + relativna kota. Bez zadanog datuma apsolutne kote se ne računaju.
* `SDPKOTA` – kota za **odabrani objekat** (relativna, apsolutna, parter, saobraćajnica, stvarna Z vrijednost). Prije
  primjene se prikazuje stara i nova kota. Mijenja se samo odabrani objekat.
* `SDPKOTEIZOZNAKA` – čita visinske oznake (`+0,00`, `-0,15`, `%%p0.00`, `KT 520,00`, `520,15 m n.v.`…) i predlaže kojem
  objektu pripadaju (oznaka unutar konture ili na kraju pokazne linije). Veze se prvo prikažu privremenim linijama,
  pa tek onda potvrđujete. Nejasne oznake (dvije moguće konture, oznaka bez objekta, oznake koje se ne slažu) se
  prijavljuju i ne dodjeljuju.
* `SDPSPOTLEVEL` – oznaka kote: relativna i u zagradi apsolutna.

### 5. Teren
* `SDPTEREN` – model terena (TIN) iz tačaka s kotom, izohipsi, prelomnica i, po izboru, dodijeljenih kota i
  visinskih oznaka. Prelomnice se poštuju, a granica može ograničiti teren. Ako podataka nema dovoljno (premalo tačaka,
  sve na jednoj liniji, bez raspona kota), teren se ne gradi i izvještaj kaže zašto.
* `SDPPROFIL` – profil terena duž odabrane linije.
* `SDPANALIZATERENA` – nagibi trouglova, ravni dijelovi, lokalne depresije (mogući problem odvodnje) i kubature
  iznad/ispod zadane kote. Rezultati su **orijentacioni** i nisu hidraulički ni geotehnički proračun.

### 6. Projektovanje
Paneli *Parter i popločanje*, *Zelenilo*, *Saobraćaj i parking*, *Infrastruktura*, *Rasvjeta* i *Urbani mobilijar*
imaju padajuće liste svih vrsta elemenata po podkategorijama i dugme *Pregled*, koje otvara paletu na toj kategoriji.

| Naredba | Namjena |
|---|---|
| `SDPPLACE` | Tačkasti element (stablo, klupa, svjetiljka, šaht…) s pregledom; rotacija uglom ili tačkom |
| `SDPDRAW` | Linijski ili površinski element: polilinija, pravougaonik, odabir unutar zatvorene granice ili postojeće krive |
| `SDPCONVERT` | Pretvaranje linija, lukova, polilinija i krugova u elemente odabrane vrste |
| `SDPEDIT` | Parametri, oznaka i svojstva odabranih elemenata |
| `SDPSETPARAM` | Jedan parametar na više elemenata |
| `SDPPATHARRAY` | Niz elemenata duž putanje (drvored, svjetiljke, stubići, stalci) |
| `SDPPARKING` | Numerisani niz parking mjesta uz rub prolaza (standardno, za osobe s invaliditetom, punjač…) |
| `SDPGRID` | Raster sadnje u granici (kvadratni ili smaknuti) |
| `SDPSELECT`, `SDPZOOM` | Odabir po vrsti, kategoriji, oznaci, id-u ili materijalu; zum na element |
| `SDPCLEARANCE` | Zone servisa, sigurnosti i podizanja iz vrijednosti elementa |
| `SDPLIGHTCOVER` | Ilustrativni krugovi pokrivenosti rasvjete – nije fotometrijski proračun |

### 7. Nacrti, oznake i količine
* Oznake: `SDPLABEL`, `SDPNUMBER`, `SDPAREALABEL`, `SDPWIDTHLABEL`, `SDPSLOPE`, `SDPCOORD` (Y = / X =), `SDPNORTH`,
  `SDPSCALEBAR`, `SDPSECTION`, `SDPDETAIL`, `SDPGRIDLINES`, `SDPLEGEND`, `SDPTITLEBLOCK`, `SDPREVCLOUD`.
* Količine: `SDPSCHEDULE` (AutoCAD tabele), `SDPSCHEDULECSV` (CSV sa separatorom `;` i JSON), `SDPQUANTITIES`.
  Količine izmjerene iz geometrije su odvojene od procijenjenih, koje su označene znakom `~`.

### 8. Provjera i izvoz
* `SDPVALIDATE` – geometrija, podaci, kolizije, preklapanja, razmaci, stavke za reviziju i provjere lokacije:
  * `SDP-C020` – element izvan parcele;
  * `SDP-E010` – saobraćajnica iznad partera;
  * `SDP-E011` – dodijeljena kota bez datuma;
  * `SDP-E012` – objekat pomjeren nakon dodjele kote.
* `SDPEXPORT` – zapisuje paket `.sdpkg` (shema 1.1.0) s elementima, kotama, georeferencom i terenom (`terrain.json`).
* `SDPAPPLYCHANGES` – primjena izmjena iz SketchUpa s pregledom. Elementi koje ste u međuvremenu mijenjali u
  AutoCAD-u preskaču se ako ne odaberete prepisivanje.

### 9. SketchUp
Meni *Extensions → Vanjsko uređenje PRO*:

| Stavka | Namjena |
|---|---|
| Uvoz paketa... | Čita `.sdpkg` ili `manifest.json`, prikazuje pregled, nudi izbor nivoa detalja i pravila, pa gradi model |
| Ažuriranje iz paketa | Ponovni uvoz zadnjeg paketa s pregledom: novo, izmijenjeno, obrisano, lokalno mijenjano, konflikti |
| Svojstva elementa | Parametri odabranog elementa; element se ponovo gradi |
| Izvoz izmjena za AutoCAD... | Izmijenjeni parametri i pomjereni ili zarotirani tačkasti elementi, za `SDPAPPLYCHANGES` |
| Izrada scena za pregled | Scene od „VU 01 Situacija“ do „VU 10 Prezentacija“ |
| Prikaz podzemnog | Skriva površine i prikazuje ukopane instalacije |
| Biblioteka i postavke... | Vrste, dodjela vlastitih komponenti (.skp) vrstama, materijali, nivo detalja, pravila ažuriranja |
| Izvještaj posljednjeg uvoza | Izvještaj zadnjeg uvoza, uz mogućnost snimanja kao tekst |
| Sigurnosne kopije i oporavak... | Prekinute operacije i kopije modela |

* **Nivoi detalja:** *Šematski prikaz* (lagano), *Standardni kvalitet* i *Visoki kvalitet*.
* **Pravila ažuriranja:**
  * lokalno izmijenjeni elementi se zadržavaju (uz prijavu konflikta) ili prepisuju verzijom iz AutoCAD-a;
  * elementi obrisani u AutoCAD-u se premještaju na skrivenu oznaku „Obrisano u izvoru“, brišu iz modela ili
    zadržavaju.
* **Teren** se uvozi kao zasebna grupa na oznaci *VU Teren*. Ponovni uvoz ga ažurira, a ne duplira.
* **Organizacija:** korijenska grupa projekta → grupa po modelskoj grupi (oznake `VU …`) → elementi. Atributi su u
  rječniku `SiteDesignPro`.

## Šifre nalaza
| Prefiks | Značenje |
|---|---|
| `SDP-I…` | Ulazni DWG (mapiranje, jedinice, parcela, objekti, kote, tačke terena) |
| `SDP-T…` | Teren (nedovoljno podataka, strmi i ravni dijelovi, depresije) |
| `SDP-E…` | Kote (saobraćajnica iznad partera, nema datuma, pomjeren objekat) |
| `SDP-G…` | Geometrija (samopresijecanje, nulta dužina ili površina, skalirani simboli) |
| `SDP-P…` | Parametri (raspon, nedostaju, neispravne vrijednosti) |
| `SDP-D…` | Podaci (nepoznata vrsta, kopirani id-evi, jedinice) |
| `SDP-C…` | Kolizije, preklapanja, razmaci, izlazak iz parcele |
| `SDP-R…` | Stavke za reviziju (statika, elektro, odvodnja, pristupačnost) – provjera stručnjaka |

## Slojevi
Slojevi dodatka imaju prefiks `VU-` (npr. `VU-ZELENILO-DRVECE`, `VU-TEREN`, `VU-KOTE-DODJELA`). `SDPLAYERS` ih pravi,
popravlja ili provjerava; opcija *Stari* preimenuje slojeve verzije 0.1 tek nakon Vaše potvrde. Puna lista slojeva
je u `shared/catalog/layers.json`, a vrste elemenata u [biblioteci objekata](OBJECT_LIBRARY.md).
