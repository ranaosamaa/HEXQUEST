from art_common import *
from art_decor import blob, poly, line, G

# ----------------------------------------------------------------------------- 9-slice frames

def rrect_sdf(w, h, r, inset=0.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    px = np.abs(xx + 0.5 - w / 2) - (w / 2 - inset - r)
    py = np.abs(yy + 0.5 - h / 2) - (h / 2 - inset - r)
    return np.hypot(np.maximum(px, 0), np.maximum(py, 0)) + np.minimum(np.maximum(px, py), 0) - r


def metal(w, h):
    """Bronze-to-gold vertical gradient used for frames."""
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    t = np.clip(1 - np.abs(yy - 0.28) * 1.7, 0, 1)
    return lerp3(t, hexcolor("#6a4417"), hexcolor("#c4912e"), hexcolor("#ffe9a0"))


def corner_ornaments(cv, w, h, size, color):
    """Small gold diamonds + curls in each corner."""
    for (cx, cy) in [(size, size), (w - size, size), (size, h - size), (w - size, h - size)]:
        poly(cv, [(cx, cy - 10), (cx + 10, cy), (cx, cy + 10), (cx - 10, cy)], G("#fff0b5"), G("#b8832a"), outline=0.5)
        blob(cv, cx, cy, 3.5, 3.5, G("#6a4417"), G("#3a230a"), outline=False)


def framed(w, h, r, fill_top, fill_bot, border=8, ornaments=True, name=None, nslice=None, texture=0.0, seed=0):
    rng = np.random.default_rng(seed)
    d = rrect_sdf(w, h, r)
    outer = aa(d)
    cv = blank(h, w)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    fill = (hexcolor(fill_top) + (hexcolor(fill_bot) - hexcolor(fill_top)) * yy[..., None]) if fill_top is not None else None
    if texture and fill is not None:
        n = noise(h, w, 18, rng, 3)
        fill = fill * (1 + texture * (n[..., None] - 0.5))
    # inner vignette
    di = -rrect_sdf(w, h, r)
    if fill is not None:
        fill = fill * (0.80 + 0.20 * np.clip(di / 40.0, 0, 1))[..., None]
    if fill_top is not None:
        paint(cv, outer, fill)
    # metal border
    band = aa(d) * (1 - aa(rrect_sdf(w, h, r, border)))
    paint(cv, band, metal(w, h))
    dark = aa(rrect_sdf(w, h, r, border)) * (1 - aa(rrect_sdf(w, h, r, border + 3)))
    paint(cv, dark, G("#1b1208"), 0.85)
    thin = aa(rrect_sdf(w, h, r, border + 8)) * (1 - aa(rrect_sdf(w, h, r, border + 10)))
    paint(cv, thin, G("#d9ab4a"), 0.55)
    outline = aa(d) * (1 - aa(rrect_sdf(w, h, r, 1.6)))
    paint(cv, outline, G("#1b1208"), 0.9)
    if ornaments:
        corner_ornaments(cv, w, h, border + 18, None)
    return cv


def parchment(w=192, h=192, r=22, seed=3):
    rng = np.random.default_rng(seed)
    d = rrect_sdf(w, h, r)
    n = noise(h, w, 26, rng, 4)
    base = lerp3(n, hexcolor("#cdb27c"), hexcolor("#e6d3a3"), hexcolor("#f3e6bf"))
    di = -rrect_sdf(w, h, r)
    burn = np.clip(1 - di / 34.0, 0, 1) ** 1.6
    rgb = base * (1 - 0.42 * burn)[..., None] + hexcolor("#5a3a14") * (0.30 * burn)[..., None]
    cv = blank(h, w)
    paint(cv, aa(d), rgb)
    for k, a in [(7, 0.9), (13, 0.5)]:
        band = aa(rrect_sdf(w, h, r, k)) * (1 - aa(rrect_sdf(w, h, r, k + 2.2)))
        paint(cv, band, G("#6b4a1d"), a)
    corner_ornaments(cv, w, h, 26, None)
    return cv


# ----------------------------------------------------------------------------- icons (white glyphs)

def glyph(draw_fn, size=192, shadow=True):
    S = 4
    im = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    draw_fn(d, size * S)
    g = downsample(im, (size, size))
    if shadow:
        a = np.asarray(g, np.float32)[..., 3] / 255
        sh = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)), np.float32) / 255
        out = blank(size, size)
        out[..., :3] = hexcolor("#0b0a14")
        out[..., 3] = np.roll(sh, (4, 3), (0, 1)) * 0.6
        over(out, np.asarray(g, np.float32) / 255)
        return out
    return np.asarray(g, np.float32) / 255


W = (255, 255, 255, 255)


