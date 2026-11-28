from art_common import *

rngd = np.random.default_rng(42)


def blob(cv, cx, cy, rx, ry, c_light, c_dark, light=(-0.5, -0.8), outline=True):
    h, w = cv.shape[:2]
    m = ellipse_mask(cx, cy, rx, ry, (w, h))
    col = shade_grad((w, h), np.asarray(c_light, np.float32), np.asarray(c_dark, np.float32), light,
                     lo=-max(rx, ry), hi=max(rx, ry))
    if outline:
        edge = np.clip((m - ellipse_mask(cx, cy, rx - 2.5, ry - 2.5, (w, h))), 0, 1)
        col = col * (1 - 0.35 * edge[..., None])
    paint(cv, m, col)


def poly(cv, pts, c_light, c_dark, light=(-0.5, -0.8), outline=0.0):
    h, w = cv.shape[:2]
    m = poly_mask(pts, (w, h))
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    span = max(max(xs) - min(xs), max(ys) - min(ys)) / 2
    col = shade_grad((w, h), np.asarray(c_light, np.float32), np.asarray(c_dark, np.float32), light, lo=-span, hi=span)
    # shift gradient centre onto the polygon
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = ((xx - cx) * light[0] + (yy - cy) * light[1] - (-span)) / (2 * span)
    t = np.clip(t, 0, 1)[..., None]
    col = np.asarray(c_dark, np.float32) + (np.asarray(c_light, np.float32) - np.asarray(c_dark, np.float32)) * t
    if outline:
        from scipy import ndimage as ndi
        er = ndi.minimum_filter(m, size=5)
        edge = np.clip(m - er, 0, 1)
        col = col * (1 - outline * edge[..., None])
    paint(cv, m, col)


def line(cv, pts, color, width, opacity=1.0):
    h, w = cv.shape[:2]
    im = Image.new("L", (w * 4, h * 4), 0)
    ImageDraw.Draw(im).line([(x * 4, y * 4) for x, y in pts], fill=255, width=int(width * 4), joint="curve")
    paint(cv, np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255, np.asarray(color, np.float32), opacity)


G = hexcolor


def tree_round(w=300, h=340, n=1, seed=1):
    r = np.random.default_rng(seed)
    cv = blank(h, w)
    spots = [(150, 250, 1.0)] if n == 1 else [(95, 245, 0.8), (205, 240, 0.78), (150, 262, 1.0)]
    for cx, base, s in sorted(spots, key=lambda t: t[1]):
        soft_shadow(cv, cx + 14, base + 6, 66 * s, 17 * s, 0.38)
    for cx, base, s in sorted(spots, key=lambda t: t[1]):
        poly(cv, [(cx - 11 * s, base), (cx - 7 * s, base - 70 * s), (cx + 7 * s, base - 70 * s), (cx + 12 * s, base)],
             G("#8a6a45"), G("#4d3724"))
        for dx, dy, rr, a, b in [(-26, -88, 44, "#5da04a", "#2f6a36"), (28, -92, 42, "#5ea64b", "#2f6a36"),
                                 (0, -128, 50, "#79bd5c", "#3b7b3b"), (-8, -98, 46, "#6cb255", "#34733a")]:
            blob(cv, cx + dx * s, base + dy * s, rr * s, rr * 0.92 * s, G(b if False else a), G(b))
        for _ in range(7):                      # leaf highlights
            hx, hy = cx + r.uniform(-34, 24) * s, base + r.uniform(-150, -85) * s
            blob(cv, hx, hy, 8 * s, 6 * s, G("#c6ee8a"), G("#9bd36b"), outline=False)
    return cv


def tree_pine(w=260, h=360, n=2, seed=2):
    cv = blank(h, w)
    spots = [(95, 285, 0.85), (175, 270, 0.95)] if n == 2 else [(130, 280, 1.1)]
    for cx, base, s in sorted(spots, key=lambda t: t[1]):
        soft_shadow(cv, cx + 14, base + 4, 50 * s, 13 * s, 0.38)
    for cx, base, s in sorted(spots, key=lambda t: t[1]):
        poly(cv, [(cx - 8 * s, base), (cx - 6 * s, base - 40 * s), (cx + 6 * s, base - 40 * s), (cx + 8 * s, base)],
             G("#7d5c3c"), G("#442f1f"))
        for i, (wd, y0, hh) in enumerate([(70, 32, 82), (56, 82, 74), (40, 128, 66)]):
            top = base - y0 * s - hh * s
            poly(cv, [(cx, top), (cx + wd * s, base - y0 * s), (cx + wd * 0.35 * s, base - y0 * s + 10 * s),
                      (cx - wd * 0.35 * s, base - y0 * s + 10 * s), (cx - wd * s, base - y0 * s)],
                 G("#4c8d5a" if i != 2 else "#5aa266"), G("#1f4a3a"), outline=0.4)
        poly(cv, [(cx, base - 270 * s * 0.0 - 220 * s), (cx + 9 * s, base - 196 * s), (cx - 9 * s, base - 196 * s)],
             G("#e9f6ee"), G("#a9c8b6"))     # snow-dusted tip
    return cv


