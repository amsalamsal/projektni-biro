#!/usr/bin/env python3
"""Site Design Pro icon family: one 32x32 grid, 2 px rounded strokes, one accent colour per category.

Writes shared/icons/<name>.svg (theme-neutral, using CSS variables) - the source of truth. render.js
rasterises them for AutoCAD (16/32 px, light and dark theme) and SketchUp (16/24 px PNG plus SVG).
Usage: python3 tools/icons/icons.py
"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "shared" / "icons"

ACCENT = {
    "tool": "#2D7FD3", "site": "#D2553F", "buildings": "#8C949C", "surfaces": "#C9A46C", "roads": "#7A8594",
    "cycling": "#D2553F", "water": "#3E8ED0", "landscape": "#4E9A49", "utilities": "#8C6BB1", "lighting": "#E5B532",
    "walls": "#9C7A5B", "structures": "#B56E3E", "furniture": "#3F9C8F", "sustainability": "#5DA83F",
    "climate": "#E07B39", "decorative": "#C0508A", "information": "#3B7BC0",
}

# body uses S (stroke colour, theme dependent) and A (accent). Coordinates on a 32x32 grid.
ICONS = {
    # --- project / tools
    "new": ("tool", '<path d="M8 4h11l6 6v18H8z" fill="none" stroke="S"/><path d="M19 4v6h6" fill="none" stroke="S"/><path d="M16 15v9M11.5 19.5h9" stroke="A"/>'),
    "settings": ("tool", '<circle cx="16" cy="16" r="4.5" fill="none" stroke="A"/><path d="M16 4v4M16 24v4M4 16h4M24 16h4M7.5 7.5l2.8 2.8M21.7 21.7l2.8 2.8M7.5 24.5l2.8-2.8M21.7 10.3l2.8-2.8" stroke="S"/>'),
    "layers": ("tool", '<path d="M16 5l11 6-11 6-11-6z" fill="A" fill-opacity=".35" stroke="S"/><path d="M5 16l11 6 11-6M5 21l11 6 11-6" fill="none" stroke="S"/>'),
    "audit": ("tool", '<rect x="6" y="5" width="16" height="22" rx="2" fill="none" stroke="S"/><path d="M10 11h8M10 16h5" stroke="S"/><circle cx="21" cy="21" r="4.5" fill="none" stroke="A"/><path d="M24.5 24.5l3 3" stroke="A"/>'),
    "browser": ("tool", '<rect x="4" y="5" width="24" height="22" rx="2" fill="none" stroke="S"/><path d="M12 5v22" stroke="S"/><circle cx="20" cy="13" r="3" fill="A"/><rect x="17" y="18" width="6" height="5" fill="A"/>'),
    "sample": ("tool", '<rect x="4" y="12" width="9" height="15" fill="A" fill-opacity=".35" stroke="S"/><rect x="15" y="7" width="8" height="20" fill="A" fill-opacity=".35" stroke="S"/><path d="M3 27h26" stroke="S"/><circle cx="26.5" cy="21" r="2.5" fill="#4E9A49"/>'),
    "validate": ("tool", '<path d="M16 4l10 4v7c0 6-4.5 10.5-10 13C10.5 25.5 6 21 6 15V8z" fill="none" stroke="S"/><path d="M11 16l3.5 3.5L21 13" fill="none" stroke="A"/>'),
    "clearance": ("tool", '<rect x="12" y="12" width="8" height="8" fill="A" fill-opacity=".5" stroke="S"/><rect x="5" y="5" width="22" height="22" rx="3" fill="none" stroke="A" stroke-dasharray="3 2.5"/>'),
    "select": ("tool", '<path d="M8 5l14 9-6 1.5L19 23l-3 1.5-3-7.5-5 4z" fill="A" fill-opacity=".35" stroke="S"/>'),
    "quantities": ("tool", '<rect x="5" y="17" width="5" height="10" fill="A"/><rect x="13.5" y="11" width="5" height="16" fill="A" fill-opacity=".7"/><rect x="22" y="6" width="5" height="21" fill="A" fill-opacity=".45"/><path d="M3 27.5h26" stroke="S"/>'),
    "draw": ("tool", '<path d="M5 25l6-12 8 5 8-12" fill="none" stroke="A"/><circle cx="5" cy="25" r="2" fill="S"/><circle cx="11" cy="13" r="2" fill="S"/><circle cx="19" cy="18" r="2" fill="S"/><circle cx="27" cy="6" r="2" fill="S"/>'),
    "convert": ("tool", '<path d="M4 9h12" stroke="S"/><path d="M4 23h12" stroke="A" stroke-width="4"/><path d="M22 6v20M18.5 22.5L22 26l3.5-3.5" fill="none" stroke="S"/>'),
    "parking": ("roads", '<path d="M5 6v20M13 6v20M21 6v20M29 6v20" stroke="S"/><path d="M4 26h26" stroke="S"/><text x="9" y="18" font-size="9" font-family="Arial" font-weight="700" text-anchor="middle" fill="A" stroke="none">P</text>'),
    "width": ("tool", '<path d="M8 5v22M24 5v22" stroke="S"/><path d="M8 16h16M11 13l-3 3 3 3M21 13l3 3-3 3" fill="none" stroke="A"/>'),
    "patharray": ("tool", '<path d="M4 24c6-14 18-14 24 0" fill="none" stroke="S" stroke-dasharray="2 3"/><circle cx="6.5" cy="18.5" r="3" fill="A"/><circle cx="16" cy="11.5" r="3" fill="A"/><circle cx="25.5" cy="18.5" r="3" fill="A"/>'),
    "slope": ("water", '<path d="M5 9l22 14" stroke="S"/><path d="M27 23l-7.5-.5 2.5-4.5z" fill="A" stroke="A"/><text x="9" y="26" font-size="8" font-family="Arial" fill="S" stroke="none">%</text>'),
    "plantgrid": ("landscape", '<rect x="4" y="4" width="24" height="24" rx="2" fill="none" stroke="S"/><g fill="A"><circle cx="10" cy="10" r="2.6"/><circle cx="22" cy="10" r="2.6"/><circle cx="16" cy="16" r="2.6"/><circle cx="10" cy="22" r="2.6"/><circle cx="22" cy="22" r="2.6"/></g>'),
    "lightcover": ("lighting", '<circle cx="16" cy="16" r="11" fill="A" fill-opacity=".25" stroke="A" stroke-dasharray="3 2"/><circle cx="16" cy="16" r="3" fill="S"/>'),
    "label": ("tool", '<path d="M6 26l9-9" stroke="S"/><circle cx="6" cy="26" r="2" fill="S"/><rect x="14" y="6" width="14" height="10" rx="2" fill="A" fill-opacity=".3" stroke="A"/><path d="M17 11h8" stroke="S"/>'),
    "number": ("tool", '<text x="16" y="22" font-size="15" font-family="Arial" font-weight="700" text-anchor="middle" fill="A" stroke="none">#</text><path d="M4 27h24" stroke="S"/>'),
    "area": ("surfaces", '<path d="M5 8l14-3 8 9-4 13-17-3z" fill="A" fill-opacity=".35" stroke="S"/><text x="16" y="20" font-size="8" font-family="Arial" text-anchor="middle" fill="S" stroke="none">m²</text>'),
    "spotlevel": ("tool", '<path d="M8 22l5-8h-10z" fill="none" stroke="S" transform="translate(3 0)"/><path d="M6 14h20" stroke="S"/><text x="20" y="11" font-size="8" font-family="Arial" text-anchor="middle" fill="A" stroke="none">+0.15</text>'),
    "north": ("tool", '<circle cx="16" cy="17" r="10" fill="none" stroke="S"/><path d="M16 7l-5 15 5-4z" fill="A" stroke="A"/><path d="M16 7l5 15-5-4z" fill="none" stroke="S"/>'),
    "scalebar": ("tool", '<path d="M4 18h24" stroke="S"/><rect x="4" y="14" width="6" height="4" fill="A"/><rect x="16" y="14" width="6" height="4" fill="A"/><rect x="4" y="14" width="24" height="4" fill="none" stroke="S"/><path d="M4 22v2M16 22v2M28 22v2" stroke="S"/>'),
    "coord": ("tool", '<path d="M16 4v24M4 16h24" stroke="S"/><circle cx="16" cy="16" r="5" fill="none" stroke="A"/>'),
    "section": ("tool", '<path d="M5 16h22" stroke="S" stroke-dasharray="4 2"/><circle cx="6" cy="16" r="3.5" fill="none" stroke="A"/><circle cx="26" cy="16" r="3.5" fill="none" stroke="A"/><path d="M6 8v4M26 8v4" stroke="A"/>'),
    "detail": ("tool", '<circle cx="16" cy="16" r="11" fill="none" stroke="S"/><path d="M5 16h22" stroke="S"/><text x="16" y="13.5" font-size="8" font-family="Arial" text-anchor="middle" fill="A" stroke="none">1</text>'),
    "grid": ("tool", '<path d="M10 7v20M22 7v20M5 13h22M5 23h22" stroke="S"/><circle cx="10" cy="4.5" r="2.5" fill="A"/><circle cx="22" cy="4.5" r="2.5" fill="A"/>'),
    "legend": ("tool", '<circle cx="8" cy="9" r="3" fill="A"/><rect x="5" y="14" width="6" height="5" fill="A" fill-opacity=".5"/><path d="M5 25h6" stroke="A"/><path d="M14 9h13M14 16.5h13M14 25h13" stroke="S"/>'),
    "titleblock": ("tool", '<rect x="3" y="5" width="26" height="22" fill="none" stroke="S"/><rect x="15" y="19" width="14" height="8" fill="A" fill-opacity=".35" stroke="S"/>'),
    "revcloud": ("tool", '<path d="M8 22a3 3 0 0 1-2-5 3 3 0 0 1 3-5 4 4 0 0 1 7-3 4 4 0 0 1 7 2 3 3 0 0 1 3 5 3 3 0 0 1-2 5 3 3 0 0 1-5 1 4 4 0 0 1-6 0 3 3 0 0 1-5 0z" fill="none" stroke="A"/>'),
    "schedule": ("tool", '<rect x="4" y="5" width="24" height="22" rx="1.5" fill="none" stroke="S"/><path d="M4 11h24M4 17h24M4 22h24M13 5v22" stroke="S"/><rect x="4" y="5" width="24" height="6" fill="A" fill-opacity=".45"/>'),
    "csv": ("tool", '<path d="M8 4h11l6 6v18H8z" fill="none" stroke="S"/><text x="16.5" y="22" font-size="7.5" font-family="Arial" font-weight="700" text-anchor="middle" fill="A" stroke="none">CSV</text>'),
    "export": ("tool", '<rect x="4" y="12" width="15" height="15" fill="A" fill-opacity=".25" stroke="S"/><path d="M14 17L27 4M19 4h8v8" fill="none" stroke="A"/>'),
    "apply": ("tool", '<path d="M27 6L14 19M14 11v8h8" fill="none" stroke="A"/><rect x="4" y="12" width="15" height="15" fill="none" stroke="S"/>'),
    "edit": ("tool", '<path d="M6 26l2-7 13-13 5 5-13 13z" fill="A" fill-opacity=".3" stroke="S"/><path d="M18 9l5 5" stroke="S"/>'),
    "catalog": ("tool", '<path d="M4 9h9l2 3h13v14H4z" fill="A" fill-opacity=".3" stroke="S"/>'),
    "reload": ("tool", '<path d="M25 11A10 10 0 1 0 26 19" fill="none" stroke="S"/><path d="M26 5v7h-7" fill="none" stroke="A"/>'),
    "about": ("tool", '<circle cx="16" cy="16" r="11" fill="none" stroke="S"/><path d="M16 14v8" stroke="A"/><circle cx="16" cy="10" r="1.5" fill="A"/>'),
    "generic": ("tool", '<rect x="6" y="6" width="20" height="20" rx="3" fill="A" fill-opacity=".3" stroke="S"/>'),
    # --- categories
    "site": ("site", '<path d="M5 6h22v20H5z" fill="none" stroke="A" stroke-dasharray="5 2 1 2"/><path d="M10 12h7v8h-7z" fill="none" stroke="S"/>'),
    "buildings": ("buildings", '<rect x="5" y="11" width="10" height="16" fill="A" fill-opacity=".4" stroke="S"/><rect x="17" y="5" width="10" height="22" fill="A" fill-opacity=".4" stroke="S"/><path d="M20 10h4M20 15h4M20 20h4M8 16h4M8 21h4" stroke="S"/>'),
    "surfaces": ("surfaces", '<path d="M4 20l12-7 12 7-12 7z" fill="A" fill-opacity=".6" stroke="S"/><path d="M10 16.5l12 7M22 16.5l-12 7" stroke="S"/>'),
    "roads": ("roads", '<path d="M11 4L6 28M21 4l5 24" stroke="S"/><path d="M16 6v4M16 14v4M16 22v4" stroke="A" stroke-width="2.5"/>'),
    "cycling": ("cycling", '<circle cx="8.5" cy="20" r="5.5" fill="none" stroke="S"/><circle cx="23.5" cy="20" r="5.5" fill="none" stroke="S"/><path d="M8.5 20l5-9h7l3 9M13.5 11l4 9h-9M18 7h4" fill="none" stroke="A"/>'),
    "water": ("water", '<path d="M16 4c5 7 8 11 8 15a8 8 0 0 1-16 0c0-4 3-8 8-15z" fill="A" fill-opacity=".45" stroke="S"/>'),
    "landscape": ("landscape", '<circle cx="16" cy="12" r="8" fill="A" fill-opacity=".55" stroke="S"/><path d="M16 20v8M11 28h10" stroke="S"/>'),
    "utilities": ("utilities", '<rect x="7" y="13" width="18" height="14" fill="A" fill-opacity=".35" stroke="S"/><path d="M4 13h24" stroke="S"/><path d="M11 13V7h10v6" fill="none" stroke="S"/>'),
    "lighting": ("lighting", '<path d="M12 28h8M16 28V12" stroke="S"/><path d="M16 12h9" stroke="S"/><path d="M21 12h7l-1.5 3h-4z" fill="A" stroke="S"/><path d="M24 18v3M21 18l-1.5 2.5M27 18l1.5 2.5" stroke="A"/>'),
    "walls": ("walls", '<path d="M4 27V9M12 27V9M20 27V9M28 27V9" stroke="S"/><path d="M4 13h24M4 22h24" stroke="A"/>'),
    "structures": ("structures", '<path d="M3 11l13-6 13 6z" fill="A" fill-opacity=".45" stroke="S"/><path d="M7 11v16M25 11v16M16 11v16" stroke="S"/>'),
    "furniture": ("furniture", '<path d="M5 16h22v4H5z" fill="A" fill-opacity=".45" stroke="S"/><path d="M7 20v7M25 20v7M5 16V9h22v7" fill="none" stroke="S"/>'),
    "sustainability": ("sustainability", '<path d="M6 26C6 12 15 6 27 6c0 12-7 20-21 20z" fill="A" fill-opacity=".4" stroke="S"/><path d="M6 26L19 13" stroke="S"/>'),
    "climate": ("climate", '<circle cx="12" cy="12" r="5" fill="A" fill-opacity=".6" stroke="S"/><path d="M12 3v2M3 12h2M5.6 5.6l1.4 1.4M18.4 5.6L17 7" stroke="S"/><path d="M14 26h12a4 4 0 0 0 0-8 6 6 0 0 0-11-1 4.5 4.5 0 0 0-1 9z" fill="none" stroke="S"/>'),
    "decorative": ("decorative", '<path d="M16 4l3.5 7.5L28 13l-6 6 1.5 8.5L16 23.5 8.5 27.5 10 19l-6-6 8.5-1.5z" fill="A" fill-opacity=".45" stroke="S"/>'),
    "information": ("information", '<rect x="5" y="5" width="22" height="15" rx="2" fill="A" fill-opacity=".35" stroke="S"/><path d="M16 20v8M11 28h10" stroke="S"/><path d="M16 9v1M16 13v4" stroke="S"/>'),
    # --- SketchUp toolbar
    "su_import": ("tool", '<path d="M16 4v14M10.5 12.5L16 18l5.5-5.5" fill="none" stroke="A"/><path d="M5 20l11 6 11-6" fill="none" stroke="S"/><path d="M5 20v-3M27 20v-3" stroke="S"/>'),
    "su_update": ("tool", '<path d="M25 11A10 10 0 1 0 26 19" fill="none" stroke="S"/><path d="M26 5v7h-7" fill="none" stroke="A"/><path d="M12 16l3 3 6-6" fill="none" stroke="A"/>'),
    "su_properties": ("tool", '<rect x="5" y="4" width="22" height="24" rx="2" fill="none" stroke="S"/><path d="M9 10h14M9 16h14M9 22h8" stroke="S"/><circle cx="21" cy="22" r="2.5" fill="A"/>'),
    "su_export": ("tool", '<path d="M16 22V8M10.5 13.5L16 8l5.5 5.5" fill="none" stroke="A"/><path d="M5 20l11 6 11-6" fill="none" stroke="S"/>'),
    "su_scenes": ("tool", '<rect x="4" y="9" width="17" height="13" rx="1.5" fill="A" fill-opacity=".3" stroke="S"/><rect x="11" y="5" width="17" height="13" rx="1.5" fill="none" stroke="S"/><path d="M8 26h12" stroke="S"/>'),
    "su_underground": ("utilities", '<path d="M3 12h26" stroke="S"/><path d="M3 12h26v15H3z" fill="A" fill-opacity=".2" stroke="none"/><rect x="10" y="16" width="12" height="8" rx="2" fill="none" stroke="A"/><path d="M16 12v4" stroke="A"/><path d="M8 8l3-3M22 5l3 3" stroke="S"/>'),
    "su_library": ("tool", '<rect x="4" y="5" width="6" height="22" fill="A" fill-opacity=".35" stroke="S"/><rect x="12" y="5" width="6" height="22" fill="none" stroke="S"/><path d="M21 6.5l5.5-1.5 4 21-5.5 1.5z" fill="none" stroke="S"/>'),
    "su_models": ("tool", '<path d="M16 4l11 6v12l-11 6-11-6V10z" fill="none" stroke="S"/><path d="M5 10l11 6 11-6M16 16v12" fill="none" stroke="S"/><path d="M16 4l11 6-11 6-11-6z" fill="A" fill-opacity=".35" stroke="none"/>'),
    "su_report": ("tool", '<path d="M8 4h11l6 6v18H8z" fill="none" stroke="S"/><path d="M12 16h9M12 20h9M12 24h5" stroke="S"/><circle cx="22.5" cy="22.5" r="0" fill="A"/><path d="M12 12h3" stroke="A"/>'),
}


def svg(name: str) -> str:
    cat, body = ICONS[name]
    accent = ACCENT[cat]
    # strokes use currentColor (root colour = light-theme slate) so renderers can recolour for dark themes
    body = body.replace('"S"', '"currentColor"').replace('"A"', '"' + accent + '"')
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32" color="#3C4652" '
            'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">\n'
            f'  <!-- Site Design Pro icon: {name} ({cat}) -->\n  {body}\n</svg>\n')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name in ICONS:
        (OUT / f"{name}.svg").write_text(svg(name), encoding="utf-8")
    print(f"{len(ICONS)} icons written to {OUT}")


if __name__ == "__main__":
    main()
