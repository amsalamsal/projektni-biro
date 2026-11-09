# Izmjene (Changelog)

## Neobjavljeno

### Ulice i ceste (dio alata, panel Crtanje)
* **Ulica** (`SDPULICA`): novi element s punim poprečnim profilom. U prozoru profili (stambena, sabirna, bulevar,
  jednosmjerna, kolni prilaz), jednosmjerna ili dvosmjerna, 1–3 trake po smjeru, širina trake, trotoar lijevo/desno
  (0–5 m, tamno sivi, 30 % prozirnosti), biciklistička staza uz kolovoz prije trotoara (0–2 m, bordo, 30 %
  prozirnosti), radijus ivičnjaka i strelice smjera (svakih 40 m). Uz kolovoz ivičnjak 15 cm, uz vanjsku ivicu
  trotoara 10 cm; isprekidane linije traka (puna sredina kod 2 + 2). Sve na sloju „Saobraćajnice“. Ulica se crta
  ili se odabere postojeća linija.
* **Sve ceste alata čine jednu mrežu**: ulica, interna pristupna saobraćajnica, kolni prilaz i servisni put iz menija
  „Ceste i biciklisti“ crtaju se zajedno i više se ne preklapaju. Na raskrsnicama i T-spojevima uglovi se zaobljuju
  radijusom ivičnjaka (stambena zona do 40 km/h, putnička i manja teretna vozila; podrazumijevano 6 m). Efektivni
  radijus prati stvarni (stvarni + biciklistička staza); radijus se vidi kad kursor stoji na ivičnjaku. Kraća
  osovina koja staje ispred druge ceste sama se produži do nje; linije traka i strelice staju prije raskrsnice.
* Nakon pomjeranja, rastezanja, kopiranja, brisanja ili izmjene u Svojstvima (`SDPEDIT`) mreža se sama osvježi.
  **Uredi ulicu** (`SDPULICAUREDI`) otvara isti prozor za postojeću ulicu ili cestu; **Osvježi ceste**
  (`SDPCESTEOSVJEZI`) ručno. Osovine iz prve verzije pretvaraju se u ulice.
* **Pješački prijelaz** (`SDPPJESACKIPRIJELAZ`) je sada element: klik na cestu, uzima broj i širinu traka; bijele
  pruge 0,50 m s razmakom 0,50 m i kote; linije traka i strelice se na prijelazu prekidaju.
* Tabele i legenda dobijaju redove Kolovoz, Biciklistička staza, Ivičnjak 15 cm, Trotoar i Ivičnjak 10 cm; isti
  elementi, linije i strelice idu u SketchUp paket.
* Crtež bez projekta dobija mjerilo 1 m = 100 cm. Rasvjeta se dodaje posebno (Rasvjeta).

### Analize za investitora i inspekciju
* **Urbanistički pokazatelji** (`SDPPOKAZATELJI`, Provjera i izvoz): koeficijent izgrađenosti i iskorištenosti,
  procenat zelenih i vodopropusnih površina, broj parking mjesta, mjesta za osobe s invaliditetom i mjesta s
  punjačem. Zahtjeve regulacionog plana upisujete u prozoru (čuvaju se u projektu); uz svaki pokazatelj stoji
  ocjena „ispunjeno“ (zeleno) ili „NIJE ispunjeno“ (crveno). Tabela se postavlja klikom, a ponovno pokretanje je
  zamjenjuje.
* **Prezentacija u boji** (`SDPPREZENTACIJA`, Dokumentacija): jednim klikom travnjak i zasadi u zelenim tonovima,
  popločanje i ceste u boji svog materijala, voda plava, stabla i grmlje s krošnjom, odsjajem i sjenom, objekti
  svijetli s bačenom sjenom. Boje su na slojevima VU-PREZ-* ispod crteža; opcija Ukloni ih briše.
* **Prohodnost vozila** (`SDPPROHODNOST`, Provjera i izvoz): stvarna provjera kretanja vatrogasnog vozila, kamiona
  za smeće, dostavnog ili putničkog vozila po odabranoj ili nacrtanoj putanji (i kroz kružni tok). Crta obrise
  karoserije i tragove točkova, a crvenim krugom i porukom označava krivine preoštre za vozilo, udare karoserije u
  drveće, stubove, opremu, objekte i zidove, te prelazak točkova preko srednjeg ostrva, travnjaka i zasada.

### Prepoznatljivi simboli u tlocrtu
* 69 vrsta opreme koje su do sada imale opći znak (pravougaonik ili krug s krstom) dobilo je vlastiti simbol pogleda
  odozgo u stvarnoj veličini: tobogan, dječija sprava, penjalica, fitness sprava, gol, trampolin, kontejner, korpa,
  toplotna pumpa i druge jedinice s ventilatorom, punjači za auta i bicikle, ormari, zidne i podne svjetiljke,
  žardinjere, skulpture, znakovi, kiosk, interfon i ostalo. Vlastiti simbol dobili su i ležaljka, stolica, stol za
  stoni tenis, šahovski sto, košarkaški koš, vanjski tuš, kamera, zvučnik i Wi-Fi tačka.
