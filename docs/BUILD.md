# Build and test

## Requirements

| Tool | Used for |
|---|---|
| .NET SDK 8 **and** 10 | Core, CLI, tests, AutoCAD adapter (R25 = net8.0-windows, R26 = net10.0-windows) |
| Ruby ≥ 2.7 with `minitest` | SketchUp core and adapter tests (`gem install parser` enables the Ruby 2.7 syntax check) |
| Python 3 with `jsonschema` | Schema validation of the sample packages |
| Node + Playwright with Chromium (optional) | Tests of the SketchUp HTML dialogs |

The AutoCAD adapter builds on Linux and macOS (`EnableWindowsTargeting`) because it compiles against the
`AutoCAD.NET` reference assemblies from NuGet. Running it requires AutoCAD on Windows.

## Everything at once

```bash
tools/build_and_test.sh
```

Steps: C# build (CLI + both AutoCAD targets) → xUnit tests → regenerate the deterministic sample package and
validate it against the schemas → Ruby tests → Ruby 2.7 syntax check → Chromium dialog tests → `tools/package.py`,
which writes:

* `dist/SiteDesignPro.bundle` and `dist/SiteDesignPro.bundle.zip` - AutoCAD Autoloader bundle
* `dist/pb_site_design_pro.rbz` - SketchUp extension
* `dist/samples/` - sample package and change file

## Individual steps

```bash
dotnet test autocad-site-design-pro/tests/SiteDesignPro.Core.Tests
```

```bash
dotnet build -c Release autocad-site-design-pro/src/SiteDesignPro.AutoCAD
```

```bash
for f in sketchup-site-design-pro/tests/test_*.rb; do ruby -Isketchup-site-design-pro/tests "$f"; done
```

```bash
python3 tools/validate_schema.py shared/samples/residential-complex.sdpkg
```

## CLI (`sdp`)

```bash
dotnet run --project autocad-site-design-pro/src/SiteDesignPro.Cli -- sample --out site.sdpkg
```

```
sdp sample   --out <folder|file.sdpkg> [--catalog <dir>]   Generate the sample residential complex package
sdp validate <package> [--catalog <dir>] [--json]           Read a package and run all validation rules
sdp schedule <package> [--id <schedule>] [--csv <folder>]   Print quantity schedules
sdp catalog  [--catalog <dir>] [--types]                    Summarise the element catalog
sdp stress   --out <path> --count <n> [--catalog <dir>]     Large synthetic package for performance tests
```
Exit codes: 0 success, 1 validation errors, 2 usage or I/O error.

## Regenerated files

* `docs/OBJECT_LIBRARY.md`: `python3 tools/gen_object_library.py` after catalog changes.
* Icons: `tools/icons/icons.py` + `render.js` rasterise `shared/icons/*.svg` to the AutoCAD and SketchUp PNGs.
* `shared/samples/residential-complex-changes.json`: `ruby sketchup-site-design-pro/tests/support/make_sample_changes.rb`.
