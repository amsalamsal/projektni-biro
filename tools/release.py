"""Makes the files of a GitHub release from the built packages (dist/).

    python3 tools/release.py <dist folder> <output folder>

Output: VanjskoUredjenjePRO-<ver>.zip (the installer: AutoCAD + SketchUp + scripts), SiteDesignPro.bundle.zip and
pb_site_design_pro.rbz (what the automatic updates download), latest.json (version, notes, addresses, SHA-256) and
notes.md (the text of the release). The version comes from autocad-site-design-pro/Directory.Build.props and must be the
same in the SketchUp extension; the notes come from the CHANGELOG section "### Verzija <ver>".
"""
import hashlib
import json
import pathlib
import re
import shutil
import sys
import zipfile
from datetime import date

ROOT = pathlib.Path(__file__).resolve().parents[1]
REPO = "amsalamsal/projektni-biro"


def version():
    props = (ROOT / "autocad-site-design-pro" / "Directory.Build.props").read_text(encoding="utf-8")
    v = re.search(r"<Version>([0-9.]+)</Version>", props).group(1)
    rb = (ROOT / "sketchup-site-design-pro" / "src" / "pb_site_design_pro" / "version.rb").read_text(encoding="utf-8")
    su = re.search(r"VERSION\s*=\s*'([0-9.]+)'", rb).group(1)
    if su != v:
        sys.exit(f"Verzije se razlikuju: AutoCAD {v}, SketchUp {su}")
    return v


def notes(v):
    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    m = re.search(r"^### Verzija " + re.escape(v) + r"\b.*?$\n(.*?)(?=^#{2,3} |\Z)", text, re.M | re.S)
    if not m:
        return []
    items, cur = [], None
    for line in m.group(1).splitlines():
        if line.startswith("* "):
            cur = line[2:].strip()
            items.append(cur)
        elif line.startswith("  ") and not line.strip().startswith("*") and items:
            items[-1] += " " + line.strip()
    return [re.sub(r"\*\*|\*|`", "", i) for i in items]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def zip_dir(src, dest, arc_root):
    with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED) as z:
        for p in sorted(src.rglob("*")):
            if p.is_file():
                z.write(p, str(pathlib.PurePosixPath(arc_root) / p.relative_to(src).as_posix()))


def main():
    dist, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    v = version()
    out.mkdir(parents=True, exist_ok=True)
    bundle, rbz = dist / "SiteDesignPro.bundle", dist / "pb_site_design_pro.rbz"

    zip_dir(bundle, out / "SiteDesignPro.bundle.zip", "SiteDesignPro.bundle")
    shutil.copy(rbz, out / "pb_site_design_pro.rbz")

    # the installer, as sent so far: AutoCAD + SketchUp + one-click scripts
    stage = out / "_stage" / "VanjskoUredjenjePRO"
    shutil.rmtree(out / "_stage", ignore_errors=True)
    (stage / "AutoCAD").mkdir(parents=True)
    (stage / "SketchUp").mkdir(parents=True)
    shutil.copytree(bundle, stage / "AutoCAD" / "SiteDesignPro.bundle")
    win = ROOT / "tools" / "windows"
    for f in ("INSTALIRAJ.bat", "DEINSTALIRAJ.bat", "instaliraj.ps1"):
        shutil.copy(win / f, stage / "AutoCAD" / f)
    shutil.copy(rbz, stage / "SketchUp" / "pb_site_design_pro.rbz")
    for f in ("INSTALIRAJ_SVE.bat", "DEINSTALIRAJ_SVE.bat", "instaliraj_sve.ps1"):
        shutil.copy(win / f, stage / f)
    shutil.copy(ROOT / "docs" / "UPUTSTVO.md", stage / "UPUTSTVO.md")
    readme = (
        f"VANJSKO UREDJENJE PRO {v} - instalacija\n"
        "=========================================\n\n"
        "NAJLAKSE (AutoCAD i SketchUp odjednom):\n"
        "  1. Raspakujte CIJELI zip (npr. na Desktop).\n"
        "  2. Dvostruki klik na INSTALIRAJ_SVE.bat\n"
        "     (ako su AutoCAD ili SketchUp otvoreni, skripta ce traziti da ih zatvorite).\n"
        "  3. Pokrenite AutoCAD: kartica VANJSKO UREDJENJE PRO -> Pomoc -> Brzi pocetak.\n\n"
        "Nove verzije stizu same (AutoCAD: Pomoc -> Napredno -> Azuriranja; SketchUp: Napredno -> Provjeri azuriranja).\n"
        "Uklanjanje oba dodatka: DEINSTALIRAJ_SVE.bat\n\n"
        "Samo AutoCAD: folder AutoCAD -> INSTALIRAJ.bat\n"
        "Samo SketchUp: Extensions > Extension Manager > Install Extension > SketchUp\\pb_site_design_pro.rbz\n"
    )
    (stage / "PROCITAJ.txt").write_bytes(readme.replace("\n", "\r\n").encode("utf-8"))
    installer = f"VanjskoUredjenjePRO-{v}.zip"
    zip_dir(stage, out / installer, "VanjskoUredjenjePRO")
    shutil.rmtree(out / "_stage")

    base = f"https://github.com/{REPO}/releases/download/v{v}/"
    items = notes(v)
    latest = {
        "version": v,
        "date": date.today().isoformat(),
        "notes": items,
        "installer": {"url": base + installer, "sha256": sha256(out / installer)},
        "autocad": {"url": base + "SiteDesignPro.bundle.zip", "sha256": sha256(out / "SiteDesignPro.bundle.zip")},
        "sketchup": {"url": base + "pb_site_design_pro.rbz", "sha256": sha256(out / "pb_site_design_pro.rbz")},
    }
    (out / "latest.json").write_text(json.dumps(latest, ensure_ascii=False, indent=2), encoding="utf-8")
    body = "\n".join("* " + i for i in items) or "Nova verzija."
    (out / "notes.md").write_text(
        f"{body}\n\n**Instalacija:** `{installer}` → raspakujte → `INSTALIRAJ_SVE.bat`. Instalirane verzije se ažuriraju same.\n",
        encoding="utf-8")
    print(v)


if __name__ == "__main__":
    main()
