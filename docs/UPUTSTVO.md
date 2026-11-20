# Uputstvo za korištenje – Vanjsko uređenje PRO

Vanjsko uređenje PRO ima dva dijela:

* **dodatak za AutoCAD** – kartica trake *VANJSKO UREĐENJE PRO*, paleta i naredbe `SDP…`;
* **proširenje za SketchUp** – meni *Extensions → Vanjsko uređenje PRO*.

AutoCAD je glavni izvor tlocrta. SketchUp od paketa iz AutoCAD-a gradi 3D model.

**Prvi put koristite program?** Počnite s uputstvom [Prvi projekat](PRVI_PROJEKAT.md) – korak po korak, od
instalacije do 3D modela. Samo redoslijed (šta se postavlja prvo, po spisku): [Redoslijed rada](REDOSLIJED_RADA.md).

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
| `SDPPARKINGOKOMITO` | Kartica *Parking mjesta → Okomito*: broj mjesta i broj mjesta za osobe s invaliditetom (2,50 m + šrafura 1,40 m i rampa za trotoar, uvijek ispred broja 1); mjesta 2,50 × 5,00 m |
| `SDPPARKINGPARALELNO` | *Paralelno*: mjesta 5,50 × 2,50 m, strelica u prolazu 3,00 m |
| `SDPPARKINGUGAO` | *Pod uglom 45°*: 3,40 m duž niza, 4,75 m dubine, šrafirani početni trougao, prikazan ugao 45°, strelica u prolazu |
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
* `SDPUVOZPAKETA` – učitava gotov paket `.sdpkg` u crtež kao uredive elemente (u prazan crtež s jedinicom, ishodištem i kotom ±0,00 iz paketa).
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

### 10. Saobraćajnice u SketchUpu – bez AutoCAD-a
Meni *Extensions → Vanjsko uređenje PRO → Saobraćajnice* i traka *Vanjsko uređenje PRO – Saobraćajnice*. Ulice,
kružni tokovi, prijelazi i rampe crtaju se direktno u SketchUpu; grade se **istim motorom kao u AutoCAD-u** (program
`sdp-ceste` dolazi uz proširenje, na Windowsu radi bez ikakve instalacije), pa su spojevi, radijusi, staze, rampe i
3D visine isti.

| Stavka | Namjena |
|---|---|
| Ulica | Prozor s presjekom (smjer, 1–3 trake lijevo/desno, širina trake, srednje ostrvo i do kojeg mjesta ide, dužina spajanja traka, trotoari, biciklističke staze, radijus ivičnjaka, strelice i razmak), zatim klikovi osovine. Visine tačaka: iz kliknutih tačaka (npr. na terenu), sve iste ili upisane za svaku tačku |
| Uredi ulicu | Isti prozor za odabranu osovinu ili kliknutu ulicu; obična cesta postaje ulica (i desni klik → *Uredi ulicu*) |
| Kružni tok | Otok 10–30 m, prijelazni prsten, broj i dužina krakova, presjek krakova; klik na centar (po želji drugi klik za smjer prvog kraka) |
| Pješački prijelaz | Klik na ulicu: trake i širina se preuzimaju, prijelaz ide okomito preko kolovoza; opcija biciklističke staze 80 cm |
| Cesta | Kolni prilaz, interna pristupna ili servisna cesta zadana širinom |
| Kota saobraćajnice | Klikovi na ulicu i visina; između kota visina se mijenja linearno, nagib preko 10 % se javlja |
| Rampa za invalide | Klik na trotoar (ta strana) ili na kolovoz (obje strane) |
| Garažna rampa | Pravolinijska, polukružna, spiralna ili kombinovana; jednosmjerna/dvosmjerna, širina trake, zaštitni razmak, ostrvo, visine (početak može na visini ulice), prijelazne dionice, blagi dio pred ulicom, ploča iznad i svijetla visina, odvodnja, zidovi, grijanje. Uz ulicu se nudi smjer okomit na nju |
| Presjek rampe | Uzdužni presjek rampe na mjestu klika (prati izmjene rampe) |
| Visine i postavke saobraćajnica | Nadmorska visina Z = 0 modela (tada se visine mogu upisivati i kao m n.v.), ravan ili neravan teren, automatsko osvježavanje |
| Osvježi saobraćajnice | Ponovo gradi sve; inače se gradi samo nakon svake izmjene |

* **Osovine** su male grupe u grupi *Saobraćajnice – osovine* (oznaka *VU Saobraćajnice – osovine*). Mogu se
  pomjerati, rotirati, kopirati i brisati alatima SketchUpa, a tačke ravne osovine pomjerati unutar njene grupe (i
  gore-dolje: to je visina osovine tu). Saobraćajnice se same osvježe; *Poništi* (Undo) vraća i osovinu i 3D.
* **3D** se gradi u grupi *Saobraćajnice – 3D* (kao uvezeni paket), tekstovi L / h / nagib garažnih rampi i uzdužni
  presjeci u grupi *Saobraćajnice – tekstovi i presjeci*.
