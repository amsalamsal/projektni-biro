"""Procedural, seamless material textures for the SketchUp library (own work, no third-party content).

Writes <out>/<material>.png (512 x 512) and updates shared/catalog/materials.json with "texture" and
"textureSize" (real size of one tile in metres). Base colours come from the catalog so a textured material
keeps its catalog colour on average.

Usage: python3 tools/assets/make_textures.py [out-dir]
"""
import json
import os
import re
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'textures')
CAT = os.path.join(ROOT, 'shared', 'catalog', 'materials.json')
N = 512
rng = np.random.default_rng(20261030)


def noise(scale, octaves=4, persistence=0.5):
    """Seamless fractal noise in [0, 1] (periodic spectral synthesis)."""
    total = np.zeros((N, N))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        f = scale * (2 ** o)
        spec = np.fft.fft2(rng.standard_normal((N, N)))
        fy = np.fft.fftfreq(N)[:, None] * N
        fx = np.fft.fftfreq(N)[None, :] * N
        r = np.sqrt(fx ** 2 + fy ** 2)
        band = np.exp(-((r - f) ** 2) / (2 * (f * 0.5 + 0.5) ** 2))
        layer = np.real(np.fft.ifft2(spec * band))
        layer = (layer - layer.mean()) / (layer.std() + 1e-9)
        total += amp * layer
        norm += amp
        amp *= persistence
    total /= norm
    return np.clip(0.5 + total * 0.18, 0, 1)


def tint(base, field, contrast=0.35):
    """Colour image from a catalog base colour modulated by a field in [0, 1] (mean kept near base)."""
    b = np.array(base, dtype=float)[None, None, :]
    f = (field - field.mean())[..., None]
    return np.clip(b * (1 + f * contrast * 2), 0, 255)


