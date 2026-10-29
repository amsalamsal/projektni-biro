#!/usr/bin/env python3
"""Assembles the distributable packages into dist/ from already-built outputs.

  dist/SiteDesignPro.bundle/           AutoCAD autoloader bundle (R23 = 2020, R24 = 2021-2024, R25 = 2025/2026, R26 = 2027)
  dist/SiteDesignPro.bundle.zip
  dist/pb_site_design_pro.rbz          SketchUp extension (install with Extension Manager > Install Extension)
  dist/samples/                        sample package, change file, schedules

Run tools/build_and_test.sh (or the dotnet build commands it lists) first.
"""
import pathlib
import shutil
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
DIST = ROOT / "dist"
ACAD = ROOT / "autocad-site-design-pro"
SU = ROOT / "sketchup-site-design-pro"
CONFIG = sys.argv[1] if len(sys.argv) > 1 else "Release"


def zip_dir(src: pathlib.Path, target: pathlib.Path, prefix: str = "") -> None:
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(src.rglob("*")):
            if f.is_file():
                z.write(f, prefix + f.relative_to(src).as_posix())


def autocad_bundle() -> pathlib.Path:
    bundle = DIST / "SiteDesignPro.bundle"
    if bundle.exists():
        shutil.rmtree(bundle)
    contents = bundle / "Contents"
    for series, tfm in (("R23", "net47"), ("R24", "net48"), ("R25", "net8.0-windows"), ("R26", "net10.0-windows")):
        out = ACAD / "src" / "SiteDesignPro.AutoCAD" / "bin" / CONFIG / tfm
        if not (out / "SiteDesignPro.AutoCAD.dll").exists():
            sys.exit(f"missing build output {out}; build with: dotnet build -c {CONFIG} autocad-site-design-pro/src/SiteDesignPro.AutoCAD")
        target = contents / "Win64" / series
        target.mkdir(parents=True)
        for name in ("SiteDesignPro.AutoCAD.dll", "SiteDesignPro.Core.dll", "SiteDesignPro.AutoCAD.deps.json", "SiteDesignPro.AutoCAD.pdb", "SiteDesignPro.Core.pdb"):
            if (out / name).exists():
                shutil.copy2(out / name, target / name)
        if tfm.startswith("net4"):
            # .NET Framework: ship the System.Text.Json dependency chain next to the plug-in
            # (loaded by DependencyResolver; AutoCAD's own copies win when present).
            core_out = ACAD / "src" / "SiteDesignPro.Core" / "bin" / CONFIG / "net47"
            for dep in sorted(core_out.glob("*.dll")):
                if dep.name != "SiteDesignPro.Core.dll":
                    shutil.copy2(dep, target / dep.name)
    shutil.copy2(ACAD / "build" / "PackageContents.xml", bundle / "PackageContents.xml")
    shutil.copytree(ROOT / "shared" / "catalog", bundle / "Resources" / "catalog")
    shutil.copytree(ROOT / "shared" / "schema", bundle / "Resources" / "schema")
    docs = contents / "Docs"
    docs.mkdir(parents=True)
    for doc in ("INSTALL.md", "USER_GUIDE.md", "KNOWN_LIMITATIONS.md"):
        if (ROOT / "docs" / doc).exists():
            shutil.copy2(ROOT / "docs" / doc, docs / doc)
    (docs / "README.html").write_text(
        "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Site Design Pro</title></head><body>"
        "<h1>Site Design Pro for AutoCAD</h1><p>See USER_GUIDE.md, INSTALL.md and KNOWN_LIMITATIONS.md in this folder. "
        "Start with SDPNEW or SDPSAMPLE; the SITE DESIGN PRO ribbon tab lists every command.</p></body></html>",
        encoding="utf-8")
    zip_dir(bundle, DIST / "SiteDesignPro.bundle.zip", "SiteDesignPro.bundle/")
    return bundle


def sketchup_rbz() -> pathlib.Path:
    staging = DIST / "rbz-staging"
    if staging.exists():
        shutil.rmtree(staging)
    shutil.copytree(SU / "src", staging)
    catalog = staging / "pb_site_design_pro" / "catalog"
    if catalog.exists():
        shutil.rmtree(catalog)
    shutil.copytree(ROOT / "shared" / "catalog", catalog)
    target = DIST / "pb_site_design_pro.rbz"
    zip_dir(staging, target)
    shutil.rmtree(staging)
    return target


def samples() -> None:
    out = DIST / "samples"
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    for f in (ROOT / "shared" / "samples").iterdir():
        if f.is_file():
            shutil.copy2(f, out / f.name)


if __name__ == "__main__":
    DIST.mkdir(exist_ok=True)
    print("AutoCAD bundle:", autocad_bundle())
    print("SketchUp rbz:  ", sketchup_rbz())
    samples()
    print("Samples:        ", DIST / "samples")
