"""Renders a still preview of the board using the SAME sprites, offsets and draw order as the Unity
CellView/LeaderView code (a script mock-up, not a Unity screenshot). Output: docs/preview_board.png"""
import json, math, os, random, sys
sys.path.insert(0, os.path.dirname(__file__))
from art_common import *

W, H, PXU = 1920, 1080, 112.0           # canvas px, pixels per world unit
man = {s["name"]: s for s in json.load(open(os.path.join(OUT, "manifest.json")))["sprites"]}
cache = {}
def load(n):
    if n not in cache: cache[n] = Image.open(os.path.join(OUT, n + ".png")).convert("RGBA")
    return cache[n]

def hash_(q, r, salt):
    h = (salt * 73856093) ^ (q * 19349663) ^ (r * 83492791)
    h &= 0xFFFFFFFF
    h ^= h >> 13; h = (h * 1274126177) & 0xFFFFFFFF; h ^= h >> 16
    return h & 0x7FFFFFFF

items = []   # (order, name, wx, wy, scale, tint(rgba floats))
def add(order, name, wx, wy, scale=1.0, tint=(1, 1, 1, 1)): items.append((order, name, wx, wy, scale, tint))

def to_world(q, r): return (SQ3 * (q + r / 2.0), -1.5 * r)
PL = [(0x4A, 0x86, 0xE8), (0xE0, 0x52, 0x4A), (0xF2, 0xB6, 0x3C), (0x3F, 0xBF, 0x7A)]
pc = lambda i: tuple(c / 255 for c in PL[i])
light = lambda c, t: tuple(c[k] + (1 - c[k]) * t for k in range(3))

R = 4
cells = [(q, r) for q in range(-R, R + 1) for r in range(max(-R, -q - R), min(R, -q + R) + 1)]
rng = random.Random(3)
owner = {c: -1 for c in cells}; garrison = {}
starts = [(4, 0), (-2, 4), (-4, 0)]          # three heroes
for i, s in enumerate(starts): owner[s] = i; garrison[s] = 2
def grow(i, n):
    cur = [starts[i]]
    for _ in range(n):
        c = rng.choice(cur)
        for d in [(1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1)]:
            nb = (c[0]+d[0], c[1]+d[1])
            if nb in owner and owner[nb] == -1: owner[nb] = i; garrison[nb] = rng.randint(1, 4); cur.append(nb); break
