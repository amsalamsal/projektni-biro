#!/usr/bin/env python3
"""Assembles the distributable packages into dist/ from already-built outputs.

  dist/SiteDesignPro.bundle/           AutoCAD autoloader bundle (R23 = 2020, R24 = 2021-2024, R25 = 2025/2026, R26 = 2027)
  dist/SiteDesignPro.bundle.zip
  dist/pb_site_design_pro.rbz          SketchUp extension (install with Extension Manager > Install Extension)
  dist/samples/                        sample package, change file, schedules

Run tools/build_and_test.sh (or the dotnet build commands it lists) first.
"""
import html
import pathlib
import re
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


USER_DOCS = [
    ("index", "UPUTSTVO.md", "Uputstvo"),
    ("prirucnik", "PRIRUCNIK.md", "Priručnik (sve opcije)"),
    ("instalacija", "INSTALACIJA.md", "Instalacija i oporavak"),
    ("kompatibilnost", "KOMPATIBILNOST.md", "Kompatibilnost"),
    ("ogranicenja", "OGRANICENJA.md", "Ograničenja"),
    ("testiranje", "IZVJESTAJ_TESTIRANJA.md", "Izvještaj testiranja"),
    ("licence", "REGISTAR_LICENCI.md", "Registar licenci"),
]


def _inline(text: str) -> str:
    links = {md: page + ".html" for page, md, _ in USER_DOCS}
    out = []
    for i, part in enumerate(re.split(r"(`[^`]*`)", text)):
        if i % 2:
            out.append("<code>" + html.escape(part[1:-1]) + "</code>")
            continue
        t = html.escape(part, quote=False)
        t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
        t = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<em>\1</em>", t)
        t = re.sub(r"\[([^\]]+)\]\(([^)]+)\)",
                   lambda m: '<a href="%s">%s</a>' % (links.get(m.group(2), m.group(2)), m.group(1)), t)
        out.append(t)
    return "".join(out)


def markdown_to_html(md: str) -> str:
    """Small Markdown subset used by the user docs: headings, paragraphs, nested lists, tables, quotes, code blocks."""
    lines, out, i = md.splitlines(), [], 0
    while i < len(lines):
        line = lines[i]
        if line.startswith("```"):
            j = i + 1
            while j < len(lines) and not lines[j].startswith("```"):
                j += 1
            out.append("<pre>" + html.escape("\n".join(lines[i + 1:j])) + "</pre>")
            i = j + 1
        elif m := re.match(r"(#{1,4}) (.*)", line):
            n = len(m.group(1))
            slug = re.sub(r"[^\w\- ]", "", m.group(2).lower()).replace(" ", "-")  # GitHub-style anchor
            out.append('<h%d id="%s">%s</h%d>' % (n, html.escape(slug), _inline(m.group(2)), n))
            i += 1
        elif line.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].strip().strip("|").split("|")]
                if not all(re.fullmatch(r":?-+:?", c) for c in cells):
                    rows.append(cells)
                i += 1
            out.append("<table><thead><tr>" + "".join("<th>%s</th>" % _inline(c) for c in rows[0]) + "</tr></thead><tbody>" +
                       "".join("<tr>" + "".join("<td>%s</td>" % _inline(c) for c in r) + "</tr>" for r in rows[1:]) + "</tbody></table>")
        elif re.match(r"\s*(\*|\d+\.) ", line):
            stack = []  # (indent, tag) of open lists; every list has an open <li>
            while i < len(lines) and (re.match(r"\s*(\*|\d+\.) ", lines[i]) or (lines[i].startswith("  ") and lines[i].strip())):
                m = re.match(r"(\s*)(\*|\d+\.) (.*)", lines[i])
                i += 1
                if not m:  # continuation line of the open item
                    out[-1] += " " + _inline(lines[i - 1].strip())
                    continue
                ind, tag = len(m.group(1)), "ul" if m.group(2) == "*" else "ol"
                while stack and stack[-1][0] > ind:
                    out.append("</li></%s>" % stack.pop()[1])
                if stack and stack[-1][0] == ind:
                    out.append("</li>")
                else:
                    stack.append((ind, tag))
                    num = m.group(2)[:-1]
                    out.append("<ol start=\"%s\">" % num if tag == "ol" and num != "1" else "<%s>" % tag)
                out.append("<li>" + _inline(m.group(3)))
            while stack:
                out.append("</li></%s>" % stack.pop()[1])
        elif line.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].startswith(">"):
                quote.append(lines[i].lstrip("> ").rstrip())
                i += 1
            out.append("<blockquote><p>%s</p></blockquote>" % _inline(" ".join(quote)))
        elif line.strip():
            para = []
            while i < len(lines) and lines[i].strip() and not re.match(r"(#|\||>|```|\s*(\*|\d+\.) )", lines[i]):
                para.append(lines[i].strip())
                i += 1
            out.append("<p>%s</p>" % _inline(" ".join(para)))
        else:
            i += 1
    return "\n".join(out)


