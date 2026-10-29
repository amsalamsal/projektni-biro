# Changelog

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
* Not yet loaded in AutoCAD or SketchUp – see `docs/KNOWN_LIMITATIONS.md`.
