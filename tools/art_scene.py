from art_common import *
from art_decor import blob, poly, line, G
import art_ui
from art_ui import metal

rng = np.random.default_rng(11)


# ----------------------------------------------------------------------------- water (seamless)

def water():
    n = 1024
    r = np.random.default_rng(5)
    base = tileable_noise(n, 2.2, r)
    detail = tileable_noise(n, 1.4, r)
    deep, mid, shallow = hexcolor("#102a55"), hexcolor("#1a4c7c"), hexcolor("#2a7ea0")
    rgb = lerp3(0.65 * base + 0.35 * detail, deep, mid, shallow)
    ridged = 1 - np.abs(2 * tileable_noise(n, 1.7, r) - 1)
    crest = np.clip((ridged - 0.86) / 0.14, 0, 1) ** 1.5
    rgb = rgb + (hexcolor("#9ee0ee") * crest[..., None]) * 0.35
    a = np.dstack([np.clip(rgb, 0, 1), np.ones((n, n), np.float32)])
    save("water_a", a, 100)
    # translucent highlight layer drifting the other way (parallax)
    r2 = np.random.default_rng(9)
    ridged2 = 1 - np.abs(2 * tileable_noise(n, 1.6, r2) - 1)
    crest2 = np.clip((ridged2 - 0.90) / 0.10, 0, 1) ** 1.6
    b = np.zeros((n, n, 4), np.float32)
    b[..., :3] = hexcolor("#d6f4ff")
    b[..., 3] = crest2 * 0.30
    save("water_b", b, 100)


def vignette():
    n = 512
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(xx + .5 - n / 2, yy + .5 - n / 2) / (n / 2)
    a = np.clip((r - 0.45) / 0.75, 0, 1) ** 1.6
    v = blank(n, n)
    v[..., :3] = hexcolor("#05070f")
    v[..., 3] = a * 0.78
    save("ui_vignette", v, 100)


# ----------------------------------------------------------------------------- logo

