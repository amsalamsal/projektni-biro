# Matrica kompatibilnosti

Stanje: verzija 0.2.0, 29.10.2026.

Statusi:

* **Implementirano i testirano** – radi i provjereno je u stvarnoj host aplikaciji;
* **Implementirano, nije testirano** – kod postoji i kompajlira se, ali nije pokrenut u toj verziji;
* **Djelimično** – dio funkcija postoji;
* **Planirano**;
* **Nije podržano**.

Nijedna verzija AutoCAD-a ni SketchUpa **nije testirana** u stvarnoj aplikaciji, jer razvojno okruženje (Linux
kontejner) nema Windows, AutoCAD ni SketchUp. Status „testirano“ nije dodijeljen nijednoj verziji.

## AutoCAD

| Verzija | Interna | Build (TFM) | AutoCAD.NET referenca | Kompajlira se | Status |
|---|---|---|---|---|---|
| AutoCAD 2019 i starije | ≤ R23.0 | – | – | – | Nije podržano |
| AutoCAD 2020 | R23.1 | net47 | 23.1.0 | da | Implementirano, nije testirano |
| AutoCAD 2021 | R24.0 | net48 | 24.0.0 | da | Implementirano, nije testirano |
| AutoCAD 2022 | R24.1 | net48 | 24.0.0 | da | Implementirano, nije testirano |
| AutoCAD 2023 | R24.2 | net48 | 24.0.0 | da | Implementirano, nije testirano |
| AutoCAD 2024 | R24.3 | net48 | 24.0.0 | da | Implementirano, nije testirano |
| AutoCAD 2025 | R25.0 | net8.0-windows | 25.0.1 | da | Implementirano, nije testirano |
| AutoCAD 2026 | R25.1 | net8.0-windows | 25.0.1 | da | Implementirano, nije testirano |
| AutoCAD 2027 | R26.0 | net10.0-windows | 26.0.0 | da | Implementirano, nije testirano |
| AutoCAD LT (sve) | – | – | – | – | Nije podržano (LT nema .NET API) |
| AutoCAD za Mac | – | – | – | – | Nije podržano |

Napomene:

* Build za AutoCAD 2021 koristi reference verzije 24.0. Dodatak ne koristi API dodan u 2022–2024, pa isti DLL
  služi za R24.0–R24.3. To je potrebno potvrditi u svakoj verziji.
* Za .NET Framework buildove (2020–2024) uz dodatak se isporučuju `System.Text.Json` i zavisnosti. Učitavaju se iz
  foldera dodatka preko `AssemblyResolve`. Mogući sukob s drugim dodacima koji učitaju drugu verziju istih biblioteka
  **nije provjeren**.
* Vertikalne aplikacije (Civil 3D, Map 3D, Architecture) bazirane na podržanim verzijama trebale bi učitati dodatak,
  ali to **nije provjereno**.

## SketchUp

| Verzija | Ruby | Status |
|---|---|---|
| SketchUp 2021 i starije | 2.7 i starije | Nije podržano (proširenje se učitava uz upozorenje; ne razvija se za te verzije) |
| SketchUp 2022 | 2.7 | Implementirano, nije testirano (provjerena samo sintaksa za Ruby 2.7) |
| SketchUp 2023 | 2.7 | Implementirano, nije testirano (provjerena samo sintaksa za Ruby 2.7) |
| SketchUp 2024 | 3.2 | Implementirano, nije testirano |
| SketchUp 2025 | 3.2 | Implementirano, nije testirano |
| SketchUp 2026 | 3.2+ | Implementirano, nije testirano |
| SketchUp Free / Web | – | Nije podržano (nema Ruby API) |
| SketchUp Make | – | Nije podržano |

Ruby kod je testiran s Ruby 3.3 protiv **lažnog (fake) SketchUp API-ja** u repozitoriju, a ne u SketchUpu. HTML
dijalozi su testirani u Chromiumu sa zamjenom za `sketchup` most, a ne u SketchUpovom CEF-u.

## Funkcije po platformi

| Funkcija | AutoCAD | SketchUp |
|---|---|---|
| Mapiranje slojeva, provjera ulaznog DWG-a | Implementirano, nije testirano | – |
| Preuzimanje parcele i tlocrta | Implementirano, nije testirano | – |
| Nulta kota, parter, spuštanje saobraćajnica | Implementirano, nije testirano | čita iz paketa |
| Kote iz tekstualnih oznaka (s pregledom) | Implementirano, nije testirano | – |
| Georeferenciranje (kontrolne tačke), KML | Implementirano, nije testirano | čita iz paketa |
| Model terena (TIN), profil, analiza | Implementirano, nije testirano | uvoz terena: Implementirano, nije testirano |
| Odvodnja / hidraulika | Djelimično (samo depresije i nagibi, orijentaciono) | – |
| Sigurnosne kopije, dnevnik oporavka | Implementirano, nije testirano | Implementirano, nije testirano |
| Elementi biblioteke (254 vrste) | Implementirano, nije testirano | Implementirano, nije testirano |
| Realistični 3D modeli proizvođača | Planirano | Planirano (danas: vlastite komponente po vrsti) |
| Razmjena SketchUp → AutoCAD | Djelimično (parametri i tačkasti elementi) | Djelimično |
| Količine (mjereno / procijenjeno) | Implementirano, nije testirano | – |
| Provjera usklađenosti s propisima | Nije podržano (samo upozorenja softvera) | – |

## Šta je stvarno provjereno

Pogledajte [izvještaj testiranja](IZVJESTAJ_TESTIRANJA.md): automatski testovi zajedničke biblioteke (C#), Ruby
jezgre i adaptera protiv lažnog API-ja, JSON sheme i HTML dijalozi u Chromiumu.
