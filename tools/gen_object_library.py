#!/usr/bin/env python3
"""Generates docs/OBJECT_LIBRARY.md from shared/catalog (run after catalog changes)."""
import json, os, collections
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
cat = lambda n: json.load(open(os.path.join(root, 'shared', 'catalog', n), encoding='utf-8'))
cats = cat('categories.json')['categories']
types = cat('element-types.json')['types']
builders = cat('builders.json')['builders']
by_cat = collections.defaultdict(list)
for t in types:
    by_cat[t['category']].append(t)
out = ['# Object library', '',
       'Generated from `shared/catalog` by `tools/gen_object_library.py` - do not edit by hand.', '',
       f'{len(types)} element types, {len(builders)} parametric builders. '
       'Types marked **review** carry values that must be confirmed against local regulations or a specialist design '
       '(they are reported as review items by validation).', '']
for c in cats:
    ts = by_cat.get(c['id'], [])
    if not ts:
        continue
    out += [f"## {c['name']} ({len(ts)})", '', '| Type id | Name | Subcategory | Builder | Geometry | Layer |', '|---|---|---|---|---|---|']
    for t in ts:
        b = builders[t['builder']]
        name = t['name'] + (' **review**' if t.get('reviewRequired') else '')
        out.append(f"| `{t['id']}` | {name} | {t.get('subcategory', '')} | {t['builder']} | {b['geometry']} | {t.get('layer', '')} |")
    out.append('')
out += ['## Builders', '', '| Builder | Geometry | Parameters |', '|---|---|---|']
for k, b in sorted(builders.items()):
    params = ', '.join(p['key'] + (f" ({p['unit']})" if p.get('unit') else '') for p in b['parameters'])
    out.append(f"| {k} | {b['geometry']} | {params} |")
open(os.path.join(root, 'docs', 'OBJECT_LIBRARY.md'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print(f'docs/OBJECT_LIBRARY.md: {len(types)} types')
