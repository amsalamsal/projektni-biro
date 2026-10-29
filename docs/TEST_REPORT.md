# Test report

Date: 2026-10-29. Environment: Linux container, .NET SDK 8.0.132 / 10.0.113, Ruby 3.3.6, Python 3, Chromium
(Playwright). Command: `tools/build_and_test.sh` (exit code 0).

## Summary

| Area | What ran | Result |
|---|---|---|
| C# build | Core, CLI, tests, AutoCAD adapter net8.0-windows (R25) and net10.0-windows (R26) | builds, 0 errors |
| C# unit tests | 61 xUnit tests | 61 passed |
| Sample package | regenerated deterministically (two runs give identical files), validated against the JSON schemas | valid |
| Change file sample | validated against `changes.schema.json` | valid |
| Ruby tests | 71 minitest tests, 1114 assertions | 71 passed |
| Ruby 2.7 syntax | every extension `.rb` parsed with `parser --27` (SketchUp 2021–2023 ship Ruby 2.7) | all parse |
| Dialog UI tests | 23 checks in Chromium against generated fixtures | 23 passed |
| Packaging | `.bundle`, `.bundle.zip`, `.rbz` assembled | produced |

## Not tested

* Loading and running the AutoCAD plug-in in AutoCAD 2025/2026/2027.
* Running the SketchUp extension in SketchUp (only against the in-repo API fake).
* Installing the `.bundle` / `.rbz`.

See `KNOWN_LIMITATIONS.md`.

## C# tests by area

| Class | Tests | Covers |
|---|---|---|
| UnitsTests | 8 | exact unit factors (mm, cm, m, in, ft), mm↔m round trip, INSUNITS mapping, project settings JSON |
| GeometryTests | 9 | bulge arcs, circle from two bulges, polygon area with hole and arc, tessellation, point at distance, self-intersection, oriented rectangle, overlap, duplicate vertices |
| CatalogTests | 5 | embedded catalog equals shared files, every type resolves layer/model group/schedule/defaults, every point type has a 2D symbol, user overlays, invalid defaults rejected |
| ExchangeTests | 10 | round trip (zip and folder) preserving ids, data, units, origin, rotation, elevation; content hash; writer skips; reader rejects foreign/unsupported, reports duplicates/missing geometry/bad coordinates/unsafe assets/corrupt archive; folder protection; deterministic sample |
| ChangeSetTests | 3 | sample change file applies; changed-since-export is a conflict unless forced; invalid values and foreign files rejected |
| ValidationTests | 12 | geometry errors, kinds/ids/duplicates, parameter checks, review items, point collisions by zone, parking obstacles, service clearances, surface overlap and canopy clearance, elevation/unit hints, width/ramp/label logic checks, large-project timing |
| QuantityAndScheduleTests | 4 | count/length/area/volume, schedule grouping, CSV escaping, compact labels |
| LayoutAndLabelTests | 7 | distribution along paths (fixed/fit, closed loops, offsets, unit guards), parking rows, planting grid, numbering |
| SymbolTests | 3 | tree symbol size, parking bay orientation, block sharing by parameters |

## Ruby tests by file

| File | Tests | Covers |
|---|---|---|
| test_builders.rb | 26 | every builder registered; every catalog type at every LOD builds closed solids; dimensions, rotations, burial depths, slopes, terrain-following fences, component sharing, LOD keeps size, fallbacks reported |
| test_geometry.rb | 8 | units, AutoCAD bulge convention, tessellation, offsets, stations, oriented rectangles, closed mesh primitives, fingerprint noise tolerance |
| test_package_reader.rb | 10 | zip and folder read identically, metadata/units/origin, parameter defaults, version handling, foreign documents, invalid data, unsafe assets, corrupt archives, ZIP CRC |
| test_change_planner.rb | 7 | first import, no-op re-import, update/conflict/local edits, delete policies, restore and duplicates, unsupported elements, policy validation |
| test_sample_and_changes.rb | 8 | full sample at all LODs, sample dimensions/positions/elevations, component reuse, review items, large project, change set moves/rotations/parameters and reported unsupported edits, model groups |
| test_sketchup_adapter.rb | 12 | against the SketchUp API fake: organised first import, inch models, face orientation, no duplicates on re-import, update/add/delete, local edits and conflicts, copies detached, delete policy, parameter edits and reverse export, area rebuild, rollback on failure, component reuse |

## Manual test procedure (in the hosts)

AutoCAD:
1. Install the bundle (`INSTALL.md`), open a new drawing, run `SDPABOUT`, check the ribbon tab has 20 panels.
2. `SDPSAMPLE` → expect 139 elements on the SDP layers, hatches, symbols and labels; `SDPVALIDATE` → 0 errors, 12 review items.
3. `SDPPLACE` a tree, `SDPDRAW` a path and a surface, `SDPEDIT` them, `COPY` one and run `SDPAUDIT` (new id).
4. `SDPSCHEDULE`, `SDPSCHEDULECSV`, `SDPEXPORT`.

SketchUp:
1. Install the `.rbz`, *Import package…* the exported file (or `dist/samples/residential-complex.sdpkg`).
2. Check the preview counts, import at each LOD, the tags, scenes and *Element properties*.
3. Change an element in AutoCAD, export, *Update from package* → one update in the preview.
4. Move a bench, change a parameter, *Export changes to AutoCAD…*, run `SDPAPPLYCHANGES` in AutoCAD.
