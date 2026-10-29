#!/usr/bin/env python3
"""Validate Site Design Pro packages against the shared JSON Schemas.

Usage: python3 tools/validate_schema.py <package.sdpkg | package-folder> [...]

An independent check (Python jsonschema) that packages written by the C# writer conform to
shared/schema/*.schema.json. Exit code 0 when every package is valid, 1 otherwise.
"""
import json
import pathlib
import sys
import zipfile

try:
    import jsonschema
except ImportError:  # pragma: no cover
    sys.exit("jsonschema is required: pip install jsonschema")

ROOT = pathlib.Path(__file__).resolve().parent.parent
SCHEMAS = {
    "manifest.json": json.loads((ROOT / "shared/schema/manifest.schema.json").read_text(encoding="utf-8")),
    "geometry.json": json.loads((ROOT / "shared/schema/geometry.schema.json").read_text(encoding="utf-8")),
}


def load(path: pathlib.Path):
    if path.is_file() and path.suffix.lower() == ".sdpkg":
        with zipfile.ZipFile(path) as z:
            return {name: json.loads(z.read(name).decode("utf-8")) for name in SCHEMAS}
    return {name: json.loads((path / name).read_text(encoding="utf-8")) for name in SCHEMAS}


def main(paths):
    ok = True
    for p in paths:
        docs = load(pathlib.Path(p))
        for name, schema in SCHEMAS.items():
            validator = jsonschema.Draft202012Validator(schema, format_checker=jsonschema.FormatChecker())
            errors = sorted(validator.iter_errors(docs[name]), key=lambda e: list(e.path))
            if errors:
                ok = False
                print(f"INVALID {p} :: {name}")
                for e in errors[:20]:
                    print("   ", "/".join(map(str, e.path)), "-", e.message[:200])
            else:
                print(f"valid   {p} :: {name}")
        ids = [e["id"] for e in docs["manifest.json"]["elements"]]
        gids = [g["id"] for g in docs["geometry.json"]["elements"]]
        if sorted(ids) != sorted(gids):
            ok = False
            print(f"INVALID {p} :: element ids in manifest and geometry differ")
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1:]))
