# Izmjene (Changelog)

## Neobjavljeno

### Ispravljeno
* AutoCAD 2021: površine sa šrafurom (22 tipa) nisu se mogle napraviti (`eInvalidInput`); `SDPSAMPLE` i `SDPDRAW` su padali.
* `SDPDRAW`: nakon greške više ne ostaje zaostala polilinija.
* Ključne riječi s istim početnim slovom dobile su različite skraćenice (npr. `PRavougaonik`).
* `SDPUVOZPAKETA`: element zamijenjen paketom vraća se ako novi ne uspije; režim kota čita se iz paketa.
* SketchUp: površine bez tri različita tjemena ili bez površine odbijaju se jasnom porukom (SDP-G005/G006),
  a granica koja siječe samu sebe javlja upozorenje (SDP-G007).

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
