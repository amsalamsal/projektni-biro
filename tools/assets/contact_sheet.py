"""Montage of asset previews for one library folder (visual review). Usage: contact_sheet.py <library> <Folder> <out.png> [cols]"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

lib, folder, out = sys.argv[1], sys.argv[2], sys.argv[3]
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 7
base = os.path.join(lib, folder)
items = sorted(d for d in os.listdir(base) if os.path.exists(os.path.join(base, d, 'preview.png')))
tile, cap = 260, 40
rows = (len(items) + cols - 1) // cols
sheet = Image.new('RGB', (cols * tile, rows * (tile + cap)), (255, 255, 255))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype('DejaVuSans.ttf', 12)
except OSError:
    font = ImageFont.load_default()
for i, d in enumerate(items):
    a = json.load(open(os.path.join(base, d, 'asset.json'), encoding='utf-8'))
    im = Image.open(os.path.join(base, d, 'preview.png')).convert('RGB').resize((tile, tile))
    x, y = (i % cols) * tile, (i // cols) * (tile + cap)
    sheet.paste(im, (x, y))
    mark = '' if a['model']['kind'] == 'custom' else '  [GENERIČKI]'
    draw.text((x + 4, y + tile + 2), a['name'][:34], fill=(0, 0, 0), font=font)
    draw.text((x + 4, y + tile + 18), (d + mark)[:40], fill=(150, 0, 0) if mark else (90, 90, 90), font=font)
sheet.save(out)
print(out, len(items))