def rocks(w=300, h=240):
    cv = blank(h, w)
    soft_shadow(cv, 160, 200, 100, 22, 0.4)
    faces = [
        ([(70, 190), (60, 140), (95, 100), (150, 95), (172, 140), (165, 190)], "#c3beb4", "#6d6a68"),
        ([(150, 195), (160, 150), (200, 120), (245, 135), (255, 185), (230, 205)], "#b4afa6", "#5f5c5c"),
        ([(40, 200), (45, 168), (70, 150), (98, 165), (100, 200)], "#a9a49c", "#585655"),
    ]
    for pts, a, b in faces:
        poly(cv, pts, G(a), G(b), outline=0.5)
    line(cv, [(95, 100), (115, 140), (108, 185)], G("#3d3b3b"), 2.5, 0.5)
    line(cv, [(205, 125), (215, 160), (232, 200)], G("#3d3b3b"), 2.5, 0.5)
    blob(cv, 110, 100, 22, 8, G("#7fae58"), G("#4e7d3d"), outline=False)      # moss on top
    return cv


def ruins(w=320, h=330):
    cv = blank(h, w)
    soft_shadow(cv, 165, 290, 120, 24, 0.42)
    poly(cv, [(40, 285), (60, 262), (260, 262), (285, 285), (255, 300), (70, 300)], G("#c6bba3"), G("#7b705e"), outline=0.5)
    for cx, top, wd in [(95, 120, 38), (170, 70, 40), (240, 165, 36)]:
        base = 268
        poly(cv, [(cx - wd / 2, base), (cx - wd / 2 + 2, top + 12), (cx - wd / 2 + 10, top - 6), (cx + 2, top + 16),
                  (cx + wd / 2 - 6, top - 2), (cx + wd / 2, top + 14), (cx + wd / 2, base)],
             G("#e0d6bd"), G("#8d8369"), light=(-0.8, -0.4), outline=0.5)
        for k in (-0.25, 0.1, 0.35):
            line(cv, [(cx + k * wd, top + 30), (cx + k * wd, base - 6)], G("#6b6350"), 2, 0.35)
        poly(cv, [(cx - wd / 2 - 6, base - 2), (cx + wd / 2 + 6, base - 2), (cx + wd / 2 + 2, base - 18), (cx - wd / 2 - 2, base - 18)],
             G("#d6ccb2"), G("#7b715d"))
    poly(cv, [(110, 250), (150, 236), (170, 262), (120, 266)], G("#cfc4aa"), G("#7b705e"), outline=0.5)   # fallen drum
    for mx, my in [(98, 122), (172, 72), (150, 262), (240, 170)]:
        blob(cv, mx, my, 14, 5, G("#7fae58"), G("#4a7a3b"), outline=False)
    return cv


def ore(w=300, h=250):
    cv = blank(h, w)
    soft_shadow(cv, 155, 205, 95, 20, 0.4)
    for pts, a, b in [([(60, 200), (55, 150), (95, 110), (150, 105), (180, 150), (170, 200)], "#bdb6aa", "#615d5c"),
                      ([(150, 205), (165, 155), (215, 130), (250, 165), (245, 205)], "#a8a298", "#55524f")]:
        poly(cv, pts, G(a), G(b), outline=0.5)
    gold = [([(95, 150), (110, 118), (135, 125), (130, 160)], 0), ([(185, 180), (200, 148), (225, 160), (222, 190)], 0),
            ([(130, 190), (145, 165), (165, 175), (158, 198)], 0), ([(205, 150), (214, 132), (228, 140), (222, 155)], 0)]
    for pts, _ in gold:
        poly(cv, pts, G("#ffe27a"), G("#d08a1a"), outline=0.45)
        cx = sum(p[0] for p in pts) / 4; cy = sum(p[1] for p in pts) / 4
        blob(cv, cx - 5, cy - 6, 5, 3.5, G("#fff8cf"), G("#fff3b0"), outline=False)
    return cv


