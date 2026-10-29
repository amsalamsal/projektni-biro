# User guide

## Workflow

1. **AutoCAD** – `SDPNEW`: project name, drawing unit, origin (E/N/elevation), true north, annotation scale.
2. Place and draw elements from the ribbon tab **SITE DESIGN PRO** or the palette (`SDPBROWSER`).
   Convert existing linework with `SDPCONVERT`.
3. Annotate (`SDPLABEL`, `SDPAREALABEL`, …), insert schedules (`SDPSCHEDULE`).
4. `SDPVALIDATE`, then `SDPEXPORT` → `<drawing>.sdpkg`.
5. **SketchUp** – *Extensions → Site Design Pro → Import package…*: the preview lists what will be created, then
   builds the 3D model at the chosen level of detail.
6. After AutoCAD changes, export again and use *Update from package*: the preview shows new, changed, deleted,
   locally modified and conflicting elements and applies the chosen policies.
7. Optional reverse path: edit parameters / move point elements in SketchUp → *Export changes to AutoCAD…* →
   `SDPAPPLYCHANGES` in AutoCAD → export again.

AutoCAD is the master for the plan layout. In SketchUp, change paths and areas in AutoCAD and update; local
SketchUp geometry edits are detected and kept or overwritten according to the policy, never silently lost.

## AutoCAD commands

### Project
| Command | Purpose |
|---|---|
| `SDPNEW` | Set up project: name, units, origin, north, scale; creates the layer standard |
| `SDPSETTINGS` | Change project settings (units, origin, north, annotation scale, numbering, checks) |
| `SDPLAYERS` | Create / repair / audit the project layers |
| `SDPAUDIT` | New ids for copied elements, refresh symbols, report broken elements |
| `SDPSAMPLE` | Build the sample residential complex in the current drawing |
| `SDPBROWSER` | Palette: searchable library, properties of the selection, validation results |
| `SDPBROWSECAT` | Palette filtered to a category (used by the ribbon *Browse* buttons) |
| `SDPCATALOGFOLDER`, `SDPRELOAD` | Open the user catalog folder (`%APPDATA%\SiteDesignPro\catalogs`), reload catalogs |
| `SDPABOUT` | Version, schema, catalog, log file |

### Placement and editing
| Command | Purpose |
|---|---|
| `SDPPLACE` | Place a point element (tree, bench, light, manhole …) with a live preview; rotation by angle or point |
| `SDPDRAW` | Draw a path or area element: polyline, rectangle, *Pick* inside a closed boundary, or *Select* existing objects |
| `SDPCONVERT` | Convert lines, arcs, polylines, circles to elements of a chosen type |
| `SDPEDIT` | Edit parameters, label and properties of the selection (dialog) |
| `SDPSETPARAM` | Set one parameter on many selected elements |
| `SDPSELECT` | Select by type, category, label, id or material |
| `SDPZOOM` | Zoom to an element by label or id |
| `SDPPATHARRAY` | Repeat an element along a path at a spacing (tree rows, lights, bollards, stands) |
| `SDPPARKING` | Numbered row of parking bays along an aisle edge at a chosen bay angle; bay type (standard, accessible, EV, …) chosen per row |
| `SDPGRID` | Fill a boundary with a square or staggered planting grid |

The 14 category panels have split buttons per subcategory listing all types of that category, plus a *Browse*
button that opens the palette on that category. Site boundary and building footprint are on the *Site Analysis*
panel; every type, including the remaining building/site reference types, is available in the palette and in the
type prompt of `SDPPLACE` / `SDPDRAW` / `SDPCONVERT`.

### Documentation
`SDPLABEL`, `SDPNUMBER`, `SDPNORTH`, `SDPSCALEBAR`, `SDPSPOTLEVEL`, `SDPSLOPE`, `SDPCOORD`, `SDPSECTION`,
`SDPDETAIL`, `SDPAREALABEL`, `SDPWIDTHLABEL`, `SDPLEGEND`, `SDPTITLEBLOCK`, `SDPREVCLOUD`, `SDPGRIDLINES`,
`SDPCLEARANCE` (clearance envelopes from element values), `SDPLIGHTCOVER` (illustrative circles only — not a
photometric calculation).

### Quantities and exchange
| Command | Purpose |
|---|---|
| `SDPSCHEDULE` | Insert schedules as AutoCAD tables (trees, shrubs, paving, furniture, lighting, drainage, …) |
| `SDPSCHEDULECSV` | Write all schedules to CSV and JSON |
| `SDPQUANTITIES` | Quantities per type on the command line |
| `SDPVALIDATE` | Geometry, data, collisions, overlaps, clearances and review items |
| `SDPEXPORT` | Validate and write the `.sdpkg` package; bumps the revision of changed elements |
| `SDPAPPLYCHANGES` | Apply a SketchUp change file; skips (or overwrites) elements changed in AutoCAD since export |

### Ribbon panels (20)
Project · Site Analysis · Surfaces · Roads & Mobility · Cycling Infrastructure · Water & Drainage · Landscape ·
Utilities · Lighting · Walls & Fences · Structures · Furniture & Recreation · Sustainability · Climate Systems ·
Decorative Elements · Information Systems · Annotations · Quantities & Schedules · SketchUp Export ·
Settings & Libraries.

### Validation codes
* `SDP-G…` geometry (self-intersection, zero length/area, non-planar or non-XY objects, scaled/mirrored symbols)
* `SDP-P…` parameters (range, missing, invalid values)
* `SDP-D…` data (unknown type, duplicate/copied ids, units)
* `SDP-C…` collisions / overlaps / clearances (e.g. tree on a manhole, overlapping surfaces in the same zone)
* `SDP-R…` review items: values that must be checked against local regulations or a specialist (structural,
  electrical, drainage, accessibility). Validation is a design aid, not a code-compliance check.

## SketchUp

Toolbar / menu *Extensions → Site Design Pro*:

| Command | Purpose |
|---|---|
| Import package… | Read `.sdpkg` or `manifest.json`, preview, choose LOD and policies, build |
| Update from package | Re-import the last package with change preview |
| Element properties | Show/edit parameters of the selected element; rebuilds it |
| Export changes to AutoCAD… | Write parameter edits and moved/rotated point elements |
| Create review scenes | Plan, overview, planting, lighting, underground, … (10 scenes) |
| Toggle underground view | Hide surfaces, show buried utilities |
| Library and settings… | Types, component mappings (use your own .skp for a type), LOD, update policies |
| Last import report | Report of the last import (saveable as text) |

Context menu on an element: *Element properties*, *Select all '<type>'*.

**Levels of detail**: *conceptual* (simple masses, fast), *standard* (default), *presentation* (more segments,
detailed furniture/vegetation). Changing LOD re-generates elements on the next update.

**Update policies**: locally modified elements – *keep* (default) or *overwrite*; elements removed in AutoCAD –
*mark* (default; tagged `SDP Deleted`), *delete* or *keep*.

**Organisation**: project root group → one group per model group (tag `SDP <group>`) → element groups /
component instances. Attributes in dictionary `SiteDesignPro` (id, type, revision, hash, parameters).
