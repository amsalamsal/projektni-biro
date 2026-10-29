# Instalacija, nadogradnja i oporavak

> **Važno:** nijedno proširenje još nije instalirano ni pokrenuto u AutoCAD-u ili SketchUpu. Razvojno okruženje
> nije imalo Windows, AutoCAD ni SketchUp. Koraci ispod slijede dokumentovane mehanizme Autodesk Autoloadera i
> SketchUp Extension Managera. Ako nešto ne radi, pošaljite ispis iz komandne linije AutoCAD-a ili Ruby konzole i
> dnevnike navedene ispod.

## AutoCAD – automatska instalacija (preporučeno)

**Gotov paket:**
1. Uzmite `dist/VanjskoUredjenjePRO-AutoCAD-instalacija.zip`, zatvorite AutoCAD i raspakujte **cijeli** zip.
2. Dvostruki klik na `INSTALIRAJ.bat`. Skripta:
   * u registru pronalazi instalirane verzije AutoCAD-a 2020–2027 (uključujući Civil 3D i Map 3D na istoj osnovi)
     i ispisuje koje su podržane;
   * uklanja stare kopije dodatka (da se ne učita dvaput);
   * kopira `SiteDesignPro.bundle` u `%APPDATA%\Autodesk\ApplicationPlugins` i skida Windows blokadu s datoteka;
   * dodaje folder u pouzdane putanje (`TRUSTEDPATHS`) svakog profila pronađenih AutoCAD-a.
3. `INSTALIRAJ_ZA_SVE_KORISNIKE.bat` instalira u `%ProgramData%` (UAC upit). `DEINSTALIRAJ.bat` uklanja dodatak.

**Kompajliranje iz izvornog koda:** u korijenu repozitorija pokrenite `IZGRADI_I_INSTALIRAJ.bat`.
* Potreban je .NET SDK 8, a za AutoCAD 2027 i .NET SDK 10.
* Kompajliraju se samo verzije za pronađene AutoCAD-e. Ako nijedan nije pronađen, kompajliraju se sve.
* Sklopljeni paket se zatim instalira kao gore.

Dnevnik instalacije je `%TEMP%\VanjskoUredjenjePRO-instalacija.log`. Skripte nisu pokrenute na Windowsu tokom
razvoja: provjerena je samo sintaksa i logika koja ne zavisi od registra (PowerShell 7 na Linuxu).

## AutoCAD 2020 – 2027 (Windows) – ručna instalacija

Paket `SiteDesignPro.bundle` sadrži četiri verzije dodatka. Autoloader bira odgovarajuću prema verziji AutoCAD-a:

| AutoCAD | Interna verzija | Folder u paketu | .NET |
|---|---|---|---|
| 2020 | R23.1 | `Contents\Win64\R23` | .NET Framework 4.7 |
| 2021, 2022, 2023, 2024 | R24.0 – R24.3 | `Contents\Win64\R24` | .NET Framework 4.8 |
| 2025, 2026 | R25.0 – R25.1 | `Contents\Win64\R25` | .NET 8 |
| 2027 | R26.0 | `Contents\Win64\R26` | .NET 10 |