def rubble(w=320, h=280):
    cv = blank(h, w)
    soft_shadow(cv, 165, 232, 120, 26, 0.5)
    blob(cv, 165, 200, 120, 46, G("#46405a"), G("#1d1a2b"), outline=False)
    for pts in [[(60, 215), (70, 150), (115, 120), (160, 140), (165, 215)],
                [(150, 220), (170, 130), (215, 95), (262, 130), (270, 220)],
                [(100, 150), (130, 85), (175, 80), (200, 130), (190, 160)]]:
        poly(cv, pts, G("#5b5470"), G("#221f33"), outline=0.55)
    for pts in [[(112, 128), (124, 160), (118, 205)], [(220, 110), (212, 160), (236, 210)], [(150, 92), (160, 120)]]:
        line(cv, pts, G("#b57cff"), 6, 0.25)
        line(cv, pts, G("#e6c8ff"), 2.5, 0.9)
    return cv


# ----------------------------------------------------------------------------- hero

def pedestal(w=300, h=300):
    cv = blank(h, w)
    soft_shadow(cv, 150, 222, 112, 30, 0.45)
    R = 112
    d, nx, ny = hex_field(h, w, R)
    d2 = d.copy()
    side = blank(h, w)
    side_col = np.broadcast_to(G("#5d554f"), (h, w, 3)).copy()
    shift = np.zeros_like(d)
    from scipy import ndimage as ndi
    side_d = ndi.shift(d, (20, 0), order=1, mode="nearest")
    paint(cv, aa(side_d), side_col * (1.0 - 0.0))
    top = shade_grad((w, h), G("#ddd5c4"), G("#9d9486"), (-0.4, -0.9))
    top = top + bevel(d, nx, ny, 7, 0.25)[..., None]
    paint(cv, aa(d), np.clip(top, 0, 1))
    ring = aa(hex_field(h, w, R - 16)[0] + 0) * (1 - aa(hex_field(h, w, R - 24)[0]))
    paint(cv, ring, G("#fff4cf"), 0.9)                       # glowing rune ring (tinted in-game via second sprite)
    return cv


def pedestal_glow(w=300, h=300):
    cv = blank(h, w)
    ring = aa(hex_field(h, w, 96)[0]) * (1 - aa(hex_field(h, w, 74)[0]))
    cv[..., :3] = 1
    cv[..., 3] = ring
    return cv


def hero_body(w=240, h=330):
    cv = blank(h, w)
    cx = 120
    poly(cv, [(cx - 52, 296), (cx - 36, 150), (cx + 36, 150), (cx + 56, 296), (cx + 20, 310), (cx - 24, 310)],
         G("#4a5878"), G("#1f2740"), light=(-0.7, -0.5), outline=0.5)                          # cloak
    poly(cv, [(cx - 30, 300), (cx - 18, 200), (cx + 4, 200), (cx - 4, 300)], G("#5d6f93"), G("#2d3858"))      # cloak fold
    poly(cv, [(cx - 28, 204), (cx - 24, 150), (cx + 24, 150), (cx + 28, 204), (cx + 20, 252), (cx - 20, 252)],
         G("#d5dbe6"), G("#7d879c"), outline=0.5)                                            # breastplate
    poly(cv, [(cx - 20, 248), (cx + 20, 248), (cx + 24, 262), (cx - 24, 262)], G("#a8651f"), G("#6d3f12"))      # belt
    blob(cv, cx - 38, 158, 20, 17, G("#e6ebf3"), G("#8892a6"))                              # pauldrons
    blob(cv, cx + 38, 158, 20, 17, G("#cfd6e2"), G("#757f95"))
    blob(cv, cx, 112, 26, 28, G("#f0f3f8"), G("#8d97ab"))                                   # helm
    poly(cv, [(cx - 18, 108), (cx + 18, 108), (cx + 14, 122), (cx - 14, 122)], G("#1a1f2e"), G("#0b0e17"))    # visor slit
    poly(cv, [(cx - 3, 86), (cx + 3, 86), (cx + 26, 52), (cx + 40, 76), (cx + 12, 98)], G("#e3463f"), G("#8c1d1d"))  # plume
    poly(cv, [(cx + 52, 178), (cx + 62, 178), (cx + 64, 288), (cx + 50, 288)], G("#c9d0dd"), G("#7a8397"))    # sword
    poly(cv, [(cx + 40, 270), (cx + 76, 270), (cx + 76, 280), (cx + 40, 280)], G("#d6a642"), G("#8f651b"))
    return cv


def hero_banner(w=150, h=330):
    cv = blank(h, w)
    poly(cv, [(26, 322), (26, 20), (34, 20), (34, 322)], G("#c9a45a"), G("#6e5320"))                    # pole (not tinted)
    cv[..., :3] = np.where(cv[..., 3:4] > 0, cv[..., :3], 1)
    return cv


def hero_flag(w=150, h=330):
    cv = blank(h, w)
    poly(cv, [(34, 24), (126, 40), (108, 74), (126, 108), (34, 100)], G("#ffffff"), G("#bfc3cf"), outline=0.25)
    line(cv, [(44, 56), (104, 62)], G("#7c8296"), 2, 0.35)
    return cv