def grid_joints(cw, ch, joint, offset_rows=0.0):
    """Mask of joints for a running-bond grid of cells cw x ch pixels (offset every other row)."""
    y, x = np.mgrid[0:N, 0:N]
    row = (y // ch)
    xs = (x + (row % 2) * offset_rows * cw) % cw
    ys = y % ch
    return ((xs < joint) | (ys < joint)).astype(float), row, ((x + (row % 2) * offset_rows * cw) // cw)


def cell_shade(rows, cols, spread):
    """Per-cell brightness variation (paving units are never identical)."""
    ids = (rows.astype(np.int64) * 7919 + cols.astype(np.int64) * 104729) % 9973
    vals = np.random.default_rng(7).uniform(-spread, spread, 9973)
    return vals[ids]


def voronoi(points_per_side, jitter=0.9):
    """Seamless Voronoi on a jittered grid: (cell id, distance to edge) for stones and flags."""
    k = points_per_side
    cell = N / k
    off = (rng.random((k, k, 2)) - 0.5) * jitter
    y, x = np.mgrid[0:N, 0:N].astype(float)
    gx = np.floor(x / cell).astype(int)
    gy = np.floor(y / cell).astype(int)
    best = np.full((N, N), 1e18)
    second = np.full((N, N), 1e18)
    owner = np.zeros((N, N), dtype=int)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx, cy = gx + dx, gy + dy
            wx, wy = cx % k, cy % k
            px = (cx + 0.5 + off[wy, wx, 0]) * cell
            py = (cy + 0.5 + off[wy, wx, 1]) * cell
            d = (x - px) ** 2 + (y - py) ** 2
            closer = d < best
            second = np.where(closer, best, np.minimum(second, d))
            owner = np.where(closer, wy * k + wx, owner)
            best = np.where(closer, d, best)
    return owner, np.sqrt(second) - np.sqrt(best)


def save(key, img, size_m):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(os.path.join(OUT, key + '.jpg'), quality=86, optimize=True)
    SIZES[key] = size_m


SIZES = {}
mats = json.load(open(CAT, encoding='utf-8'))['materials']
C = {m['key']: m['color'] for m in mats}
grain = lambda s=24: noise(s, 3)


def paving(key, cw_m, ch_m, joint_m, tile_m, bond=0.5, spread=0.08, joint_col=None, base=None):
    px = N / tile_m
    mask, rows, cols = grid_joints(int(cw_m * px), int(ch_m * px), max(int(joint_m * px), 2), bond)
    shade = cell_shade(rows, cols, spread)
    img = tint(base or C[key], grain(40) + shade[...] * 0.6, 0.35)
    jc = np.array(joint_col or [int(c * 0.55) for c in (base or C[key])], dtype=float)
    img = img * (1 - mask[..., None]) + jc[None, None, :] * mask[..., None]
    save(key, img, tile_m)


paving('concrete_paver', 0.2, 0.1, 0.006, 0.8)
paving('permeable_paver', 0.2, 0.2, 0.025, 0.8, bond=0.0, joint_col=[150, 140, 120])
paving('concrete_slab', 0.6, 0.6, 0.006, 1.2, bond=0.0, spread=0.05)
paving('porcelain', 0.6, 0.6, 0.004, 1.2, bond=0.0, spread=0.03)
paving('brick', 0.25, 0.065, 0.01, 1.0, joint_col=[190, 184, 172])

# natural stone flags (crazy paving)
owner, edge = voronoi(6, 0.95)
shade = np.random.default_rng(3).uniform(-0.18, 0.18, owner.max() + 1)[owner]
img = tint(C['natural_stone'], grain(30) + shade, 0.4)
img = np.where((edge < 4)[..., None], np.array([110, 104, 96])[None, None, :], img)
save('natural_stone', img, 1.5)

# stone masonry (irregular blocks)
owner, edge = voronoi(9, 0.6)
shade = np.random.default_rng(4).uniform(-0.2, 0.2, owner.max() + 1)[owner]
img = tint(C['stone_wall'], grain(35) + shade, 0.45)
img = np.where((edge < 5)[..., None], np.array([150, 146, 138])[None, None, :], img)
save('stone_wall', img, 1.2)

# gabion: stones behind a 10 cm welded mesh
owner, edge = voronoi(14, 0.9)
shade = np.random.default_rng(5).uniform(-0.25, 0.25, owner.max() + 1)[owner]
img = tint(C['gabion'], grain(40) + shade, 0.5)
img = np.where((edge < 3)[..., None], np.array([60, 58, 54])[None, None, :], img)
y, x = np.mgrid[0:N, 0:N]
wire = ((x % 51) < 3) | ((y % 51) < 3)
img = np.where(wire[..., None], np.array([170, 174, 178])[None, None, :], img)
save('gabion', img, 1.0)

# gravel: pebbles
owner, edge = voronoi(40, 1.0)
shade = np.random.default_rng(6).uniform(-0.3, 0.3, owner.max() + 1)[owner]
img = tint(C['gravel'], shade + noise(60, 2) * 0.3, 0.5)
img = np.where((edge < 3)[..., None], img * 0.55, img)
save('gravel', img, 0.5)

# rubber EPDM: base plus coloured granules
img = tint(C['rubber'], noise(120, 2), 0.25)
for col, share in (([40, 40, 40], 0.18), ([200, 200, 190], 0.05), ([60, 120, 60], 0.05)):
    m = np.random.default_rng(len(col) + int(share * 100)).random((N, N)) < share
    img = np.where(m[..., None], np.array(col)[None, None, :], img)
save('rubber', img, 0.5)

# tactile: truncated domes on 0.3 m tiles
y, x = np.mgrid[0:N, 0:N]
cell = N / 2 / 6
cx = (x % cell) - cell / 2
cy = (y % cell) - cell / 2
dome = np.clip(1 - np.sqrt(cx ** 2 + cy ** 2) / (cell * 0.32), 0, 1)
mask, _, _ = grid_joints(N // 2, N // 2, 3, 0)
img = tint(C['tactile'], grain(20) * 0.5 + dome * 0.5, 0.5)
img = np.where((dome > 0)[..., None], img * (0.85 + 0.25 * dome[..., None]), img)
img = img * (1 - mask[..., None]) + np.array([90, 90, 80])[None, None, :] * mask[..., None]
save('tactile', img, 0.6)

# timber deck boards with grain
y, x = np.mgrid[0:N, 0:N]
board = N / (1.2 / 0.145)
row = (y // board).astype(int)
streak = noise(8, 3)
streak = np.roll(streak, 0, axis=0)
fib = np.sin((y + streak * 80) / 3.0) * 0.5 + 0.5
shade = np.random.default_rng(8).uniform(-0.12, 0.12, row.max() + 1)[row]
img = tint(C['timber_deck'], fib * 0.4 + grain(30) * 0.3 + shade, 0.45)
joint = (y % board) < 4
img = np.where(joint[..., None], img * 0.35, img)
ends = ((x + (row * 137) % N) % (N // 2)) < 2
img = np.where(ends[..., None], img * 0.5, img)
save('timber_deck', img, 1.2)

# timber (furniture, cladding): fine grain
fib = np.sin((y + noise(6, 3) * 160) / 7.0) * 0.5 + 0.5
save('timber', tint(C['timber'], fib * 0.7 + grain(40) * 0.2, 0.55), 0.6)
save('bark', tint(C['bark'], np.sin((x + noise(10, 3) * 140) / 4.0) * 0.5 + 0.5, 0.6), 0.4)

# lawns, meadows, roofs, soils
blades = noise(180, 2) * 0.6 + noise(25, 3) * 0.4
save('grass', tint(C['grass'], blades, 1.1), 1.0)
mead = tint(C['meadow'], noise(140, 2) * 0.6 + noise(20, 3) * 0.4, 0.5)
for col, share in (([236, 196, 48], 0.006), ([240, 238, 230], 0.008), ([128, 84, 168], 0.004)):
    m = np.random.default_rng(int(share * 1e4)).random((N, N)) < share
    mead = np.where(m[..., None], np.array(col)[None, None, :], mead)
save('meadow', mead, 1.0)
sed = tint(C['sedum'], noise(90, 3), 0.5)
m = np.random.default_rng(11).random((N, N)) < 0.03
save('sedum', np.where(m[..., None], np.array([170, 70, 60])[None, None, :], sed), 0.6)
owner, edge = voronoi(48, 1.0)
chips = np.random.default_rng(13).uniform(-0.4, 0.4, owner.max() + 1)[owner]
save('mulch', np.where((edge < 2)[..., None], tint(C['mulch'], chips, 0.5) * 0.5, tint(C['mulch'], chips + noise(40, 2) * 0.3, 0.5)), 0.8)
for key, s in (('soil', 30), ('terrain', 20), ('sand', 120)):
    save(key, tint(C[key], noise(s, 4), 0.5 if key != 'sand' else 0.25), 0.8)
save('grass_grid', np.where(grid_joints(N // 4, N // 4, 14, 0)[0][..., None] > 0, tint(C['concrete'], grain(30), 0.2), tint(C['grass'], blades, 0.5)), 0.6)
save('asphalt', tint(C['asphalt'], noise(160, 2) * 0.7 + noise(20, 3) * 0.3, 0.6), 1.0)
save('asphalt_red', tint(C['asphalt_red'], noise(160, 2) * 0.7 + noise(20, 3) * 0.3, 0.5), 1.0)
save('acrylic_court', tint(C['acrylic_court'], noise(60, 3), 0.15), 1.0)
for key in ('concrete', 'concrete_precast'):
    pores = (np.random.default_rng(12).random((N, N)) < 0.004).astype(float)
    save(key, tint(C[key], noise(25, 4) - pores * 0.8, 0.3), 1.0)

# foliage (leaf texture so crowns read as leaves, not plastic)
for key in ('foliage', 'foliage_evergreen', 'foliage_fruit', 'foliage_dark', 'foliage_light', 'planting_mixed', 'planting_flowers'):
    if key not in C:
        continue
    owner, edge = voronoi(26, 1.0)
    shade = np.random.default_rng(len(key)).uniform(-0.35, 0.35, owner.max() + 1)[owner]
    leaf = tint(C[key], shade + noise(30, 2) * 0.4, 0.5)
    leaf = np.where((edge < 2)[..., None], leaf * 0.6, leaf)
    save(key, leaf, 0.5)

# pool mosaic and water
mask, rows, cols = grid_joints(N // 24, N // 24, 2, 0)
shade = cell_shade(rows, cols, 0.12)
img = tint(C['pool_finish'], shade + 0.5, 0.4)
save('pool_finish', img * (1 - mask[..., None]) + np.array([230, 236, 238])[None, None, :] * mask[..., None], 0.6)
save('water', tint(C['water'], noise(10, 4) * 0.7 + noise(60, 2) * 0.3, 0.35), 2.0)
save('solar_pv', np.where(grid_joints(N // 6, N // 10, 3, 0)[0][..., None] > 0, np.array([200, 205, 210])[None, None, :], tint(C['solar_pv'], noise(80, 2), 0.2)), 1.0)

# update catalog (keep the one-entry-per-line formatting)
text = open(CAT, encoding='utf-8').read()
for key, size in SIZES.items():
    pat = re.compile(r'(\{ "key": "' + re.escape(key) + r'",[^\n]*?)(, "texture": "[^"]*", "textureSize": \[[^\]]*\])?( \},?)')
    rep = lambda m: m.group(1) + f', "texture": "{key}.jpg", "textureSize": [{size}, {size}]' + m.group(3)
    text, n = pat.subn(rep, text, count=1)
    if n != 1:
        print('materials.json: not found', key)
open(CAT, 'w', encoding='utf-8').write(text)
print(f'{len(SIZES)} tekstura u {OUT}')