def gold_text(txt, fnt, pad=40, outline=10):
    tmp = Image.new("L", (10, 10))
    bbox = ImageDraw.Draw(tmp).textbbox((0, 0), txt, font=fnt, stroke_width=outline)
    w, h = bbox[2] - bbox[0] + pad * 2, bbox[3] - bbox[1] + pad * 2
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).text((pad - bbox[0], pad - bbox[1]), txt, font=fnt, fill=255)
    m = np.asarray(mask, np.float32) / 255
    big = Image.new("L", (w, h), 0)
    ImageDraw.Draw(big).text((pad - bbox[0], pad - bbox[1]), txt, font=fnt, fill=255, stroke_width=outline)
    mo = np.asarray(big, np.float32) / 255
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32)
    t = np.clip((yy - pad) / max(1, h - 2 * pad), 0, 1)
    rgb = lerp3(1 - t, hexcolor("#7a4a12"), hexcolor("#e2a93a"), hexcolor("#fff1b8"))
    # engraved highlight line
    rgb = rgb + (np.clip(1 - np.abs(t - 0.5) / 0.04, 0, 1) * 0.15)[..., None]
    cv = blank(h, w)
    sh = np.asarray(Image.fromarray((mo * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
    paint(cv, np.roll(sh, (14, 8), (0, 1)), G("#05060c"), 0.7)
    paint(cv, mo, G("#2a1606"))
    paint(cv, m, rgb)
    return cv


def logo():
    f1 = font("CinzelDecorative-Bold.ttf", 250)
    f2 = font("Almendra-Bold.ttf", 54)
    title = gold_text("HEXQUEST", f1, pad=50, outline=10)
    tag = gold_text("A  SAGA  OF  TERRITORY  &  CUNNING", f2, pad=24, outline=4)
    th, tw = title.shape[:2]
    gh, gw = tag.shape[:2]
    Wd = max(tw, gw) + 40
    emblem = 240
    Hd = emblem + th + gh + 10
    cv = blank(Hd, Wd)
    # emblem: gold hex with crossed swords
    hx = blank(emblem, emblem)
    d, nx, ny = hex_field(emblem, emblem, 112)
    paint(hx, aa(d), metal(emblem, emblem))
    paint(hx, aa(hex_field(emblem, emblem, 98)[0]), np.stack([lerp3(np.mgrid[0:emblem, 0:emblem][0] / emblem, hexcolor("#26324f"), hexcolor("#141c33"), hexcolor("#0b1122"))[..., i] for i in range(3)], -1))
    sw = art_ui.glyph(art_ui.ic_attack, 192, shadow=False)
    sw = np.asarray(Image.fromarray((sw * 255).astype(np.uint8)).resize((150, 150), Image.LANCZOS), np.float32) / 255
    sw[..., :3] = hexcolor("#ffe9a0")
    paint(hx, sw[..., 3], sw[..., :3], 1.0, box=(45, 40))
    paint(cv, hx[..., 3], hx[..., :3], 1.0, box=((Wd - emblem) // 2, 0))
    over_slice = cv[emblem - 30:emblem - 30 + th, (Wd - tw) // 2:(Wd - tw) // 2 + tw]
    over(over_slice, title)
    over_slice2 = cv[emblem - 30 + th - 20:emblem - 30 + th - 20 + gh, (Wd - gw) // 2:(Wd - gw) // 2 + gw]
    over(over_slice2, tag)
    save("logo", cv, 100)


# ----------------------------------------------------------------------------- title screen layers (2560x1440)

TW, TH = 2560, 1440


def ridge(base, amp, scale, seed, w=TW):
    r = np.random.default_rng(seed)
    x = np.arange(w, dtype=np.float32)
    y = np.zeros(w, np.float32)
    for o in range(5):
        s = scale / (2 ** o)
        g = r.random(int(w / s) + 3).astype(np.float32)
        y += np.interp(x / s, np.arange(len(g)), g) * (0.5 ** o)
    y = (y - y.min()) / (y.max() - y.min() + 1e-6)
    return base - y * amp


def layer_sky():
    yy, xx = np.mgrid[0:TH, 0:TW].astype(np.float32)
    t = yy / TH
    top, mid, hor = hexcolor("#150f3a"), hexcolor("#6b3a86"), hexcolor("#ffb15e")
    sky = np.where(t[..., None] < 0.62, top + (mid - top) * (t[..., None] / 0.62) ** 1.2,
                   mid + (hor - mid) * (np.clip(t[..., None] - 0.62, 0, None) / 0.38) ** 1.1)
    sun = np.exp(-((xx - TW * 0.62) ** 2 + (yy - TH * 0.60) ** 2) / (2 * 260.0 ** 2))
    sky = sky + hexcolor("#ffd9a0") * sun[..., None] * 0.65
    r = np.random.default_rng(3)
    stars = (r.random((TH, TW)) > 0.9993) * np.clip(1 - t * 1.8, 0, 1)
    stars = np.asarray(Image.fromarray((stars * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255
    sky = sky + stars[..., None] * 2.5
    cl = noise(TH, TW, 380, r, 4)
    bands = np.clip((cl - 0.52) * 3, 0, 1) * np.clip(1 - np.abs(t - 0.45) * 3.2, 0, 1)
    sky = sky * (1 - bands[..., None] * 0.35) + hexcolor("#ffc1d6") * (bands[..., None] * 0.18)
    save("title_sky", np.dstack([np.clip(sky, 0, 1), np.ones((TH, TW), np.float32)]), 100)


def layer_mountains():
    cv = blank(TH, TW)
    fade = [hexcolor("#7a5a9c"), hexcolor("#5a3f84"), hexcolor("#3a2b66")]
    for i, (base, amp, sc, seed) in enumerate([(960, 330, 420, 21), (1040, 380, 330, 22), (1120, 300, 260, 23)]):
        y = ridge(base, amp, sc, seed)
        yy = np.mgrid[0:TH, 0:TW][0].astype(np.float32)
        m = np.clip(0.5 + (yy - y[None, :]) / 1.5, 0, 1)
        col = fade[i] * (1 - 0.35 * np.clip((yy - y[None, :]) / 500, 0, 1))[..., None]
        paint(cv, m, col)
    save("title_far", cv, 100)


def layer_hills():
    cv = blank(TH, TW)
    yy = np.mgrid[0:TH, 0:TW][0].astype(np.float32)
    r = np.random.default_rng(31)
    y = ridge(1130, 150, 520, 31)
    m = np.clip(0.5 + (yy - y[None, :]) / 1.5, 0, 1)
    paint(cv, m, hexcolor("#1c2347") * (1 - 0.4 * np.clip((yy - y[None, :]) / 360, 0, 1))[..., None])
    # pine silhouettes along the ridge
    sil = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
    d = ImageDraw.Draw(sil)
    for x in range(0, TW, 16):
        if r.random() < 0.62:
            h = r.uniform(70, 150)
            base_y = float(y[min(TW - 1, x)]) + 14
            w0 = h * 0.28
            for k in range(3):
                top = base_y - h * (0.35 + 0.33 * k) - h * 0.28
                bot = base_y - h * 0.33 * k
                d.polygon([(x, top), (x + w0 * (1 - 0.16 * k), bot), (x - w0 * (1 - 0.16 * k), bot)], fill=(14, 20, 40, 255))
    over(cv, np.asarray(sil, np.float32) / 255)
    save("title_hills", cv, 100)


def layer_foreground():
    """A little island of the real game tiles in the foreground, plus water and a hero."""
    cv = blank(TH, TW)
    yy, xx = np.mgrid[0:TH, 0:TW].astype(np.float32)
    # water band
    wy = 1160
    wm = np.clip(0.5 + (yy - wy), 0, 1)
    wt = np.clip((yy - wy) / (TH - wy), 0, 1)
    wc = lerp3(wt, hexcolor("#ec9a62"), hexcolor("#2f4f86"), hexcolor("#0f2146"))
    sun = np.exp(-((xx - TW * 0.62) ** 2) / (2 * 150.0 ** 2)) * np.clip(1 - wt * 1.6, 0, 1)
    wc = wc + hexcolor("#ffe0b0") * sun[..., None] * 0.6
    paint(cv, wm, np.clip(wc, 0, 1))
    # island of tiles
    tiles = {n: Image.open(os.path.join(OUT, f"ground_{n}.png")) for n in ["grass_0", "grass_1", "grass_2", "forest", "sand", "rock"]}
    side = Image.open(os.path.join(OUT, "hex_side.png"))
    shadow = Image.open(os.path.join(OUT, "hex_shadow.png"))
    layout = [(-2, 1, "grass_1"), (-1, 1, "grass_0"), (0, 1, "sand"), (1, 1, "grass_2"), (2, 1, "rock"),
              (-1, 0, "forest"), (0, 0, "grass_0"), (1, 0, "grass_1"), (-2, 2, "grass_2"), (-1, 2, "grass_1"), (0, 2, "forest")]
    sc = 0.78
    tw, th = int(TILE_W * sc), int(TILE_H * sc)
    ox, oy = int(TW * 0.30), int(TH * 0.60)
    pil = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
    ordered = sorted(layout, key=lambda t: (t[1], t[0]))
    for q, rr, n in ordered:       # all shadows + sides first
        x = ox + int((q + rr * 0.5) * tw) - tw // 2
        y = oy + int(rr * th * 0.75) - th // 2
        s = shadow.resize((int(shadow.width * sc), int(shadow.height * sc)), Image.LANCZOS)
        pil.alpha_composite(s, (x - int(60 * sc), y - int(60 * sc) + 36))
    for q, rr, n in ordered:
        x = ox + int((q + rr * 0.5) * tw) - tw // 2
        y = oy + int(rr * th * 0.75) - th // 2
        pil.alpha_composite(side.resize((tw, th), Image.LANCZOS), (x, y + int(46 * sc)))
    for q, rr, n in ordered:
        x = ox + int((q + rr * 0.5) * tw) - tw // 2
        y = oy + int(rr * th * 0.75) - th // 2
        pil.alpha_composite(tiles[n].resize((tw, th), Image.LANCZOS), (x, y))
    decor = [("pine_a", -1, 0, 0.95), ("tree_b", 1, 0, 0.95), ("ruins", 0, 1, 0.85), ("rocks", 2, 1, 0.8), ("tree_a", -2, 2, 0.8),
             ("pine_b", 0, 2, 0.85), ("ore", -1, 1, 0.7)]
    for name, q, rr, s in sorted(decor, key=lambda t: t[2]):
        im = Image.open(os.path.join(OUT, name + ".png"))
        im = im.resize((int(im.width * sc * s), int(im.height * sc * s)), Image.LANCZOS)
        cx = ox + int((q + rr * 0.5) * tw)
        cy = oy + int(rr * th * 0.75) + int(th * 0.18)
        pil.alpha_composite(im, (cx - im.width // 2, cy - int(im.height * 0.85)))
    hero = Image.open(os.path.join(OUT, "hero_body.png"))
    flag = Image.open(os.path.join(OUT, "hero_flag.png")).copy()
    pole = Image.open(os.path.join(OUT, "hero_pole.png"))
    pedestal = Image.open(os.path.join(OUT, "pedestal.png"))
    hs = 1.15
    hx_, hy_ = ox + int((0 + 0 * 0.5) * tw), oy + int(0 * th * 0.75)
    pil.alpha_composite(pedestal.resize((int(pedestal.width * sc * 0.8), int(pedestal.height * sc * 0.8))), (hx_ - int(pedestal.width * sc * 0.4), hy_ - int(pedestal.height * sc * 0.4)))
    f2 = np.asarray(flag, np.float32) / 255
    f2[..., :3] *= hexcolor("#d0453d")
    flag = Image.fromarray((f2 * 255).astype(np.uint8), "RGBA")
    for part in (pole, flag):
        im = part.resize((int(part.width * sc * hs), int(part.height * sc * hs)), Image.LANCZOS)
        pil.alpha_composite(im, (hx_ + 40, hy_ - int(im.height * 0.86)))
    im = hero.resize((int(hero.width * sc * hs), int(hero.height * sc * hs)), Image.LANCZOS)
    pil.alpha_composite(im, (hx_ - im.width // 2, hy_ - int(im.height * 0.86)))
    over(cv, np.asarray(pil, np.float32) / 255)
    save("title_fore", cv, 100)


def build():
    water(); vignette(); logo()
    layer_sky(); layer_mountains(); layer_hills(); layer_foreground()


if __name__ == "__main__":
    build(); write_manifest()
