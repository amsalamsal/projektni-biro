# Known limitations

## Not verified in the host applications

The development environment had no Windows, AutoCAD or SketchUp. Therefore:

* **The AutoCAD plug-in compiles** (both targets, against the `AutoCAD.NET` reference assemblies) **but has never
  been loaded in AutoCAD.** Ribbon, palette, jigs, dialogs, hatching, blocks, tables and multileaders are untested
  at runtime.
* **The SketchUp extension has only run against an in-repo fake of the SketchUp API.** The fake models the API
  calls used, but real behaviour (face orientation after `add_face`, hole cutting, `soft`/`smooth` edges,
  `HtmlDialog` messaging, undo operations, performance on large models) is untested.
* The HTML dialogs were tested in Chromium with a stub of the `sketchup` bridge, not in SketchUp's CEF.
* The `.rbz` and `.bundle` packages were assembled but not installed.
* No DWG or SKP sample file exists; `SDPSAMPLE` builds the sample in AutoCAD, and the `.sdpkg` sample is imported
  into SketchUp.

## API conventions handled defensively (to confirm in the hosts)

* `Curve.GetOffsetCurves` side: the result side is measured and the opposite offset used if needed.
* SketchUp north: `shadow_info['NorthAngle']` is set directly to the package `trueNorthDegrees`; the sign
  convention of the two has not been confirmed in SketchUp (check with a project whose true north ≠ 0).
* Block references inserted in a non-world UCS or mirrored: the direction is read from the block transform; scaled
  or mirrored symbols get a validation warning (`SDP-G021`) because 3D size comes from parameters.

## Functional limits (by design in this phase)

* SketchUp → AutoCAD sends parameter edits and moves/rotations of **point** elements only. Paths and areas are
  changed in AutoCAD.
* Geometry must be in the world XY plane (elevations via vertex Z / parameters). Supported source objects:
  lines, arcs, circles, lightweight polylines and 3D polylines. Old-style 2D polylines (CONVERTPOLY), splines and
  ellipses must be converted to lightweight polylines first.
* No terrain model: elements sit on their own elevations; there is no TIN draping or cut/fill.
* Symbols and 3D models are schematic, parametric representations — not manufacturer products. Use component
  mappings in SketchUp for real products.
* Validation is a design aid. Review items (`SDP-R…`) and catalog values marked *review* must be checked against
  local regulations and by the responsible specialists. Lighting coverage circles are illustrative.
* Catalog localisation: English names only. Units: metric drawing units (mm, cm, m) are fully supported;
  imperial units are converted but catalog defaults are metric.
* Only one project per drawing; SketchUp models can hold several projects.

## Performance

Measured only for the host-independent parts (`sdp stress`, Ruby builders with the recording canvas). Import time
in real SketchUp for thousands of elements is unknown; the conceptual LOD and shared components for repeated
point elements are the intended mitigations.
