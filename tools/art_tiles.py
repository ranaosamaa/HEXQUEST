from art_common import *

PAL = {   # (dark, mid, light)
    "grass_0": ("#4f7f3a", "#79a948", "#a7cf63"),
    "grass_1": ("#56873f", "#82b04c", "#b2d669"),
    "grass_2": ("#47753a", "#6f9e47", "#9cc65f"),
    "forest":  ("#2f5a35", "#467a42", "#6a9a55"),
    "sand":    ("#b99a5e", "#dcc088", "#f0dcaa"),
    "rock":    ("#6e6a68", "#9a948c", "#c2bbae"),
}
R_TOP = TILE_R * 0.955


def ground(kind, seed):
    rng = np.random.default_rng(seed)
    H, W = TILE_H, TILE_W
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    n1, n2, n3 = noise(H, W, 120, rng, 4), noise(H, W, 30, rng, 3), noise(H, W, 7, rng, 2)
    t = 0.55 * n1 + 0.30 * n2 + 0.15 * n3
    c0, c1, c2 = (hexcolor(c) for c in PAL[kind])
    rgb = lerp3(t, c0, c1, c2)
    rgb = rgb * (1.07 - 0.17 * (yy / H) - 0.05 * (xx / W))[..., None]

    detail = Image.new("RGBA", (W * 2, H * 2), (0, 0, 0, 0))
    dr = ImageDraw.Draw(detail)
    if kind.startswith("grass") or kind == "forest":
        light, dark = (hexcolor(PAL[kind][2]), hexcolor(PAL[kind][0]))
        for _ in range(900):
            x, y = rng.uniform(40, W - 40) * 2, rng.uniform(60, H - 60) * 2
            L = rng.uniform(8, 20) * 2
            a = math.radians(rng.uniform(-115, -65))
            col = light if rng.random() < 0.55 else dark
            c = tuple(int(255 * v) for v in np.clip(col * rng.uniform(0.95, 1.12), 0, 1)) + (int(rng.uniform(70, 150)),)
            dr.line([x, y, x + math.cos(a) * L, y + math.sin(a) * L], fill=c, width=3)
        if kind != "forest":
            for _ in range(26):                       # tiny wildflowers
                x, y = rng.uniform(60, W - 60) * 2, rng.uniform(80, H - 80) * 2
                col = [(255, 238, 150), (255, 255, 255), (255, 160, 170), (180, 200, 255)][rng.integers(0, 4)]
                dr.ellipse([x - 4, y - 4, x + 4, y + 4], fill=col + (230,))
    elif kind == "sand":
        for _ in range(26):                           # wind ripples
            x, y = rng.uniform(60, W - 120) * 2, rng.uniform(80, H - 80) * 2
            L = rng.uniform(60, 150) * 2
            pts = [(x + i, y + math.sin(i / 38.0) * 6) for i in range(0, int(L), 8)]
            dr.line(pts, fill=(130, 100, 55, 90), width=3)
            dr.line([(px, py - 4) for px, py in pts], fill=(255, 245, 210, 110), width=2)
        for _ in range(200):
            x, y = rng.uniform(40, W - 40) * 2, rng.uniform(60, H - 60) * 2
            dr.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(110, 88, 52, 120))
    elif kind == "rock":
        for _ in range(16):                           # cracks
            x, y = rng.uniform(80, W - 80) * 2, rng.uniform(100, H - 100) * 2
            pts = [(x, y)]
            for _ in range(7):
                x += rng.uniform(-30, 30) * 2; y += rng.uniform(-6, 40) * 2
                pts.append((x, y))
            dr.line(pts, fill=(45, 42, 44, 150), width=4)
            dr.line([(a + 3, b + 3) for a, b in pts], fill=(235, 230, 220, 70), width=2)
    det = np.asarray(downsample(detail, (W, H)), np.float32) / 255.0
    rgb = rgb * (1 - det[..., 3:4]) + det[..., :3] * det[..., 3:4]

    d, nx, ny = hex_field(H, W, R_TOP)
    rgb = rgb + bevel(d, nx, ny, 9, 0.30)[..., None]
    rgb = rgb * (1 - 0.30 * np.clip(1 + d / 2.5, 0, 1) * (d < 0))[..., None]      # crisp dark outline
    out = np.concatenate([np.clip(rgb, 0, 1), aa(d)[..., None]], -1)
    return out


def earth_side():
    """Stone-and-earth hex used as the tile's lip (drawn offset below the top face)."""
    rng = np.random.default_rng(7)
    H, W = TILE_H, TILE_W
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    n = noise(H, W, 40, rng, 4)
    strata = 0.5 + 0.5 * np.sin(yy / 9.0 + n * 6)
    rgb = lerp3(0.6 * n + 0.4 * strata, hexcolor("#4a3524"), hexcolor("#6e5238"), hexcolor("#8d6d4b"))
    d, nx, ny = hex_field(H, W, R_TOP)
    rgb = rgb * (1 - 0.35 * np.clip(1 + d / 2.5, 0, 1) * (d < 0))[..., None]
    return np.concatenate([np.clip(rgb, 0, 1), aa(d)[..., None]], -1)


def shadow():
    H, W = TILE_H + 120, TILE_W + 120
    d, _, _ = hex_field(H, W, R_TOP)
    m = aa(d)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(22)), np.float32) / 255
    out = blank(H, W)
    out[..., :3] = hexcolor("#0b1020")
    out[..., 3] = m * 0.55
    return out


def overlays():
    H, W = TILE_H, TILE_W
    d, nx, ny = hex_field(H, W, R_TOP)
    inside = aa(d)
    # glaze: stronger toward the rim, so tinted ownership reads like a coloured border-glow
    glaze = blank(H, W); glaze[..., :3] = 1
    glaze[..., 3] = inside * (0.55 + 0.45 * np.clip(1 + d / 70.0, 0, 1))
    save("hex_fill", glaze, TILE_PPU)

    ring = blank(H, W); ring[..., :3] = 1
    dr, _, _ = hex_field(H, W, R_TOP - 10)
    band = aa(dr + 0) * (1 - aa(dr + 13))                     # 13 px band just inside the rim
    ring[..., 3] = np.clip(band, 0, 1)
    save("hex_ring", ring, TILE_PPU)

    glow = blank(H, W); glow[..., :3] = 1
    g = np.clip(1 + d / 55.0, 0, 1) * inside
    glow[..., 3] = g * 0.9
    save("hex_glow", glow, TILE_PPU)

    sel = blank(H, W); sel[..., :3] = 1
    o1 = aa(hex_field(H, W, R_TOP + 4)[0]) * (1 - aa(hex_field(H, W, R_TOP - 14)[0]))
    o2 = aa(hex_field(H, W, R_TOP - 24)[0]) * (1 - aa(hex_field(H, W, R_TOP - 31)[0]))
    sel[..., 3] = np.clip(o1 + 0.6 * o2, 0, 1)
    save("hex_select", sel, TILE_PPU)


def build():
    for i, k in enumerate(["grass_0", "grass_1", "grass_2", "forest", "sand", "rock"]):
        save("ground_" + k, ground(k, 100 + i), TILE_PPU)
    save("hex_side", earth_side(), TILE_PPU)
    save("hex_shadow", shadow(), TILE_PPU)
    overlays()


if __name__ == "__main__":
    build(); write_manifest()