def p(n, pts):
    return [(x * n / 192, y * n / 192) for x, y in pts]


def ic_attack(d, n):
    for sgn in (1, -1):
        cx = 96
        pts = [(cx - 8, 150), (cx + 8, 150), (cx + 6, 40), (cx, 22), (cx - 6, 40)]
        # rotate blade around centre
        ang = math.radians(40 * sgn)
        rot = [((x - 96) * math.cos(ang) - (y - 96) * math.sin(ang) + 96, (x - 96) * math.sin(ang) + (y - 96) * math.cos(ang) + 96) for x, y in pts]
        d.polygon(p(n, rot), fill=W)
        guard = [(cx - 28, 136), (cx + 28, 136), (cx + 28, 146), (cx - 28, 146)]
        grd = [((x - 96) * math.cos(ang) - (y - 96) * math.sin(ang) + 96, (x - 96) * math.sin(ang) + (y - 96) * math.cos(ang) + 96) for x, y in guard]
        d.polygon(p(n, grd), fill=W)
        hl = [(cx - 6, 146), (cx + 6, 146), (cx + 6, 172), (cx - 6, 172)]
        hr = [((x - 96) * math.cos(ang) - (y - 96) * math.sin(ang) + 96, (x - 96) * math.sin(ang) + (y - 96) * math.cos(ang) + 96) for x, y in hl]
        d.polygon(p(n, hr), fill=W)


def ic_move(d, n):                      # boot
    d.polygon(p(n, [(66, 30), (112, 30), (116, 100), (160, 126), (166, 156), (52, 156), (52, 100)]), fill=W)
    d.rectangle([p(n, [(52, 160)])[0], p(n, [(166, 172)])[0]], fill=W)


def ic_claim(d, n):                     # flag
    d.rectangle([p(n, [(48, 24)])[0], p(n, [(60, 170)])[0]], fill=W)
    d.polygon(p(n, [(60, 28), (158, 54), (60, 100), (96, 64)]), fill=W)
    d.polygon(p(n, [(34, 172), (86, 172), (74, 158), (46, 158)]), fill=W)


def ic_reinforce(d, n):                 # shield with plus
    d.polygon(p(n, [(36, 34), (156, 34), (156, 100), (96, 168), (36, 100)]), fill=W)
    d.polygon(p(n, [(88, 56), (104, 56), (104, 84), (132, 84), (132, 100), (104, 100), (104, 128), (88, 128), (88, 100), (60, 100), (60, 84), (88, 84)]), fill=(0, 0, 0, 0))
    # punch the plus out with transparency
    im = d._image
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).polygon(p(n, [(88, 56), (104, 56), (104, 84), (132, 84), (132, 100), (104, 100), (104, 128), (88, 128), (88, 100), (60, 100), (60, 84), (88, 84)]), fill=255)
    a = np.asarray(im, np.uint8).copy()
    a[np.asarray(mask) > 0] = (0, 0, 0, 0)
    im.paste(Image.fromarray(a, "RGBA"))


def ic_gather(d, n):                    # pickaxe
    d.line(p(n, [(60, 168), (130, 52)]), fill=W, width=int(n * 0.06))
    d.polygon(p(n, [(30, 78), (66, 40), (108, 34), (150, 44), (176, 72), (150, 66), (110, 62), (74, 72)]), fill=W)


def ic_upgrade(d, n):                   # arrow + spark
    d.polygon(p(n, [(96, 24), (150, 88), (116, 88), (116, 164), (76, 164), (76, 88), (42, 88)]), fill=W)
    d.polygon(p(n, [(152, 30), (158, 48), (176, 54), (158, 60), (152, 78), (146, 60), (128, 54), (146, 48)]), fill=W)


def ic_pass(d, n):                      # hourglass
    d.polygon(p(n, [(50, 24), (142, 24), (142, 40), (110, 92), (110, 100), (142, 152), (142, 168), (50, 168), (50, 152), (82, 100), (82, 92), (50, 40)]), fill=W)


def ic_stats(d, n):
    for x, hgt in [(40, 70), (82, 112), (124, 150)]:
        d.rounded_rectangle(p(n, [(x, 170 - hgt), (x + 32, 170)]), radius=int(n * 0.02), fill=W)


def ic_menu(d, n):
    for y in (52, 88, 124):
        d.rounded_rectangle(p(n, [(36, y), (156, y + 18)]), radius=int(n * 0.04), fill=W)


def ic_focus(d, n):
    d.ellipse(p(n, [(40, 40), (152, 152)]), outline=W, width=int(n * 0.05))
    d.ellipse(p(n, [(82, 82), (110, 110)]), fill=W)
    for a in [((96, 14), (96, 46)), ((96, 146), (96, 178)), ((14, 96), (46, 96)), ((146, 96), (178, 96))]:
        d.line(p(n, list(a)), fill=W, width=int(n * 0.045))