* Postojeći crteži dobijaju nove simbole naredbom **Popravi oznake**.
* Nova naredba **Pregled simbola** (`SDPSIMBOLI`, Napredno): list sa svim simbolima i nazivima.

### Jednostavnije za korištenje
* **Ulica** (`SDPULICA`): cijeli profil ulice iz jedne osovine – kolovoz, ivičnjaci, trotoari, biciklističke staze,
  drvoredi i rasvjeta. Profili Stambena, Sabirna, Bulevar, Pješačka, Prilaz; širina kolovoza se pita.
* **Kružni tok** (`SDPKRUZNITOK`): prsten, ivičnjaci, srednje ostrvo sa zelenilom, krakovi s razdjelnim ostrvima,
  pješačkim prelazima, strelicama i saobraćajnim znakovima.
* **Dokumentuj list** (`SDPDOKUMENTUJ`): jednim klikom oznake, površine, širine, legenda, tabele, sjever, razmjernik
  i sastavnica na najmanjem listu koji stane (A3–A0); tabele idu desno od legende ili ispod plana.
  Ponovno pokretanje zamjenjuje list bez dupliranja.
* **Novi projekat u jednom prozoru** umjesto osam pitanja u komandnoj liniji; na ravnom 2D crtežu nulta kota
  automatski bira relativne Z.
* **Izbor tipa sa slikama**: kad naredba treba tip, otvara se lista s pretragom, kategorijama, nedavnim i omiljenim –
  nema više kucanja id-ova. Dugmad trake i dvoklik u biblioteci odmah predaju odabrani tip.
* **Širina se pita pri crtanju** staza, puteva, kanala i ivičnjaka (`SDPDRAW`, `SDPCONVERT`); zadnja širina po tipu
  se pamti.
* **Decimalni zarez svuda** u komandnoj liniji (npr. `4,5`), uz tačku.
* **Traka sa 6 panela** (Projekat, Crtanje, Izmjena, Dokumentacija, Provjera i izvoz, Pomoć) i padajući meni
  **Napredno** za rijetke naredbe; padajući meniji tipova po kategorijama.
* **Svojstva se mijenjaju odmah** na kartici Svojstva (Enter, izlaz iz polja ili izbor) – bez dugmeta Primijeni.
* **Oznake se ne preklapaju**: oznaka koja bi pala preko druge pomjera se dijagonalno.
* **Mirnija provjera**: iste poruke su grupisane (×N), stavke „za stručni pregled“ su skrivene dok se ne uključe,
  dvoklik zumira na sve elemente grupe.
* Ulica: rasvjeta u istom pojasu kao drvored postavlja se između stabala (ranije je padala na stabla i javljala kolizije).
* Provjera grupiše stavke po šifri (npr. sve kolizije u jednu stavku ×N); dugme Biblioteka uvijek otvara karticu Biblioteka.
* **Prvi koraci** (`SDPPRVIKORACI`, kartica u paleti): 8 koraka od projekta do izvoza koji se sami štikliraju;
  dvoklik pokreće korak.

### Ispravljeno
* AutoCAD 2021: površine sa šrafurom (22 tipa) nisu se mogle napraviti (`eInvalidInput`); `SDPSAMPLE` i `SDPDRAW` su padali.
* `SDPDRAW`: nakon greške više ne ostaje zaostala polilinija.
* Ključne riječi s istim početnim slovom dobile su različite skraćenice (npr. `PRavougaonik`).
* `SDPUVOZPAKETA`: element zamijenjen paketom vraća se ako novi ne uspije; režim kota čita se iz paketa.
* SketchUp: površine bez tri različita tjemena ili bez površine odbijaju se jasnom porukom (SDP-G005/G006),
  a granica koja siječe samu sebe javlja upozorenje (SDP-G007).

* Kartica trake se vraća kad je AutoCAD ukloni pri ponovnom učitavanju menija.
* Sigurnosna kopija sačuvanog (otvorenog) crteža više se ne prijavljuje kao neuspjela.
* Provjera ulaznog DWG-a i mapiranje slojeva ne ubrajaju slojeve samog dodatka.
* `SDPZOOM`/`SDPSETPARAM` na pogrešnom handleu, `SDPEDIT` s više tipova, `SDPKOTEIZOZNAKA` na „Ne“.
* Nakon klika na dugme trake ili palete odgovori koje korisnik kuca sada stižu u komandnu liniju (fokus se vraća crtežu).
* `SDPDATUM` → NultaKota na ravnom 2D crtežu (sve na Z = 0) tretira Z kao relativne; ranije je cijeli model u paketu
  završavao npr. 520 m ispod nulte kote, a provjera je javljala SDP-G012 za svaki element.
* Korisničke postavke se zapisuju atomski; nestanak struje više ne ostavlja oštećen `preferences.json`.
* Paleta se pri prvom otvaranju sidri desno umjesto da kao mali prozor prekrije dugmad trake.