* Pri crtanju osovine: Enter ili dvostruki klik = kraj, Backspace = poništi tačku, u VCB se upisuje dužina u m,
  `dužina;visina` ili `v visina` (visina sljedeće tačke).
* Upozorenja (nagib ulice preko 10 %, rampe preko 17 %, mali radijus rampe, niska svijetla visina) javljaju se odmah;
  zadnji izvještaj: *Saobraćajnice → Izvještaj posljednje izgradnje*.

#### Parking mjesta u nizu (AutoCAD, kartica *Parking mjesta*)
* Okomito, paralelno ili pod uglom 45°: upišete broj mjesta (kod okomitog i broj mjesta za osobe s invaliditetom),
  kliknete početak niza na ivici trotoara/granici, smjer niza i stranu prolaza (dok birate, niz se vidi).
* Crta se kao u standardu: linije 10 cm, T glava 1,00 m na strani prolaza, kuka 55 cm na početku i kraju niza,
  brojevi mjesta, žuto mjesto za osobe s invaliditetom sa simbolom, šrafurom 1,40 m i rampom za trotoar (uvijek
  ispred broja 1), kod 45° šrafirani početni trougao i ugao 45°, strelica smjera u prolazu. Kote se ne crtaju.
* Kad u crtežu već ima numerisanih mjesta, naredba pita: numeracija **Nastavi** ili **Od1**.
* Svako mjesto je element (broji se u pokazateljima, izvozi u SketchUp s istim oznakama).

#### Tehnički minimumi rampi
Rampa ispod minimuma se **ne može projektovati**: AutoCAD je ne nacrta (poruka s razlogom, izmjena poništena), a
SketchUp poništi izmjenu. Garažna rampa: uzdužni nagib najviše 17 % u svakom dijelu, unutrašnji poluprečnik luka
najmanje 5,00 m, traka najmanje 2,50 m (zakrivljena 3,50 m), zaštitni razmak zakrivljene rampe 0,25 m, svijetla
visina pod pločom najmanje 2,10 m. Pješačka rampa: nagib najviše 8,33 % (1 : 12), širina najmanje 1,20 m. Rampa
koja je već u crtežu javlja se u provjeri kao greška `SDP-C012`.
Garažna rampa je na **prvom i zadnjem metru ravna** (bez poprečnog pada, u ravni s ulicom ili pločom); poprečni pad
3 % počinje tek nakon 1 m i raste na punu vrijednost na sljedeća 2 m.

#### Saobraćajnice u oba smjera (SketchUp ⇄ AutoCAD)
* **SketchUp → AutoCAD:** *Saobraćajnice → Izvoz saobraćajnica za AutoCAD…* snima ulice, ceste, kružne tokove,
  prijelaze, kote, rampe za invalide, garažne rampe i presjeke (osovine i sve postavke, ne 3D) u `.sdpkg`. U
  AutoCAD-u `SDPUVOZPAKETA`: postojeći elementi se zamjenjuju (isti id), novi dodaju, generisani dijelovi (kolovoz,
  trotoari, oznake) se preskaču i AutoCAD odmah ponovo crta mrežu ulica. Ulice obrisane u SketchUpu program nudi
  obrisati i u crtežu (pitanje Da/Ne). Visine osovina dolaze kao kote saobraćajnice.
* **AutoCAD → SketchUp:** nakon uvoza AutoCAD paketa, *Saobraćajnice → Preuzmi saobraćajnice iz AutoCAD-a* pretvara
  ulice tog projekta u osovine alata Saobraćajnice (uređuju se ovdje; 3D gradi motor). Od tada je projekat
  **povezan**: svaki naredni *Uvoz paketa* iz AutoCAD-a prenosi izmjene ulica (nove, izmijenjene, obrisane) u
  osovine i ponovo gradi saobraćajnice, a izvoz iz SketchUpa ide pod id-em AutoCAD projekta.
* **Koordinate:** paket je u koordinatama AutoCAD projekta (ishodište / državne koordinate se prenose preko
  korijenske grupe uvezenog projekta). Model napravljen uvozom iz AutoCAD-a se zato poklapa sam; ulice nacrtane u
  praznom SketchUp modelu idu u AutoCAD relativno na ishodište crteža.
* Šifre u izvještaju uvoza: `SDP-S001` preuzete saobraćajnice, `SDP-S002` / `SDP-S003` nisu izgrađene / preuzete.

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
| `SDP-S…` | Saobraćajnice između SketchUpa i AutoCAD-a |
| `SDP-R…` | Stavke za reviziju (statika, elektro, odvodnja, pristupačnost) – provjera stručnjaka |

## Slojevi
Slojevi dodatka imaju prefiks `VU-` (npr. `VU-ZELENILO-DRVECE`, `VU-TEREN`, `VU-KOTE-DODJELA`). `SDPLAYERS` ih pravi,
popravlja ili provjerava; opcija *Stari* preimenuje slojeve verzije 0.1 tek nakon Vaše potvrde. Puna lista slojeva
je u `shared/catalog/layers.json`, a vrste elemenata u [biblioteci objekata](OBJECT_LIBRARY.md).
