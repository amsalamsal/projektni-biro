# Izvještaj testiranja

Datum: 30.10.2026. Verzija: 0.2.0 s bibliotekom 3D modela, nakon cjelovitog pregleda.

Okruženje:

* Linux kontejner;
* .NET SDK 8.0.132 i 10.0.113;
* Ruby 3.3.6;
* Python 3;
* Chromium (Playwright).

Naredba: `tools/build_and_test.sh`, izlazni kod 0.

Ovdje su upisani **samo stvarno dobijeni rezultati**. Ništa nije pokrenuto u AutoCAD-u ni u SketchUpu.

## Sažetak

| Oblast | Šta je pokrenuto | Rezultat |
|---|---|---|
| C# build – zajednička biblioteka | `SiteDesignPro.Core` za net47 i net8.0 | 0 grešaka |
| C# build – AutoCAD dodatak | net47 (R23, 2020), net48 (R24, 2021–2024), net8.0-windows (R25, 2025–2026), net10.0-windows (R26, 2027), protiv NuGet referentnih biblioteka AutoCAD.NET | 0 grešaka, 0 upozorenja u dodatku |
| C# testovi (xUnit, net8.0) | 147 testnih slučajeva | 147 prošlo |
| Primjer paketa | ponovo generisan i provjeren prema JSON shemama (manifest, geometrija, teren, izmjene) | ispravno |
| Ruby testovi (minitest) | 104 testa, 6467 provjera | 104 prošlo |
| Ruby 2.7 sintaksa | svaki `.rb` proširenja parsiran s `parser --27` (SketchUp 2022–2023) | sve ispravno |
| HTML dijalozi | 41 provjera u Chromiumu nad generisanim podacima (uvoz, izvještaj, svojstva, biblioteka i postavke, biblioteka 3D modela) | 41 prošlo |
| Biblioteka 3D modela | 253 modela: zatvorena tijela na 3 nivoa detalja, potpunost prema katalogu i specifikaciji (122 objekta, 14 kategorija), ispravan COLLADA i postojeće teksture | sve prošlo |
| Parametri modela | `tools/assets/check_params.rb`: svaki od 1402 parametra svih 253 modela promijenjen, model ponovo izgrađen; parametar bez efekta je greška (osim pregledanog spiska opisnih) | 1402 provjereno, 0 bez efekta |
| Registracija AutoCAD naredbi | `tools/check_commands.py`: svaka klasa s naredbama registrovana u `Plugin.cs` | 58 naredbi, 8 klasa |
| Cijelo SketchUp proširenje | `test_ui_smoke.rb`: `main.rb` učitan kao u SketchUpu, svaka stavka menija i svaki poziv svakog dijaloga, alat za umetanje za svih 253 modela | prošlo |
| COLLADA nezavisnim čitačem | `tools/assets/check_dae.js`: svaki `model.dae` učitan three.js ColladaLoaderom u Chromiumu, dimenzije upoređene s `asset.json` | 253 provjereno, 0 neusklađeno |
| Pakovanje | `.bundle` (R23, R24, R25, R26), `.bundle.zip`, `.rbz` | napravljeno, nije instalirano |

Napomena: net47 build zajedničke biblioteke je kompajliran, ali testovi nisu pokrenuti pod .NET Frameworkom (u
kontejneru nema Mono/.NET Framework okruženja). Testovi rade pod .NET 8.

## Greške pronađene pregledom i ispravljene

| Gdje | Greška | Kako je otkrivena |
|---|---|---|
| AutoCAD 2021, SDPDRAW | naredba je pokretala `PLINE`/`RECTANG` iz koda → `eInvalidInput` i prozor greške | prijava korisnika |
| AutoCAD | 15 naredbi (SDPPOMOC, SDPMAPIRANJE, SDPTEREN, SDPDATUM, SDPOPORAVAK …) „Unknown command": klase nisu bile registrovane | prijava korisnika; sada provjera pri izgradnji |
| AutoCAD | `CommandContext` je hvatao samo neke vrste grešaka; ostale su rušile naredbu | pregled koda |
| AutoCAD | 5 naredbi i rukovaoci događajima palete, trake i dijaloga mapiranja bez zaštite od grešaka | pregled koda |
| AutoCAD | katalog iz instalacije se tražio u pogrešnom folderu (koristila se kopija iz DLL-a) | pregled putanja |
| AutoCAD | ključne riječi mogle su se ispisati dvaput u komandnoj liniji; „Poništi" s kvačicom | pregled koda |
| SketchUp | umetanje modela unutar grupe/komponente u uređivanju bilo je pomaknuto | pregled koda |
| Modeli | visinski pomak (58 modela), materijal (35), dimenzije (40) i niz posebnih parametara nisu mijenjali model | nova provjera parametara |