def ic_seats(d, n):
    for cx, s in [(66, 1.0), (128, 0.8)]:
        d.ellipse(p(n, [(cx - 22 * s, 44), (cx + 22 * s, 44 + 44 * s)]), fill=W)
        d.pieslice(p(n, [(cx - 40 * s, 96), (cx + 40 * s, 96 + 120 * s)]), 180, 360, fill=W)


def ic_cancel(d, n):
    d.line(p(n, [(48, 48), (144, 144)]), fill=W, width=int(n * 0.085))
    d.line(p(n, [(144, 48), (48, 144)]), fill=W, width=int(n * 0.085))


def ic_territory(d, n):
    pts = [(96 + 70 * math.cos(math.radians(60 * i - 30)), 96 + 70 * math.sin(math.radians(60 * i - 30))) for i in range(6)]
    d.polygon(p(n, pts), outline=W, width=int(n * 0.05))
    inner = [(96 + 34 * math.cos(math.radians(60 * i - 30)), 96 + 34 * math.sin(math.radians(60 * i - 30))) for i in range(6)]
    d.polygon(p(n, inner), fill=W)


def ic_crown(d, n):
    d.polygon(p(n, [(26, 150), (26, 62), (66, 100), (96, 40), (126, 100), (166, 62), (166, 150)]), fill=W)
    d.rectangle([p(n, [(26, 156)])[0], p(n, [(166, 172)])[0]], fill=W)


def ic_gear(d, n):
    cx = cy = 96
    pts = []
    for i in range(16):
        a0 = math.radians(i * 22.5)
        r = 76 if i % 2 == 0 else 58
        pts += [(cx + r * math.cos(a0 - 0.18), cy + r * math.sin(a0 - 0.18)), (cx + r * math.cos(a0 + 0.18), cy + r * math.sin(a0 + 0.18))]
    d.polygon(p(n, pts), fill=W)
    d.ellipse(p(n, [(66, 66), (126, 126)]), fill=(0, 0, 0, 0))
    im = d._image
    a = np.asarray(im, np.uint8).copy()
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    a[np.hypot(xx - n / 2, yy - n / 2) < n * 0.16] = (0, 0, 0, 0)
    im.paste(Image.fromarray(a, "RGBA"))


def ic_book(d, n):
    d.polygon(p(n, [(30, 44), (92, 56), (92, 160), (30, 148)]), fill=W)
    d.polygon(p(n, [(162, 44), (100, 56), (100, 160), (162, 148)]), fill=W)


def ic_play(d, n):
    d.polygon(p(n, [(54, 30), (160, 96), (54, 162)]), fill=W)


def ic_check(d, n):
    d.line(p(n, [(36, 100), (80, 144), (160, 52)]), fill=W, width=int(n * 0.09), joint="curve")


def ic_door(d, n):
    d.rounded_rectangle(p(n, [(40, 24), (120, 168)]), radius=int(n * 0.03), fill=W)
    d.polygon(p(n, [(110, 80), (150, 80), (150, 60), (184, 96), (150, 132), (150, 112), (110, 112)]), fill=W)


def ic_dice(d, n):
    d.rounded_rectangle(p(n, [(36, 36), (156, 156)]), radius=int(n * 0.07), fill=W)
    im = d._image
    a = np.asarray(im, np.uint8).copy()
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    for px, py in [(70, 70), (122, 70), (96, 96), (70, 122), (122, 122)]:
        a[np.hypot(xx - px * n / 192, yy - py * n / 192) < n * 0.045] = (0, 0, 0, 0)
    im.paste(Image.fromarray(a, "RGBA"))


ICONS = dict(attack=ic_attack, move=ic_move, claim=ic_claim, reinforce=ic_reinforce, gather=ic_gather, upgrade=ic_upgrade,
             pass_=ic_pass, stats=ic_stats, menu=ic_menu, focus=ic_focus, seats=ic_seats, cancel=ic_cancel,
             territory=ic_territory, crown=ic_crown, gear=ic_gear, book=ic_book, play=ic_play, check=ic_check,
             door=ic_door, dice=ic_dice)