1. Izgradite paket (`tools/build_and_test.sh`) ili uzmite `dist/SiteDesignPro.bundle.zip`.
2. Ako je Windows blokirao preuzetu zip datoteku: desni klik → *Properties* → *Unblock*, pa tek onda raspakujte.
3. Raspakujte tako da folder `SiteDesignPro.bundle` bude u
   `%APPDATA%\Autodesk\ApplicationPlugins\` (trenutni korisnik) ili `%PROGRAMDATA%\Autodesk\ApplicationPlugins\`
   (svi korisnici).
4. Pokrenite AutoCAD. Treba se pojaviti kartica trake **VANJSKO UREĐENJE PRO**.
5. Ako je `SECURELOAD` 1 ili 2, dodajte folder paketa u *Trusted Locations* (OPTIONS → Files).

Ručno učitavanje za testiranje: `NETLOAD` i odaberite DLL iz foldera koji odgovara verziji AutoCAD-a. Za AutoCAD
2020–2024 uz DLL moraju biti i pomoćne biblioteke (`System.Text.Json.dll` i ostale iz istog foldera).

Provjera nakon instalacije:

1. `SDPABOUT` – ispisuje verziju, verziju sheme, katalog i putanju dnevnika
   (`%LOCALAPPDATA%\SiteDesignPro\logs\site-design-pro-<datum>.log`).
2. U praznom crtežu `SDPSAMPLE` → *Oba*: crta primjer izvornog crteža i primjer projekta.
3. `SDPPOMOC` otvara ovo uputstvo (`Contents\Docs\index.html`).

## SketchUp 2022 ili noviji

1. *Window → Extension Manager → Install Extension* → odaberite `dist/pb_site_design_pro.rbz`.
2. Pojavljuju se meni **Extensions → Vanjsko uređenje PRO** i traka alata *Vanjsko uređenje PRO*
   (View → Toolbars ako je skrivena).
3. Provjera: *Uvoz paketa...* → `dist/samples/residential-complex.sdpkg`.

Na SketchUpu starijem od 2022 proširenje se učitava, ali prikazuje upozorenje da verzija nije podržana.

Dnevnik je `site_design_pro.log` u privremenom folderu SketchUpa (*Extensions → Vanjsko uređenje PRO → Otvori
dnevnik (log)*).

## Nadogradnja s verzije 0.1

* **AutoCAD:** zatvorite AutoCAD, obrišite stari folder `SiteDesignPro.bundle` i raspakujte novi. Podaci elemenata u
  crtežima ostaju (XData/Xrecord). Postavke projekta verzije 1 se automatski nadograđuju na verziju 2. Ne izmišljaju se
  nulta kota ni visinski datum, pa ih nakon nadogradnje zadajte naredbom `SDPDATUM`.
* **Slojevi verzije 0.1** imaju engleske nazive (npr. `L-PLNT-TREE`). Novi standard koristi nazive `VU-…`. Stari slojevi
  se **ne preimenuju automatski**, jer isti nazivi mogu biti i slojevi Vašeg izvornog crteža. Ako ih je napravilo ovo
  proširenje, pokrenite `SDPLAYERS` → *Stari*. Naredba prikazuje listu i traži potvrdu.
* **SketchUp:** u Extension Manageru deinstalirajte staru verziju, ponovo pokrenite SketchUp i instalirajte novi `.rbz`.
  Postojeći modeli zadržavaju grupe i atribute. Nove oznake (tagovi) imaju prefiks `VU `. Stare oznake `SDP …`
  ostaju u modelu dok ih ne obrišete ručno.
* Paketi sheme 1.0.0 se i dalje čitaju. Novi paketi imaju shemu 1.1.0 (kote, georeferenca, teren).

## Sigurnosne kopije i oporavak

* **AutoCAD:** prije svake veće operacije (preuzimanje objekata, kote iz oznaka, teren, primjena izmjena iz SketchUpa,
  generatori nizova) dodatak kopira **zadnju sačuvanu verziju** crteža u folder `_SDP_backup` pored crteža i provjerava
  kopiju (SHA-256). Nesačuvane izmjene nisu u kopiji, pa crtež sačuvajte prije većih operacija. Cijela operacija je
  jedan korak `UNDO`.
  * `SDPKOPIJA` – ručna kopija odmah.
  * `SDPKOPIJEPOSTAVKE` – uključivanje, broj kopija po crtežu i najveća starost. Najnovija kopija se nikad ne briše.
  * `SDPOPORAVAK` – lista prekinutih operacija i kopija. *Otvori* vraća kopiju kao **novi crtež** (trenutni crtež se
    nikad ne prepisuje), *Folder* otvara folder kopija, *Oznaci* označava prekinute operacije kao pregledane.
  * Dnevnik operacija: `%APPDATA%\SiteDesignPro\autocad-journal.jsonl`. Pri pokretanju AutoCAD-a dodatak javlja
    prekinute operacije.
* **SketchUp:** prije uvoza i ažuriranja sačuvanog modela pravi se kopija (`Model#save_copy`) u `_SDP_backup` pored
  modela. Dnevnik: `%APPDATA%\SiteDesignPro\sketchup-journal.jsonl`. Meni: *Sigurnosne kopije i oporavak...*

## Deinstalacija

* **AutoCAD:** obrišite folder `SiteDesignPro.bundle`. Crteži zadržavaju geometriju. Podaci elemenata ostaju kao
  XData/Xrecord i AutoCAD ih bez dodatka zanemaruje.
* **SketchUp:** Extension Manager → *Uninstall*. Modeli zadržavaju grupe, oznake i atribute.
