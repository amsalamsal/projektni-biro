"""Writes docs/INVENTAR_MODELA.md from the shipped library (Assets/inventory.json and library.json).

Usage: python3 tools/assets/gen_inventory_doc.py
"""
import json
import os
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ASSETS = os.path.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'Assets')
inv = json.load(open(os.path.join(ASSETS, 'inventory.json'), encoding='utf-8'))
lib = json.load(open(os.path.join(ASSETS, 'library.json'), encoding='utf-8'))
by_id = {a['assetId']: a for a in lib['assets']}
KIND = {'custom': 'vlastiti detaljni model', 'parametric': 'parametarska površina/trasa s teksturom', 'generic': 'GENERIČKI prikaz'}

objects = [o for c in inv['categories'] for o in c['objects']]
asset_status = Counter(a['statusBs'] for a in lib['assets'])
kinds = Counter(a['model']['kind'] for a in lib['assets'])
out = []
w = out.append
w('# Inventar 3D modela')
w('')
w('Generisano iz biblioteke proširenja (`Assets/inventory.json`, `Assets/library.json`) alatom')
w('`tools/assets/gen_inventory_doc.py` – ne uređivati ručno.')
w('')
w('## Sažetak')
w('')
w(f'* Objekata iz specifikacije: **{len(objects)}** u **{len(inv["categories"])}** kategorija.')
w(f'* Modela u biblioteci: **{len(lib["assets"])}** (jedan model po tipu elementa; ID modela = ID tipa elementa).')
w('* Veza 2D ↔ 3D: svaki AutoCAD element (blok simbola, polilinija, šrafura) nosi ID tipa u svojim podacima (`SDP_ELEMENT`),'
  ' paket ga prenosi u SketchUp, a SketchUp gradi model s istim ID-om. Nedostajući model se prijavljuje, nikad ne zamjenjuje drugim.')
w(f'* Vrsta modela: vlastiti detaljni {kinds.get("custom", 0)}, parametarski s teksturom {kinds.get("parametric", 0)}, generički {kinds.get("generic", 0)}.')
w('* Statusi modela: ' + ', '.join(f'{k} {v}' for k, v in sorted(asset_status.items())) + '.')
w(f'* Objekata bez modela: **{sum(1 for o in objects if any(a["assetId"] not in by_id for a in o["assets"]))}**.')
w('')
w('### Statusi (od početka do kraja)')
w('')
w('| Status | Značenje |')
w('|---|---|')
for s, d in [('Nije započeto (Not started)', 'model ne postoji'), ('Traži se model (Searching for model)', 'traži se vanjski model'),
             ('Model pronađen (Model found)', 'vanjski model pronađen, licenca nije provjerena'), ('Model preuzet (Model acquired)', 'datoteka preuzeta'),
             ('Napravljen vlastiti model (Custom model created)', 'model napravljen u projektu'), ('Licenca provjerena (License verified)', 'licenca dozvoljava upotrebu i distribuciju'),
             ('Uvezeno u biblioteku (Imported into library)', 'model, pregled i metapodaci su u biblioteci i prošli su automatske provjere'),
             ('Testirano u SketchUpu (Tested in SketchUp)', 'umetnut i provjeren u stvarnom SketchUpu'), ('Završeno (Complete)', 'testiran u SketchUpu i odobren'),
             ('Blokirano (Blocked)', 'greška ili otvoreno pitanje – razlog je naveden')]:
    w(f'| {s} | {d} |')
w('')
w('**Iskreno stanje:** svi modeli su na statusu *Uvezeno u biblioteku*. Nijedan još nije *Testirano u SketchUpu* ni')
w('*Završeno*, jer u razvojnom okruženju nema SketchUpa. Automatske provjere koje su prošle za svaki model:')
w('zatvorena tijela (manifold) na tri nivoa detalja, dimenzije prema parametrima, ispravan COLLADA (metri, Z gore)')
w('koji nezavisni čitač (three.js ColladaLoader) učitava s istim dimenzijama, postojeće teksture i slika pregleda.')
w('Za status *Testirano u SketchUpu* svaki model treba umetnuti kroz *Biblioteka 3D modela*, promijeniti parametar,')
w('rotirati i zamijeniti, i provjeriti izgled; rezultat se upisuje u `IZVJESTAJ_TESTIRANJA.md`.')
w('')
w('Svi modeli su vlastiti rad (proceduralni generatori u `core/models/*.rb`, teksture iz `tools/assets/make_textures.py`);')
w('nijedan nije preuzet s interneta, pa licenca nije ograničenje (vidi `REGISTAR_LICENCI.md`).')
w('')
for c in inv['categories']:
    w(f'## {c["category"]} (`Assets/{c["folder"]}`)')
    w('')
    w('| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |')
    w('|---|---|---|---|---|')
    for o in c['objects']:
        first = True
        for a in o['assets']:
            m = by_id.get(a['assetId'])
            name = m['name'] if m else '—'
            kind = KIND.get(m['model']['kind'], m['model']['kind']) if m else 'NEDOSTAJE'
            status = a['statusBs'] if m else 'Nije započeto'
            w(f'| {o["name"] if first else ""} | `{a["assetId"]}` | {name} | {kind} | {status} |')
            first = False
    w('')
open(os.path.join(ROOT, 'docs', 'INVENTAR_MODELA.md'), 'w', encoding='utf-8').write('\n'.join(out))
print('docs/INVENTAR_MODELA.md:', len(objects), 'objekata,', len(lib['assets']), 'modela')
