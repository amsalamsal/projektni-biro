#!/usr/bin/env python3
"""Applies the Bosnian (ijekavian) translations to shared/catalog.

The English source text is kept next to each translated field (nameEn, labelEn, ...), so the script is
idempotent and can be re-run after catalog additions; it fails when a translatable string has no translation.
Identifiers (type ids, parameter keys, option values, material keys) are never translated.
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
CAT = ROOT / "shared" / "catalog"
HERE = pathlib.Path(__file__).resolve().parent
TYPES = json.loads((HERE / "catalog_bs_types.json").read_text(encoding="utf-8"))
MISC = json.loads((HERE / "catalog_bs_misc.json").read_text(encoding="utf-8"))
missing = []


def tr(table, key, where):
    if key in MISC[table]:
        return MISC[table][key]
    missing.append(f"{where}: {key!r}")
    return key


def load(name):
    return json.loads((CAT / name).read_text(encoding="utf-8"))


INLINE_LISTS = {"types", "parameters", "materials", "layers", "categories", "modelGroups", "schedules", "commonProperties"}


def inline(value):
    """One-line JSON in the catalog's house style: { "a": 1, "b": [1, 2] }."""
    if isinstance(value, dict):
        return "{ " + ", ".join(f"{json.dumps(k, ensure_ascii=False)}: {inline(v)}" for k, v in value.items()) + " }" if value else "{}"
    if isinstance(value, list):
        return "[" + ", ".join(inline(v) for v in value) + "]"
    return json.dumps(value, ensure_ascii=False)


def dump(value, indent=0, key=None):
    pad = "  " * indent
    if isinstance(value, dict):
        if not value:
            return "{}"
        if len(inline(value)) <= 110 and indent > 0:
            return inline(value)
        items = [f"{pad}  {json.dumps(k, ensure_ascii=False)}: {dump(v, indent + 1, k)}" for k, v in value.items()]
        return "{\n" + ",\n".join(items) + "\n" + pad + "}"
    if isinstance(value, list):
        if key in INLINE_LISTS:
            return "[\n" + ",\n".join(f"{pad}  {inline(v)}" for v in value) + "\n" + pad + "]"
        return inline(value)
    return json.dumps(value, ensure_ascii=False)


def save(name, data):
    (CAT / name).write_text(dump(data) + "\n", encoding="utf-8")


def main():
    types = load("element-types.json")
    for t in types["types"]:
        t.setdefault("nameEn", t["name"])
        t.setdefault("subcategoryEn", t.get("subcategory", ""))
        if t["id"] not in TYPES:
            missing.append(f"type {t['id']}")
        else:
            t["name"] = TYPES[t["id"]]
        if t.get("subcategoryEn"):
            t["subcategory"] = tr("subcategories", t["subcategoryEn"], t["id"])
    save("element-types.json", types)

    builders = load("builders.json")
    builders["optionLabels"] = MISC["optionLabels"]
    for key, b in builders["builders"].items():
        b.setdefault("descriptionEn", b.get("description", ""))
        if key in MISC["builderDescriptions"]:
            b["description"] = MISC["builderDescriptions"][key]
        else:
            missing.append(f"builder description {key}")
        for p in b["parameters"]:
            p.setdefault("labelEn", p.get("label", ""))
            p.setdefault("groupEn", p.get("group", ""))
            p["label"] = tr("paramLabels", p["labelEn"], f"{key}.{p['key']}")
            if p["groupEn"]:
                p["group"] = tr("paramGroups", p["groupEn"], f"{key}.{p['key']} group")
            for o in p.get("options") or []:
                if str(o) not in MISC["optionLabels"]:
                    missing.append(f"option {o}")
    save("builders.json", builders)

    materials = load("materials.json")
    for m in materials["materials"]:
        m.setdefault("nameEn", m["name"])
        m["name"] = tr("materials", m["key"], "material")
    save("materials.json", materials)

    layers = load("layers.json")
    for l in layers["layers"]:
        l.setdefault("legacyName", l["name"])
        l.setdefault("descriptionEn", l.get("description", ""))
        name, desc = MISC["layers"].get(l["key"], [None, None])
        if name is None:
            missing.append(f"layer {l['key']}")
            continue
        l["name"], l["description"] = name, desc
    save("layers.json", layers)

    cats = load("categories.json")
    for c in cats["categories"]:
        c.setdefault("nameEn", c["name"])
        c["name"] = tr("categories", c["id"], "category")
    for g in cats["modelGroups"]:
        g.setdefault("nameEn", g["name"])
        g.setdefault("legacyTag", g["tag"])
        name, tag = MISC["modelGroups"].get(g["id"], [None, None])
        if name is None:
            missing.append(f"model group {g['id']}")
            continue
        g["name"], g["tag"] = name, tag
    for s in cats["schedules"]:
        s.setdefault("titleEn", s["title"])
        s["title"] = tr("schedules", s["id"], "schedule")
    for p in cats["commonProperties"]:
        p.setdefault("labelEn", p["label"])
        p.setdefault("groupEn", p["group"])
        label, group = MISC["commonProperties"].get(p["key"], [None, None])
        if label is None:
            missing.append(f"property {p['key']}")
            continue
        p["label"], p["group"] = label, group
    save("categories.json", cats)

    if missing:
        print("Missing translations:\n  " + "\n  ".join(missing))
        return 1
    print(f"Catalog translated: {len(types['types'])} types, {len(builders['builders'])} builders, "
          f"{len(materials['materials'])} materials, {len(layers['layers'])} layers.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