## Nije testirano

* učitavanje i rad dodatka u AutoCAD-u 2020–2027;
* rad proširenja u SketchUpu 2022+ (testirano samo protiv lažnog API-ja);
* umetanje, rotacija, zamjena i izgled modela biblioteke u stvarnom SketchUpu (nijedan model nema status *Testirano u SketchUpu*);
* uvoz `model.dae` naredbom SketchUpa *File > Import* (provjereno samo nezavisnim COLLADA čitačem);
* instalacija `.bundle` i `.rbz` paketa;
* WinForms dijalog mapiranja slojeva, traka (ribbon), paleta i pregledi (jig) u AutoCAD-u;
* sigurnosne kopije stvarnih DWG/SKP datoteka (testirano na običnim datotekama);
* stvarni DWG crteži korisnika (testirano na sintetičkom primjeru izvornog crteža).

## C# testovi po klasama

| Klasa | [Fact]/[Theory] | Pokriva |
|---|---|---|
| AcceptanceWorkflowTests | 9 | tok rada na primjeru izvornog crteža: mapiranje, provjera, oznake → kote samo za svoje objekte, teren iz geodetskih slojeva, teren iz oznaka bez datuma, KML koordinate, čuvanje postavki, nadogradnja postavki v1 bez izmišljanja kota |
| SiteChecksTests | 3 | primjer prolazi; pomjeren objekat, saobraćajnica iznad partera i element izvan parcele se prijavljuju; kota bez datuma |
| ElevationDatumTests | 5 | novi datum nema kotu i odbija pretvaranje, apsolutno = datum + relativno, parter prati ±0,00 osim ako je zadano drugačije, spuštanje saobraćajnica zadaje korisnik, formatiranje, čuvanje dodjele u svojstvima |
| ElevationLabelTests | 5 | relativne i apsolutne oznake, mala vrijednost bez predznaka je nejasna i ne pogađa se, par apsolutno/relativno, tekst bez broja |
| ElevationAssignerTests | 5 | najmanja kontura, pokazna linija ima prednost, apsolutna oznaka bez datuma se ne pretvara, nesaglasne oznake se blokiraju, jednake konture su nejasne |
| GeoreferenceTests | 4 | jedna tačka bez rotacije se odbija, jedna tačka sa zadanom rotacijom, tri tačke (rotacija, pomak, odstupanja), mm crtež prema metrima → problem razmjere |
| ProjectionTests | 3 | projekcija → WGS84 u skladu s PROJ referentnim vrijednostima, povratna transformacija TM, KML sa zatvorenim prstenom i napomenom o tačnosti |
| TerrainTests | 11 | nedovoljno podataka, ravan, preciznost projektovanih koordinata, prelomnica kao ivica, nasumične tačke i ograničenja, granica, depresije i ravni dijelovi, konfliktne duple tačke, tačne kubature, profil, JSON |
| InputValidatorTests | 7 | šifre SDP-I…: potpun crtež bez grešaka, nedostajući slojevi i kote (ništa se ne izmišlja), otvorene/skoro zatvorene/samopresijecajuće/duple granice, oznaka objekta traži poseban postupak, objekat izvan parcele i sumnja na jedinice, problemi oznaka, nejasne uloge slojeva |
| LayerMappingTests | 3 | zamjenski znakovi i nazivi bez obzira na velika/mala slova, prijedlog po nazivima (samo prijedlog), predlošci |
| SafetyTests | 5 | atomski zapis bez privremenih datoteka, kopije s vremenom i provjerom uz čuvanje najnovije, isključene kopije, dnevnik prekinutih operacija s oštećenim redom, sažimanje dnevnika |
| BosnianFormatTests | 3 | decimalni zarez, unos sa zarezom i tačkom, grupisanje hiljada i množina |
| UnitsTests, GeometryTests, CatalogTests, ExchangeTests, ChangeSetTests, ValidationTests, QuantityAndScheduleTests, LayoutAndLabelTests, SymbolTests | 4, 9, 5, 9, 3, 12, 4, 7, 3 | testovi iz verzije 0.1 (jedinice, geometrija, katalog, paket, izmjene, provjere, količine, raspored, simboli), prilagođeni bosanskim nazivima |

## Ruby testovi po datotekama