def hex_ui():
    H, Wd = 184, 160
    d, nx, ny = hex_field(H, Wd, 78)
    rim = blank(H, Wd)
    col = metal(Wd, H)
    paint(rim, aa(d) * (1 - aa(hex_field(H, Wd, 66)[0])), col)
    paint(rim, aa(hex_field(H, Wd, 66)[0]) * (1 - aa(hex_field(H, Wd, 63)[0])), G("#1b1208"), 0.85)
    save("ui_hex_rim", rim, UI_PPU)
    face = blank(H, Wd)
    d2, nx2, ny2 = hex_field(H, Wd, 64)
    g = shade_grad((Wd, H), G("#ffffff"), G("#b8bccb"), (-0.4, -0.9))
    g = g + bevel(d2, nx2, ny2, 6, 0.18)[..., None]
    paint(face, aa(d2), np.clip(g, 0, 1))
    save("ui_hex_face", face, UI_PPU)


def build():
    # panels / buttons (9-slice)
    save("ui_panel_dark", framed(192, 192, 24, "#1d2a4a", "#0d1428", border=9, texture=0.12, seed=1), UI_PPU, border=(64, 64, 64, 64))
    save("ui_panel_parchment", parchment(), UI_PPU, border=(64, 64, 64, 64))
    save("ui_button", framed(176, 104, 26, "#8a5a26", "#4a2c12", border=7, ornaments=False, texture=0.10, seed=2), UI_PPU, border=(48, 48, 48, 48))
    save("ui_button_dark", framed(176, 104, 26, "#26324f", "#0f1730", border=7, ornaments=False, texture=0.10, seed=4), UI_PPU, border=(48, 48, 48, 48))
    save("ui_card_frame", framed(160, 192, 26, None, None, border=8, ornaments=False, seed=5), UI_PPU, border=(48, 48, 48, 48))
    chip = framed(112, 64, 24, "#141c33", "#0a0f20", border=5, ornaments=False, seed=6)
    save("ui_chip", chip, UI_PPU, border=(30, 30, 30, 30))
    # face for coloured cards: soft glossy rounded rect (tintable)
    h = w = 128
    d = rrect_sdf(w, h, 26)
    face = blank(h, w)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    g = np.stack([1.0 - 0.38 * yy] * 3, -1)
    gloss = np.clip(1 - np.abs(yy - 0.18) / 0.22, 0, 1)[..., None] * 0.18
    paint(face, aa(d), np.clip(g + gloss, 0, 1))
    save("ui_face", face, UI_PPU, border=(40, 40, 40, 40))
    # round button
    n = 256
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx + .5 - n / 2, yy + .5 - n / 2)
    ring = blank(n, n)
    paint(ring, aa(r - 126) * (1 - aa(r - 112)), metal(n, n))
    paint(ring, aa(r - 112) * (1 - aa(r - 108)), G("#1b1208"), 0.9)
    inner = aa(r - 108)
    fill = lerp3(np.clip(yy / n, 0, 1), hexcolor("#26324f"), hexcolor("#141c33"), hexcolor("#0b1122"))
    paint(ring, inner, fill)
    paint(ring, aa(r - 126) * (1 - aa(r - 123)), G("#1b1208"), 0.9)
    save("ui_round", ring, UI_PPU)
    faceR = blank(n, n)
    paint(faceR, aa(r - 104), np.stack([1 - 0.4 * (yy / n)] * 3, -1))
    save("ui_round_face", faceR, UI_PPU)
    # slider
    trk = framed(96, 40, 18, "#0b1122", "#16213a", border=4, ornaments=False, seed=7)
    save("ui_slider_track", trk, UI_PPU, border=(20, 20, 20, 20))
    fl = blank(40, 96)
    paint(fl, aa(rrect_sdf(96, 40, 16)), lerp3(np.mgrid[0:40, 0:96][0] / 40.0, hexcolor("#ffe9a0"), hexcolor("#d9a437"), hexcolor("#9b6a1f")))
    save("ui_slider_fill", fl, UI_PPU, border=(20, 20, 20, 20))
    k = blank(72, 72)
    blob(k, 36, 36, 31, 31, G("#ffe9a0"), G("#b8832a"))
    blob(k, 36, 36, 14, 14, G("#6a4417"), G("#3a230a"), outline=False)
    save("ui_knob", k, UI_PPU)
    # divider
    dv = blank(32, 512)
    for x0, x1 in [(10, 224), (288, 502)]:
        paint(dv, np.clip(1 - np.abs(np.mgrid[0:32, 0:512][0] - 16) / 1.6, 0, 1) * ((np.mgrid[0:32, 0:512][1] > x0) & (np.mgrid[0:32, 0:512][1] < x1)), G("#d9ab4a"), 0.9)
    poly(dv, [(256, 3), (272, 16), (256, 29), (240, 16)], G("#fff0b5"), G("#b8832a"), outline=0.5)
    save("ui_divider", dv, UI_PPU)
    hex_ui()
    for name, fn in ICONS.items():
        save("icon_" + name.rstrip("_"), glyph(fn), UI_PPU)


if __name__ == "__main__":
    build(); write_manifest()