for i, n in enumerate([9, 7, 6]): grow(i, n)
deposits = {c: rng.randint(1, 3) for c in rng.sample(cells, 9)}
blocked = {(0, -1)}
highlight = {(-1, 0): "legal", (0, 1): "legal", (1, 1): "legal", (2, -1): "attack"}
salt = 4242
for (q, r) in cells:
    wx, wy = to_world(q, r); row = (r + 24) * 8
    add(0, "hex_shadow", wx + .05, wy - .22); add(1, "hex_side", wx, wy - .156)
    h = hash_(q, r, salt); roll = h % 100; decor = None; ds = .62
    if roll < 40: g = f"ground_grass_{(h // 100) % 3}"
    elif roll < 58: g = "ground_forest"; decor = "pine_a" if (h // 7) % 2 == 0 else "tree_b"
    elif roll < 68:
        g = "ground_sand"
        if (h // 13) % 3 == 0: decor, ds = "ruins", .5
    elif roll < 78: g = "ground_rock"; decor, ds = "rocks", .5
    else:
        g = f"ground_grass_{(h // 100) % 3}"
        if (h // 11) % 3 == 0: decor = "tree_a" if (h // 5) % 2 == 0 else "pine_b"
    isb = (q, r) in blocked
    tone = (.42, .38, .55, 1) if isb else (1, 1, 1, 1)
    add(2, g, wx, wy, 1, tone)
    o = owner[(q, r)]
    if o >= 0 and not isb:
        c = pc(o)
        add(3, "hex_fill", wx, wy, 1, (*c, .40)); add(4, "hex_ring", wx, wy, 1, (*light(c, .12), .95))
    if (q, r) in highlight:
        col = (1, .96, .62, .6) if highlight[(q, r)] == "legal" else (1, .32, .26, .65)
        add(5, "hex_glow", wx, wy, 1, col)
    if decor and not isb: add(200 + row, decor, wx - .30, wy + .20, ds)
    if isb: add(205 + row, "rubble", wx, wy - .05, .62)
    if (q, r) in deposits and not isb:
        add(201 + row, "ore", wx + .34, wy + .12, .36); add(602 + row, "coin", wx + .34, wy - .06, .30)
    if o >= 0 and not isb and garrison.get((q, r), 0) > 0:
        add(600 + row, "badge_face", wx, wy - .50, .74, (*light(pc(o), .05), 1)); add(601 + row, "badge_rim", wx, wy - .50, .74)
# heroes
for i, s in enumerate(starts):
    wx, wy = to_world(*s); row = int(round(-wy / 1.5 + 24)) * 8; b = 203 + row
    add(b, "pedestal", wx, wy + .06, .62); add(b + 1, "pedestal_glow", wx, wy + .06, .62, (*pc(i), .9))
    add(b + 2, "hero_body", wx - .06, wy - .10, .60); add(b + 2, "hero_pole", wx + .20, wy - .10, .60); add(b + 3, "hero_flag", wx + .20, wy - .10, .60, (*pc(i), 1))

# --- background water
bg = Image.new("RGBA", (W, H), (18, 48, 88, 255))
wa, wb = load("water_a"), load("water_b")
tile = int(1024 * 1.5 * PXU / 100 * 0.0 + 1024 * (PXU / 100.0) * 1.5)
wa2 = wa.resize((tile, tile), Image.LANCZOS); wb2 = wb.resize((tile, tile), Image.LANCZOS)
for ty in range(-1, H // tile + 2):
    for tx in range(-1, W // tile + 2):
        bg.alpha_composite(wa2, (tx * tile - 120, ty * tile - 60))
        bg.alpha_composite(wb2, (tx * tile + 90, ty * tile + 40))

def place(im_name, wx, wy, scale, tint):
    im = load(im_name); m = man[im_name]
    f = PXU / m["ppu"] * scale
    w, h = max(1, int(im.width * f)), max(1, int(im.height * f))
    im2 = im.resize((w, h), Image.LANCZOS)
    if tint != (1, 1, 1, 1):
        a = np.asarray(im2, np.float32) / 255
        a[..., 0] *= tint[0]; a[..., 1] *= tint[1]; a[..., 2] *= tint[2]; a[..., 3] *= tint[3]
        im2 = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA")
    cx, cy = W / 2 + wx * PXU, H / 2 - wy * PXU - 20
    x = cx - m["px"] * w; y = cy - (1 - m["py"]) * h
    bg.alpha_composite(im2, (int(x), int(y)))

for order, name, wx, wy, sc, tint in sorted(items, key=lambda t: t[0]):
    place(name, wx, wy, sc, tint)

# --- numbers on shields / deposits (title font)
d = ImageDraw.Draw(bg)
fnt = font("Philosopher-Bold.ttf", 26); fsm = font("Philosopher-Bold.ttf", 16)
for (q, r) in cells:
    wx, wy = to_world(q, r); cx, cy = W / 2 + wx * PXU, H / 2 - wy * PXU - 20
    if owner[(q, r)] >= 0 and (q, r) not in blocked and garrison.get((q, r), 0) > 0:
        t = str(garrison[(q, r)]); d.text((cx + 1.5, cy + .5 * PXU - 2), t, font=fnt, fill=(0, 0, 0, 170), anchor="mm")
        d.text((cx, cy + .5 * PXU - 4), t, font=fnt, fill=(255, 255, 255, 255), anchor="mm")
    if (q, r) in deposits and (q, r) not in blocked:
        d.text((cx + .34 * PXU, cy + .075 * PXU), str(deposits[(q, r)]), font=fsm, fill=(70, 38, 4, 255), anchor="mm")

# vignette
v = load("ui_vignette").resize((W, H), Image.BILINEAR); bg.alpha_composite(v)
os.makedirs(os.path.join(ROOT, "docs"), exist_ok=True)
bg.convert("RGB").save(os.path.join(ROOT, "docs", "preview_board.png"))
print("saved docs/preview_board.png")
