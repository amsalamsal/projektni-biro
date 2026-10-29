# Architecture

## 1. Technical assessment (environment)

The development environment is a Linux container: .NET SDK 8 and 10, Ruby 3.3, Python 3, Node with Playwright/Chromium.
**There is no Windows, AutoCAD or SketchUp available**, which drives the main design decision:

* Everything that does not need a host application lives in host-independent libraries that are compiled and
  tested here (`SiteDesignPro.Core` in C#, `core/` in pure Ruby).
* The host adapters (AutoCAD .NET, SketchUp Ruby API) are thin. The AutoCAD adapter is compiled against the
  official `AutoCAD.NET` NuGet reference assemblies (compile-time only). The SketchUp adapter is exercised against
  an in-repo fake of the SketchUp API (`tests/support/fake_sketchup.rb`).
* Neither adapter has been loaded in its real host. See `KNOWN_LIMITATIONS.md`.

## 2. Repository layout

```
shared/
  catalog/      layers, materials, builders (parametric families), element types, categories/model groups/schedules
  schema/       JSON Schema (2020-12) for manifest.json, geometry.json and the SketchUp change file
  samples/      residential-complex.sdpkg (+ unpacked folder) and residential-complex-changes.json
  icons/        SVG sources of all toolbar/ribbon icons
autocad-site-design-pro/
  src/SiteDesignPro.Core      net8.0, no AutoCAD dependency: model, catalog, geometry, exchange, validation,
                              quantities, layout generators, symbols, sample project
  src/SiteDesignPro.Cli       `sdp` command line (sample, validate, schedule, catalog, stress)
  src/SiteDesignPro.AutoCAD   AutoCAD adapter; multi-targeted net8.0-windows (R25) and net10.0-windows (R26)
  tests/SiteDesignPro.Core.Tests  xUnit
  build/PackageContents.xml   Autoloader manifest
sketchup-site-design-pro/
  src/pb_site_design_pro.rb   SketchUp extension registration
  src/pb_site_design_pro/core       pure Ruby: package reader, geometry, mesh generation, builders, change planner
  src/pb_site_design_pro/sketchup   SketchUp adapter: canvas, materials, model index, importer, scenes, editing, UI
  tests/                      minitest + fake SketchUp API + Chromium tests for the HTML dialogs
tools/  build_and_test.sh, package.py, validate_schema.py, gen_object_library.py, icons/
```

## 3. Data model

* **Element** = stable id (UUID v4), catalog type id, label/number, revision, parameters (typed values defined by the
  builder schema with type defaults), geometry, optional custom properties.
* **Geometry kinds**: `point` (position + rotation), `path` (vertices + bulges, open/closed), `polygon` (outer ring +
  holes, rings may contain bulges). Bulge follows the AutoCAD convention: `tan(sweep/4)`, positive = counter-clockwise,
  bulge *i* belongs to segment *i → i+1*.
* **Catalog** is data, not code. `element-types.json` (254 types) references one of 43 builders in `builders.json`.
  A builder defines the geometry kind, the parameter schema, quantity rule, 2D symbol, hatch and linetype. Both
  hosts implement the same builders; adding a type that reuses a builder requires no code. Users can add overlay
  catalogs in `%APPDATA%\SiteDesignPro\catalogs` (AutoCAD) - they are embedded into each exported package, so
  SketchUp needs no copy of them.

## 4. Exchange package (schema 1.0.0)

`.sdpkg` = zip (or an unpacked folder) containing:

| File | Content |
|---|---|
| `manifest.json` | format, schemaVersion, packageId, project (id, name, revision), units, coordinate system (origin E/N/elevation, true north), generator, embedded `typeDefinitions` and `materials`, element list (id, type, label, revision, contentHash, parameters, model group) |
| `geometry.json` | geometry per element id |
| `assets/` | optional files referenced by the manifest (sha256 listed) |
| `preview/` | validation report (JSON + text) |

Rules:
* Lengths in **metres**, relative to the project origin (drawing coordinates minus origin, converted from the drawing
  unit). Elevations are absolute to the project datum minus origin elevation. Angles in radians, CCW about +Z.
* `contentHash` = SHA-256 over canonical JSON (sorted keys, coordinates rounded to 1e-9) of type, parameters,
  geometry and custom properties. C# and Ruby produce identical hashes (tested).
* Readers reject unknown major versions and report (not crash on) unknown types, missing geometry and invalid values.
* Writes are atomic (temp file + rename); the writer refuses to overwrite a folder that is not a package.

### Reverse direction (SketchUp → AutoCAD)

`site-design-pro-changes` JSON (`shared/schema/changes.schema.json`) carries, per element: base content hash,
parameter edits and moves/rotations of point elements. AutoCAD applies a change only when the element still has the
base hash (otherwise reports a conflict, or overwrites if the user chooses so). Geometry edits of paths/polygons
are deliberately **not** sent back: AutoCAD remains the master for 2D layout.

## 5. AutoCAD adapter

* **Element storage**: XData application `SITEDESIGNPRO` (id, type - visible to LIST and cheap to filter) plus an
  extension-dictionary Xrecord `SDP_ELEMENT` with the full JSON record (chunked to 240 chars). The record also stores
  the owner handle so copies (COPY, clipboard, WBLOCK) are detected; `SDPAUDIT` gives them new ids.
* **Representation**: point elements are block references to generated symbol blocks
  (`SDP_<symbol>_<unit>_<hash>`; same parameters share one block). Paths are polylines (width or offset edges).
  Polygons are closed polylines with associative hatches. Annotation and auxiliary objects carry
  `SITEDESIGNPRO_AUX` XData pointing to the element and are regenerated, never exported.
* **Project settings** in the named-object dictionary (`SDP_PROJECT`): unit, origin, north, scale, numbering, checks.
* **Commands** use one transaction each (`CommandContext`), never leave partial objects on cancel and log to
  `%LOCALAPPDATA%\SiteDesignPro\logs\`.
* **UI**: Ribbon tab `SITE DESIGN PRO` with 20 panels; 14 category panels are generated from the catalog (all types of
  those categories); the palette lists every type. A palette (WPF) has a searchable library, property editor and validation list.

## 6. SketchUp adapter

* `core/` is host-independent: package reader (zip reader in pure Ruby), builders that emit geometry to a *canvas*
  interface (`group`, `mesh`, `edges`, `instance`). `RecordingCanvas` is used in tests and checks that every solid
  is closed and consistently oriented. `SuCanvas` writes real SketchUp geometry (one group per solid, oriented
  faces, holes, softened edges, shared component definitions per key).
* **Model organisation**: one project root group → model-group groups (tagged, e.g. `SDP Planting`) → element groups
  / component instances. Each element carries attribute dictionary `SiteDesignPro` (id, type, revision, content hash,
  parameters, geometry fingerprint).
* **Update**: `ChangePlanner` compares package and model: create, update, unchanged, keep_local (user edited, policy
  keep), conflict, mark_deleted / delete / keep_orphan, restore, detach_duplicate (copied element), skip. The plan is
  shown in a preview dialog before anything changes. Each import is one undoable operation.
* **Levels of detail**: conceptual / standard / presentation, chosen per import.
* Scenes (10), tags per model group, north angle from the package, an underground toggle.

## 7. Key decisions and risks

| Decision | Reason | Risk |
|---|---|---|
| Host-independent cores | Most logic testable without hosts | Adapter-only bugs remain until host testing |
| Data-driven catalog shared by both hosts | One source of truth; 254 types without 254 classes | Builders must stay in sync between C# (2D) and Ruby (3D) - covered by tests that build every type |
| AutoCAD is master for layout; SketchUp sends back parameters and point moves only | Avoids lossy 3D→2D geometry reconstruction | Users must make plan geometry changes in AutoCAD |
| Content hash per element | Cheap change detection both ways, conflict detection | Hash must be canonical across languages (tested) |
| Generated symbol blocks instead of a DWG block library | No binary assets; symbols follow parameters | Symbols are schematic, not manufacturer drawings |
| Two AutoCAD binaries (R25 net8, R26 net10) | AutoCAD 2027 moved to .NET 10 | R26 reference package version must match the shipping 2027 API |