def badge_face(w=128, h=150):
    cv = blank(h, w)
    pts = [(14, 14), (114, 14), (114, 74), (64, 138), (14, 74)]
    poly(cv, [(x, y) for x, y in pts], G("#ffffff"), G("#c3c8d6"), outline=0.2)
    return cv


def badge_rim(w=128, h=150):
    cv = blank(h, w)
    pts = [(14, 14), (114, 14), (114, 74), (64, 138), (14, 74)]
    outer = poly_mask(pts, (w, h))
    inner = poly_mask([(w / 2 + (x - w / 2) * 0.86, 76 + (y - 76) * 0.86) for x, y in pts], (w, h))
    ring = np.clip(outer - inner, 0, 1)
    col = shade_grad((w, h), G("#f4f6fb"), G("#7e8696"), (-0.5, -0.8))
    paint(cv, ring, col)
    paint(cv, np.clip(inner * 0.0, 0, 1), col)
    return cv


def coin(w=96, h=96):
    cv = blank(h, w)
    blob(cv, 48, 48, 42, 42, G("#ffe27a"), G("#c9821a"))
    blob(cv, 48, 48, 31, 31, G("#f2b73a"), G("#ffe27a"), light=(0.5, 0.8), outline=True)
    poly(cv, [(48, 24), (53, 40), (70, 42), (57, 52), (62, 69), (48, 59), (34, 69), (39, 52), (26, 42), (43, 40)],
         G("#fff1b0"), G("#e0a02a"))
    return cv


# ----------------------------------------------------------------------------- fx

def fx():
    n = 256
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx + .5 - n / 2, yy + .5 - n / 2) / (n / 2)
    soft = blank(n, n); soft[..., :3] = 1; soft[..., 3] = np.clip(1 - r, 0, 1) ** 2.2
    save("fx_soft", soft, 256)
    ring = blank(n, n); ring[..., :3] = 1
    ring[..., 3] = np.exp(-((r - 0.82) / 0.07) ** 2) * (r < 1.05)
    save("fx_ring", ring, 256)
    sp = blank(n, n); sp[..., :3] = 1
    a = np.exp(-(np.abs(xx + .5 - n / 2) / 5.0)) * np.clip(1 - np.abs(yy + .5 - n / 2) / (n / 2), 0, 1) ** 1.5
    b = np.exp(-(np.abs(yy + .5 - n / 2) / 5.0)) * np.clip(1 - np.abs(xx + .5 - n / 2) / (n / 2), 0, 1) ** 1.5
    sp[..., 3] = np.clip(a + b + np.clip(1 - r * 3, 0, 1), 0, 1)
    save("fx_spark", sp, 256)
    sl = blank(n, n); sl[..., :3] = 1
    ang = np.arctan2(yy + .5 - n / 2, xx + .5 - n / 2)
    crescent = np.exp(-((r - 0.75) / 0.09) ** 2) * np.clip(1 - np.abs(ang + 0.6) / 1.1, 0, 1) ** 0.8
    sl[..., 3] = np.clip(crescent, 0, 1)
    save("fx_slash", sl, 256)


def build():
    save("tree_a", tree_round(n=1, seed=1), TILE_PPU, (0.5, 0.18))
    save("tree_b", tree_round(n=3, seed=2), TILE_PPU, (0.5, 0.18))
    save("pine_a", tree_pine(n=2), TILE_PPU, (0.5, 0.15))
    save("pine_b", tree_pine(n=1), TILE_PPU, (0.5, 0.15))
    save("rocks", rocks(), TILE_PPU, (0.5, 0.2))
    save("ruins", ruins(), TILE_PPU, (0.5, 0.12))
    save("ore", ore(), TILE_PPU, (0.5, 0.2))
    save("rubble", rubble(), TILE_PPU, (0.5, 0.15))
    save("pedestal", pedestal(), TILE_PPU, (0.5, 0.5))
    save("pedestal_glow", pedestal_glow(), TILE_PPU, (0.5, 0.5))
    save("hero_body", hero_body(), TILE_PPU, (0.5, 0.06))
    save("hero_pole", hero_banner(), TILE_PPU, (0.18, 0.06))
    save("hero_flag", hero_flag(), TILE_PPU, (0.18, 0.06))
    save("badge_face", badge_face(), TILE_PPU, (0.5, 0.5))
    save("badge_rim", badge_rim(), TILE_PPU, (0.5, 0.5))
    save("coin", coin(), TILE_PPU, (0.5, 0.5))
    fx()


if __name__ == "__main__":
    build(); write_manifest()
