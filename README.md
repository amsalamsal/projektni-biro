# Vanjsko uređenje PRO

Proširenja za AutoCAD i SketchUp za projektovanje vanjskog uređenja (parter, zelenilo, saobraćaj i parking,
infrastruktura, rasvjeta, urbani mobilijar, teren i kote).

* `autocad-site-design-pro/` – dodatak za AutoCAD 2020–2027 (C#, AutoCAD .NET API) i zajednička biblioteka
  `SiteDesignPro.Core`.
* `sketchup-site-design-pro/` – proširenje za SketchUp 2022+ (Ruby, SketchUp Ruby API).
* `shared/` – shema razmjene, katalog elemenata (vrste, graditelji, slojevi, materijali), primjeri paketa.
* `docs/` – dokumentacija.

Dokumentacija:

* korisnička: [Instalacija](docs/INSTALACIJA.md) · [Uputstvo](docs/UPUTSTVO.md) · [Priručnik – sve opcije](docs/PRIRUCNIK.md) ·
  [Kompatibilnost](docs/KOMPATIBILNOST.md) · [Ograničenja](docs/OGRANICENJA.md) ·
  [Izvještaj testiranja](docs/IZVJESTAJ_TESTIRANJA.md) · [Registar licenci](docs/REGISTAR_LICENCI.md) ·
  [Izmjene](CHANGELOG.md);
* razvojna (na engleskom): [Architecture](docs/ARCHITECTURE.md) · [Build](docs/BUILD.md) ·
  [Object library](docs/OBJECT_LIBRARY.md).

> Status: oba proširenja se grade i prolaze automatske testove, ali **nijedno još nije pokrenuto u AutoCAD-u ni u
> SketchUpu** (pogledajte [Kompatibilnost](docs/KOMPATIBILNOST.md)).

Brzi početak: `tools/build_and_test.sh` gradi, testira i piše instalacione pakete u `dist/`.
