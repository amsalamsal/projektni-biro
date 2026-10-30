"""Fails when a class with [CommandMethod] is not registered with [assembly: CommandClass] in Plugin.cs.

AutoCAD scans only the listed classes once any CommandClass attribute exists, so an unlisted class makes all its
commands "Unknown command". Usage: python3 tools/check_commands.py
"""
import pathlib
import re
import sys

src = pathlib.Path(__file__).resolve().parents[1] / 'autocad-site-design-pro' / 'src' / 'SiteDesignPro.AutoCAD'
registered = set(re.findall(r'CommandClass\(typeof\((\w+)\)\)', (src / 'Plugin.cs').read_text(encoding='utf-8')))
missing = []
count = 0
for f in src.rglob('*.cs'):
    if '/obj/' in f.as_posix() or '/bin/' in f.as_posix():
        continue
    text = f.read_text(encoding='utf-8')
    for cls in re.findall(r'class\s+(\w+)', text):
        body = text.split('class ' + cls, 1)[1]
        nxt = re.search(r'\n\s*(public|internal)?\s*(sealed\s+|static\s+)*class\s+\w+', body)
        body = body[:nxt.start()] if nxt else body
        n = len(re.findall(r'\[CommandMethod\(', body))
        count += n
        if n and cls not in registered:
            missing.append(f'{cls} ({f.name}, {n} naredbi)')
if missing:
    print('Klase s naredbama koje nisu registrovane u Plugin.cs:', ', '.join(missing))
    sys.exit(1)
print(f'AutoCAD naredbe: {count}, sve klase registrovane ({len(registered)})')
