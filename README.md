# Site Design Pro

AutoCAD + SketchUp extension suite for landscape architecture and site design.

* `autocad-site-design-pro/` — AutoCAD extension (C#, AutoCAD .NET API) and the host-independent `SiteDesignPro.Core` library.
* `sketchup-site-design-pro/` — SketchUp extension (Ruby, SketchUp Ruby API).
* `shared/` — exchange schema, element catalog (types, builders, layers, materials), sample packages.
* `docs/` — architecture, build, installation, user guide, limitations.

Documentation: [Architecture](docs/ARCHITECTURE.md) · [Build](docs/BUILD.md) · [Install](docs/INSTALL.md) ·
[User guide](docs/USER_GUIDE.md) · [Object library](docs/OBJECT_LIBRARY.md) · [Test report](docs/TEST_REPORT.md) ·
[Known limitations](docs/KNOWN_LIMITATIONS.md) · [Changelog](CHANGELOG.md)

> Status: both extensions build and pass their automated tests, but neither has been run inside AutoCAD or SketchUp
> yet (see Known limitations).

Quick start: `tools/build_and_test.sh` builds, tests and writes the installable packages to `dist/`.