### Dodano
* GitHub Actions: izgradnja i testovi pri svakom slanju; gotovi paketi na grani `builds/<grana>`.

## 0.2.0 – 29.10.2026.

### Dodano
* **Podrška za AutoCAD 2020–2024:**
  * buildovi R23 (net47) i R24 (net48), uz postojeće R25 (net8) i R26 (net10);
  * zajednička biblioteka se gradi za net47 i net8.0.
* **Bosanski jezik:**
  * cijeli korisnički interfejs i katalog;
  * kartica trake **VANJSKO UREĐENJE PRO** sa 16 panela;
  * decimalni zarez;
  * slojevi `VU-…` i SketchUp oznake `VU …`.
* **Uvoz DWG-a:**
  * mapiranje slojeva na uloge (`SDPMAPIRANJE`), s predlošcima `*.sdpmap.json`;
  * provjera ulaznog crteža sa šiframa `SDP-I…` (`SDPPROVJERADWG`);
  * preuzimanje parcele i tlocrta (`SDPPREUZMI`).

  Nazivi izvornih slojeva se ne mijenjaju, a oznaka objekta se ne pretvara automatski u tlocrt.
* **Kote:**
  * nulta kota objekta, kota partera i spuštanje saobraćajnica (`SDPDATUM`). Spuštanje nema podrazumijevanu
    vrijednost;
  * kota za pojedinačni objekat s pregledom (`SDPKOTA`);
  * prijedlog kota iz tekstualnih oznaka s prikazom veza i potvrdom (`SDPKOTEIZOZNAKA`).
* **Georeferenciranje:**
  * kontrolne tačke (`SDPGEOREF`): jedna tačka daje samo položaj, dvije ili više daju transformaciju s
    odstupanjima;
  * historija transformacija;
  * Gauss-Krüger zone 5–7 (MGI 1901), UTM, ETRS89;
  * KML za orijentaciju (`SDPKML`).
* **Teren:**
  * TIN iz tačaka, izohipsi i prelomnica (`SDPTEREN`);
  * profil (`SDPPROFIL`);
  * orijentaciona analiza nagiba, depresija i kubatura (`SDPANALIZATERENA`);
  * uvoz terena u SketchUp.
* **Provjere lokacije:**
  * `SDP-C020` element izvan parcele;
  * `SDP-E010` saobraćajnica iznad partera;
  * `SDP-E011` dodijeljena kota bez datuma;
  * `SDP-E012` objekat pomjeren nakon dodjele kote.
* **Sigurnost:**
  * provjerene sigurnosne kopije prije većih operacija, s pravilima čuvanja;
  * dnevnik oporavka (`SDPKOPIJA`, `SDPKOPIJEPOSTAVKE`, `SDPOPORAVAK`);
  * isto u SketchUpu (*Sigurnosne kopije i oporavak...*);
  * veće operacije u AutoCAD-u su jedan korak `UNDO`.
* **Shema razmjene 1.1.0:** kote, georeferenca i `terrain.json`. Paketi 1.0.0 se i dalje čitaju.
* **Pomoć:** `SDPPOMOC` otvara bosansku dokumentaciju; matrica kompatibilnosti; registar licenci.

### Promijenjeno
* Minimalna verzija SketchUpa je 2022.
* Postavke projekta imaju verziju 2. Verzija 1 se nadograđuje bez izmišljanja kota.
* Količine razlikuju izmjerene i procijenjene (`~`) vrijednosti. CSV koristi separator `;`.

### Poznati problemi
* Nije pokrenuto u AutoCAD-u ni u SketchUpu – pogledajte `docs/KOMPATIBILNOST.md` i `docs/OGRANICENJA.md`.

## 0.1.0 – 2026-10-29 (Phase 1 and vertical slice)

### Added
* Shared exchange format (schema 1.0.0): `manifest.json`, `geometry.json`, `assets/`, `preview/`; JSON schemas;
  reverse change file for SketchUp → AutoCAD.
* Shared catalog: 254 element types in 16 categories, 43 parametric builders, 38 layers, 59 materials,
  model groups, schedules.
* `SiteDesignPro.Core` (C#): model, units, geometry, package writer/reader, content hashing, validation,
  quantities and schedules, layout generators, 2D symbols, sample residential complex; `sdp` CLI.
* AutoCAD plug-in (2025/2026 R25, 2027 R26): ribbon tab with 20 panels, palette, 43 commands for project setup,
  placement, drawing, conversion, editing, annotation, schedules, validation, export and applying SketchUp changes.
* SketchUp extension: package import with preview, three levels of detail, update with change detection and
  policies, tags, scenes, attributes, element properties, library/settings dialog, change export.
* Build pipeline, packaging (`.bundle`, `.rbz`), tests, documentation.

### Known issues
* Not yet loaded in AutoCAD or SketchUp – see `docs/OGRANICENJA.md`.