DOC_CSS = ("body{font-family:Segoe UI,Arial,sans-serif;max-width:60rem;margin:0 auto;padding:1rem 1.5rem;line-height:1.5;color:#1d2520;background:#fff}"
           "nav{border-bottom:1px solid #ccd5cf;padding-bottom:.5rem;margin-bottom:1rem}nav a{margin-right:1rem}"
           "table{border-collapse:collapse;margin:.75rem 0;font-size:.92rem}th,td{border:1px solid #ccd5cf;padding:.3rem .5rem;vertical-align:top;text-align:left}"
           "th{background:#eef3ef}code{background:#f1f3f2;padding:0 .2rem}pre{background:#f1f3f2;padding:.6rem;overflow:auto}"
           "blockquote{border-left:4px solid #3b7a57;margin:.75rem 0;padding:.25rem .75rem;background:#f4f8f5}h1{color:#2c5e43}")


def write_docs(target: pathlib.Path) -> None:
    """Bosnian user documentation as HTML (index.html is opened by SDPPOMOC) plus the Markdown sources."""
    target.mkdir(parents=True)
    nav = "<nav>" + "".join('<a href="%s.html">%s</a>' % (page, title) for page, _, title in USER_DOCS) + "</nav>"
    for page, md, title in USER_DOCS:
        src = ROOT / "docs" / md
        shutil.copy2(src, target / md)
        body = markdown_to_html(src.read_text(encoding="utf-8"))
        (target / (page + ".html")).write_text(
            "<!DOCTYPE html><html lang='bs'><head><meta charset='utf-8'><title>Vanjsko uređenje PRO – %s</title>"
            "<style>%s</style></head><body>%s%s</body></html>" % (title, DOC_CSS, nav, body), encoding="utf-8")


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
    write_docs(contents / "Docs")
    zip_dir(bundle, DIST / "SiteDesignPro.bundle.zip", "SiteDesignPro.bundle/")
    return bundle


README_INSTALL = """Vanjsko uredjenje PRO za AutoCAD 2020-2027 - instalacija
=========================================================

1. Zatvorite AutoCAD.
2. Raspakujte CIJELI ovaj zip u neki folder (npr. Desktop). Ne pokrecite iz samog zip-a.
3. Dvostruki klik na INSTALIRAJ.bat
   - skripta pronalazi instalirane verzije AutoCAD-a (2020-2027, i Civil 3D/Map 3D na istoj osnovi),
   - kopira SiteDesignPro.bundle u %APPDATA%\\Autodesk\\ApplicationPlugins,
   - uklanja Windows blokadu preuzetih datoteka i stare verzije dodatka,
   - dodaje folder u pouzdane putanje AutoCAD-a (TRUSTEDPATHS).
4. Pokrenite AutoCAD: pojavljuje se kartica VANJSKO UREDJENJE PRO. Provjera: SDPABOUT, pa SDPSAMPLE.

Ostalo:
- INSTALIRAJ_ZA_SVE_KORISNIKE.bat - instalacija u %PROGRAMDATA% (trazi administratorska prava).
- DEINSTALIRAJ.bat - uklanja dodatak (crtezi zadrzavaju geometriju i podatke).
- Ako Windows SmartScreen upozori: "More info" -> "Run anyway".
- Ako AutoCAD pri prvom pokretanju pita za sigurnost (SECURELOAD): odaberite "Always load".
- Dnevnik instalacije: %TEMP%\\VanjskoUredjenjePRO-instalacija.log

Dodatak jos nije testiran u stvarnom AutoCAD-u. Ako nesto ne radi, posaljite verziju AutoCAD-a,
tekst greske iz komandne linije i dnevnike (%LOCALAPPDATA%\\SiteDesignPro\\logs).
"""


def autocad_installer(bundle: pathlib.Path) -> pathlib.Path:
    """Zip for end users: the bundle plus the Windows installer scripts (detects the installed AutoCAD versions)."""
    target = DIST / "VanjskoUredjenjePRO-AutoCAD-instalacija.zip"
    win = ROOT / "tools" / "windows"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for name in ("INSTALIRAJ.bat", "INSTALIRAJ_ZA_SVE_KORISNIKE.bat", "DEINSTALIRAJ.bat", "instaliraj.ps1"):
            data = (win / name).read_bytes().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")  # CRLF for cmd.exe
            z.writestr(name, data)
        z.writestr("PROCITAJ.txt", README_INSTALL.replace("\n", "\r\n"))
        for f in sorted(bundle.rglob("*")):
            if f.is_file():
                z.write(f, "SiteDesignPro.bundle/" + f.relative_to(bundle).as_posix())
    return target


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
    bundle = autocad_bundle()
    print("AutoCAD bundle:", bundle)
    print("AutoCAD setup: ", autocad_installer(bundle))
    print("SketchUp rbz:  ", sketchup_rbz())
    samples()
    print("Samples:        ", DIST / "samples")