| Datoteka | Testova | Pokriva |
|---|---|---|
| test_builders.rb | 26 | svi graditelji i modeli, svaka vrsta na svakom nivou detalja daje zatvorena tijela |
| test_geometry.rb | 8 | jedinice, lukovi, offset, stacionaže, mreže |
| test_package_reader.rb | 10 | zip i folder, verzije sheme, neispravni i nesigurni paketi |
| test_change_planner.rb | 7 | prvi uvoz, ponovni uvoz, konflikti, pravila brisanja |
| test_sample_and_changes.rb | 8 | primjer na svim nivoima, izmjene prema AutoCAD-u |
| test_sketchup_adapter.rb | 12 | uvoz u lažni SketchUp API: organizacija, ažuriranje, konflikti, vraćanje pri grešci |
| test_safety_and_terrain.rb | 9 | kopije i dnevnik, čitanje terena, gradnja terena, ažuriranje terena bez dupliranja |
| test_ui_smoke.rb | 8 | cijelo proširenje učitano kao u SketchUpu (lažni UI): meni, traka, ikone, sve naredbe, svi dijalozi i njihovi pozivi (imena usklađena sa stranicama), uvoz/ponovni uvoz, svojstva, biblioteka modela, alat za umetanje (rotacija, uređivanje unutar grupe), zamjena |
| test_asset_library.rb | 16 | biblioteka modela: 14 kategorija, model i pregled za svaki tip, pokrivenost specifikacije, COLLADA i teksture, zatvorena tijela za svaki model, umetanje, zamjena i uvoz DAE (lažni API) |

## Postupak ručnog prihvatnog testa (u host aplikacijama)

Prihvatni test još **nije izveden**. Izvesti ga za svaku verziju iz [matrice kompatibilnosti](KOMPATIBILNOST.md)
i rezultat upisati ovdje.

AutoCAD:

1. Instalirati paket ([Instalacija](INSTALACIJA.md)). `SDPABOUT`; kartica *VANJSKO UREĐENJE PRO* ima 16 panela.
2. Novi crtež, `SDPSAMPLE` → *Izvorni*. Zatim `SDPMAPIRANJE` (*Prijedlog prema nazivima*, provjera) i `SDPPROVJERADWG`.
   Očekuje se: 1 parcela, 9.600,00 m², 2 objekta, 5 upotrebljivih oznaka, bez grešaka.
3. `SDPDATUM`: ±0,00 = 520,15, spuštanje saobraćajnica 0,15 m. Zatim `SDPKOTEIZOZNAKA`: pregled veza, potvrda;
   saobraćajnica dobija -0,15.
4. `SDPPREUZMI`, `SDPTEREN`, `SDPPROFIL`, `SDPANALIZATERENA`.
5. `SDPGEOREF` s dvije kontrolne tačke, zatim `SDPKML`; otvoriti KML u Google Earthu.
6. `SDPSAMPLE` → *Elementi*, `SDPVALIDATE`, `SDPSCHEDULE`, `SDPEXPORT`.
7. Prekinuti AutoCAD tokom `SDPTEREN` (Task Manager), ponovo pokrenuti i provjeriti poruku i `SDPOPORAVAK`.

SketchUp:

1. Instalirati `.rbz` i *Uvoz paketa...* izvezenog paketa. Provjeriti pregled, nivoe detalja, oznake `VU …`, teren
   i scene.
2. Promijeniti element u AutoCAD-u, izvesti i *Ažuriranje iz paketa* → jedna izmjena u pregledu, teren se ne duplira.
3. Pomjeriti klupu, promijeniti parametar, *Izvoz izmjena za AutoCAD...*, pa u AutoCAD-u `SDPAPPLYCHANGES`.

### Prihvatni test biblioteke 3D modela (SketchUp)

Za svaki model iz [inventara](INVENTAR_MODELA.md), u SketchUpu 2022 i najnovijoj verziji:

1. *Biblioteka 3D modela...* → kategorija → model → **Umetni u model**; klik u model.
2. Strelicama zarotirati prije klika; provjeriti da crvena linija pokazuje smjer modela.
3. Promijeniti jedan parametar u obrascu i ponovo umetnuti; provjeriti dimenziju (Tape Measure).
4. Odabrati umetnuti element → *Svojstva elementa* → promijeniti parametar → *Primijeni*.
5. Odabrati element → drugi model iste vrste → **Zamijeni odabrano**; položaj i rotacija ostaju.
6. Linijski/površinski modeli: nacrtati liniju/pravougaonik, odabrati ivice/lice → **Umetni u model**.
7. **Uvezi DAE** – model se uvozi s teksturama.
8. Upisati rezultat (verzija SketchUpa, ID modela, prošlo/nije, napomena). Tek tada model dobija status
   *Testirano u SketchUpu*, a nakon odobrenja *Završeno*.

