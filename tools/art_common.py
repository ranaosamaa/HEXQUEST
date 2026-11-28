"""Shared helpers for the HexQuest art generator (Pillow + numpy, no external assets)."""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art")
FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")
os.makedirs(OUT, exist_ok=True)

SQ3 = math.sqrt(3.0)
TILE_W, TILE_H = 512, 592                  # one pointy-top hex, circumradius 1 world unit
TILE_R = TILE_H / 2.0
TILE_PPU = TILE_W / SQ3                    # pixels per world unit for everything in the world
UI_PPU = 200.0                             # UI sprites are authored at 2x for crisp 4K

MANIFEST = []


def hexcolor(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


TOOL_FONTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts")


def font(name, size):
    for d in (FONTS, TOOL_FONTS):
        p = os.path.join(d, name)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    raise FileNotFoundError(name)


# ----------------------------------------------------------------------------- saving

def to_pil(arr):
    a = np.clip(arr, 0, 1)
    return Image.fromarray((a * 255 + 0.5).astype(np.uint8), "RGBA")


def save(name, img, ppu, pivot=(0.5, 0.5), border=None):
    """img: float RGBA array (straight alpha) or a PIL RGBA image. border = (l, b, r, t) in pixels."""
    if isinstance(img, np.ndarray):
        img = to_pil(img)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    MANIFEST.append({"name": name, "ppu": float(ppu), "px": pivot[0], "py": pivot[1],
                     "border": list(border) if border else [0, 0, 0, 0],
                     "w": img.width, "h": img.height})


def write_manifest():
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump({"sprites": MANIFEST}, f, indent=0)


# ----------------------------------------------------------------------------- arrays

def blank(h, w):
    return np.zeros((h, w, 4), np.float32)


def over(dst, src):
    """Alpha-composite src over dst (both float RGBA, straight alpha), in place."""
    sa = src[..., 3:4]
    da = dst[..., 3:4]
    oa = sa + da * (1 - sa)
    rgb = (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
    dst[..., :3] = rgb
    dst[..., 3:4] = oa
    return dst


def paint(dst, mask, rgb, opacity=1.0, box=None):
    """Composite colour `rgb` (3-vector or HxWx3) through `mask` (HxW 0..1). box=(x0,y0) for sub-canvases."""
    h, w = mask.shape
    x0, y0 = box if box else (0, 0)
    H, W = dst.shape[:2]
    xs0, ys0 = max(0, x0), max(0, y0)
    xs1, ys1 = min(W, x0 + w), min(H, y0 + h)
    if xs1 <= xs0 or ys1 <= ys0:
        return
    m = mask[ys0 - y0:ys1 - y0, xs0 - x0:xs1 - x0]
    if np.ndim(rgb) == 3:
        c = rgb[ys0 - y0:ys1 - y0, xs0 - x0:xs1 - x0]
    else:
        c = np.broadcast_to(np.asarray(rgb, np.float32), m.shape + (3,))
    layer = np.concatenate([c, (m * opacity)[..., None]], axis=-1)
    dst[ys0:ys1, xs0:xs1] = over(dst[ys0:ys1, xs0:xs1], layer)


def noise(h, w, scale, rng, octaves=4, persistence=0.5):
    """Smooth value noise in 0..1."""
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        s = max(2.0, scale / (2 ** o))
        gh, gw = int(h / s) + 3, int(w / s) + 3
        g = Image.fromarray(rng.random((gh, gw)).astype(np.float32), "F")
        g = g.resize((int(gw * s), int(gh * s)), Image.BICUBIC)
        out += amp * np.asarray(g, np.float32)[:h, :w]
        tot += amp
        amp *= persistence
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def tileable_noise(n, beta, rng):
    """Seamlessly tiling n x n noise (FFT, power-law spectrum), 0..1."""
    fx = np.fft.fftfreq(n)[:, None]
    fy = np.fft.fftfreq(n)[None, :]
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1.0
    spec = (rng.normal(size=(n, n)) + 1j * rng.normal(size=(n, n))) / (f ** beta)
    spec[0, 0] = 0
    a = np.real(np.fft.ifft2(spec)).astype(np.float32)
    return (a - a.min()) / (a.max() - a.min() + 1e-6)


def lerp3(t, c0, c1, c2):
    """Three-stop gradient over t in 0..1 -> HxWx3."""
    t = t[..., None]
    lo = c0 + (c1 - c0) * np.clip(t * 2, 0, 1)
    hi = c1 + (c2 - c1) * np.clip(t * 2 - 1, 0, 1)
    return np.where(t < 0.5, lo, hi)


# ----------------------------------------------------------------------------- hex geometry

_N = np.array([[1.0, 0.0], [0.5, SQ3 / 2], [-0.5, SQ3 / 2]], np.float32)


def hex_field(h, w, R):
    """Signed distance (px, negative inside) to a pointy-top hex of circumradius R centred in HxW,
    plus the outward unit normal of the nearest edge (image coords, y down)."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    x, y = xx + 0.5 - w / 2.0, yy + 0.5 - h / 2.0
    p = np.stack([x * _N[k, 0] + y * _N[k, 1] for k in range(3)], 0)
    k = np.argmax(np.abs(p), axis=0)
    val = np.take_along_axis(p, k[None], 0)[0]
    sign = np.sign(val)
    sign[sign == 0] = 1
    d = np.abs(val) - R * SQ3 / 2
    nx = sign * _N[k, 0]
    ny = sign * _N[k, 1]
    return d, nx, ny


def aa(d):
    """Analytic anti-aliased coverage from a signed distance."""
    return np.clip(0.5 - d, 0, 1).astype(np.float32)


LIGHT = np.array([-0.55, -0.83], np.float32)   # light comes from the top-left


def bevel(d, nx, ny, width, strength):
    """Light/dark rim just inside the edge."""
    t = np.clip(1 - (-d) / width, 0, 1) * (d < 0)
    facing = nx * LIGHT[0] + ny * LIGHT[1]
    return t * facing * strength


# ----------------------------------------------------------------------------- shapes for decorations

def poly_mask(points, size, ss=4):
    """Anti-aliased polygon mask (supersampled). points in px; size = (w, h)."""
    w, h = size
    im = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(im).polygon([(x * ss, y * ss) for x, y in points], fill=255)
    im = im.resize((w, h), Image.LANCZOS)
    return np.asarray(im, np.float32) / 255.0


def ellipse_mask(cx, cy, rx, ry, size, ss=4):
    w, h = size
    im = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(im).ellipse([(cx - rx) * ss, (cy - ry) * ss, (cx + rx) * ss, (cy + ry) * ss], fill=255)
    return np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255.0


def shade_grad(size, c_light, c_dark, direction=(-0.5, -0.85), lo=None, hi=None):
    """Linear gradient from c_light (toward `direction`) to c_dark; returns HxWx3."""
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = (xx - w / 2) * direction[0] + (yy - h / 2) * direction[1]
    lo = -max(w, h) / 2 if lo is None else lo
    hi = max(w, h) / 2 if hi is None else hi
    t = np.clip((t - lo) / (hi - lo), 0, 1)[..., None]       # 0 = dark side ... 1 = lit side
    return c_dark + (c_light - c_dark) * t


def soft_shadow(canvas, cx, cy, rx, ry, strength=0.35):
    h, w = canvas.shape[:2]
    m = ellipse_mask(cx, cy, rx, ry, (w, h))
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(max(2, ry * 0.35))),
                   np.float32) / 255.0
    paint(canvas, m, np.array([0.05, 0.07, 0.1], np.float32), strength)


def downsample(img, size):
    """Downsample a PIL RGBA image without dark fringes (premultiplied resize)."""
    return img.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")
