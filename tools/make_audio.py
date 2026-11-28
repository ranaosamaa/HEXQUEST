"""Synthesises every sound effect and two seamless music loops (numpy only, no samples used)."""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
os.makedirs(OUT, exist_ok=True)
SR = 44100
rng = np.random.default_rng(2024)


def save(name, x, peak=0.8, sr=SR):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def t_(sec): return np.arange(int(SR * sec)) / SR
def midi(n): return 440.0 * 2 ** ((n - 69) / 12)
def env(n, a=0.005, d=0.2, s=0.0, r=0.1):
    """ADSR-ish envelope over n samples."""
    e = np.ones(n)
    ia, idc, ir = int(a * SR), int(d * SR), int(r * SR)
    e[:ia] = np.linspace(0, 1, max(1, ia))
    if idc:
        e[ia:ia + idc] = np.linspace(1, s, idc)[: max(0, n - ia)]
    e[ia + idc:] = s
    if ir:
        e[-ir:] *= np.linspace(1, 0, ir)
    return e


def expdec(n, tau): return np.exp(-np.arange(n) / (tau * SR))
def lowpass(x, fc):
    from scipy.signal import butter, lfilter
    b, a = butter(2, min(0.99, fc / (SR / 2)), "low"); return lfilter(b, a, x)
def highpass(x, fc):
    from scipy.signal import butter, lfilter
    b, a = butter(2, min(0.99, fc / (SR / 2)), "high"); return lfilter(b, a, x)


def bell(f, dur, bright=1.0):
    t = t_(dur)
    s = sum(a * np.sin(2 * np.pi * f * k * t) * np.exp(-t / (dur * d)) for k, a, d in
            [(1, 1.0, 0.45), (2.01, 0.5 * bright, 0.30), (2.76, 0.35 * bright, 0.22), (4.07, 0.18 * bright, 0.14), (5.4, 0.1 * bright, 0.10)])
    return s * np.minimum(1, t / 0.003)


def pluck(f, dur):
    t = t_(dur)
    s = sum(np.sin(2 * np.pi * f * k * t) / k ** 1.3 * np.exp(-t * (3 + k * 2.2)) for k in range(1, 8))
    return s * np.minimum(1, t / 0.002)


def reverb_ir(sec=1.6, wet_lp=5000):
    n = int(SR * sec)
    ir = rng.normal(size=n) * np.exp(-np.arange(n) / (0.35 * SR))
    ir = lowpass(ir, wet_lp)
    ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
    return ir / np.sqrt(np.sum(ir ** 2))


def reverb(x, mix=0.3, sec=1.6, circular=False):
    ir = reverb_ir(sec)
    if circular:
        n = len(x)
        irp = np.zeros(n); irp[: min(n, len(ir))] = ir[:n]
        wet = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(irp)))
    else:
        from scipy.signal import fftconvolve
        wet = fftconvolve(x, ir)[: len(x) + len(ir)]
        x = np.pad(x, (0, len(wet) - len(x)))
    return x * (1 - mix) + wet * mix * 2.2


# ----------------------------------------------------------------------------- effects

def sfx():
    # UI
    n = int(0.07 * SR); t = t_(0.07)
    save("ui_click", (lowpass(rng.normal(size=n), 3500) * expdec(n, 0.008) * 0.9 + np.sin(2 * np.pi * 620 * t) * expdec(n, 0.02) * 0.5), 0.6)
    n = int(0.04 * SR)
    save("ui_hover", highpass(rng.normal(size=n), 2500) * expdec(n, 0.006) * 0.5 + np.sin(2 * np.pi * 1100 * t_(0.04)) * expdec(n, 0.01) * 0.2, 0.25)
    t = t_(0.18)
    save("ui_back", np.sin(2 * np.pi * (520 - 700 * t) * t) * expdec(len(t), 0.07), 0.5)
    # gameplay
    for name, k in [("move", 0), ("move2", 1)]:
        n = int(0.22 * SR)
        thud = lowpass(rng.normal(size=n), 450 + 120 * k) * expdec(n, 0.045)
        step2 = np.roll(thud, int(0.11 * SR)) * 0.7
        step2[: int(0.11 * SR)] = 0
        save(name, thud + step2, 0.55)
    x = np.zeros(int(1.0 * SR))
    for i, m in enumerate([60, 64, 67]):
        b = bell(midi(m), 0.9) * 0.7
        x[int(i * 0.06 * SR): int(i * 0.06 * SR) + len(b)] += b[: len(x) - int(i * 0.06 * SR)]
    save("claim", reverb(x, 0.2, 0.9), 0.7)
    n = int(0.5 * SR); t = t_(0.5)
    clank = sum(np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, d in [(180, 9), (467, 14), (980, 22), (1630, 30)])
    save("reinforce", clank * 0.6 + lowpass(rng.normal(size=n), 900) * expdec(n, 0.03) * 0.4, 0.7)
    n = int(0.55 * SR); t = t_(0.55)
    sweep = highpass(rng.normal(size=n), 1200) * np.sin(np.pi * np.minimum(1, t / 0.28)) * (t < 0.3)
    thud = np.sin(2 * np.pi * (120 - 80 * t) * t) * expdec(n, 0.12) * (t > 0.26)
    thud = np.roll(np.sin(2 * np.pi * (120 - 60 * t) * t) * expdec(n, 0.12), int(0.26 * SR)) * (t > 0.26)
    save("attack", sweep * 0.5 + thud * 1.0 + lowpass(rng.normal(size=n), 700) * expdec(n, 0.05) * np.roll((t > 0), 0) * (t > 0.26) * 0.4, 0.85)
    t = t_(0.4)
    save("block", sum(np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, d in [(300, 12), (710, 18), (1330, 28)]) * 0.7, 0.7)
    x = np.zeros(int(0.9 * SR))
    for i, (d, f) in enumerate([(0.0, 2093), (0.07, 2637), (0.13, 3136), (0.22, 2637), (0.3, 3520)]):
        b = bell(f, 0.35, 0.4) * 0.5
        s0 = int(d * SR); x[s0:s0 + len(b)] += b[: len(x) - s0]
    save("gather", x, 0.6)
    x = np.zeros(int(1.2 * SR))
    for i, m in enumerate([72, 76, 79, 84]):
        b = bell(midi(m), 0.8, 1.2) * 0.6
        s0 = int(i * 0.1 * SR); x[s0:s0 + len(b)] += b[: len(x) - s0]
    save("upgrade", reverb(x, 0.25, 1.1), 0.7)
    save("pass", reverb(bell(midi(57), 0.9), 0.3, 0.8), 0.5)
    x = np.zeros(int(1.0 * SR))
    for i, m in enumerate([76, 83]):
        b = bell(midi(m), 0.7) * 0.7
        s0 = int(i * 0.15 * SR); x[s0:s0 + len(b)] += b[: len(x) - s0]
    save("turn", reverb(x, 0.25, 0.9), 0.55)
    t = t_(0.28)
    save("error", (np.sign(np.sin(2 * np.pi * 110 * t)) * 0.5 + np.sin(2 * np.pi * 95 * t)) * expdec(len(t), 0.12), 0.45)
    # jingles
    def jingle(notes, step, dur, bright):
        x = np.zeros(int((step * len(notes) + dur + 0.5) * SR))
        for i, m in enumerate(notes):
            b = bell(midi(m), dur, bright) * 0.7
            s0 = int(i * step * SR); x[s0:s0 + len(b)] += b[: len(x) - s0]
        return reverb(x, 0.35, 1.8)
    save("victory", jingle([60, 64, 67, 72, 76, 79, 84], 0.14, 1.8, 1.0), 0.8)
    save("defeat", jingle([69, 65, 62, 57], 0.32, 2.0, 0.3), 0.7)


# ----------------------------------------------------------------------------- music (seamless loops)

def add_wrap(buf, start, sig):
    n = len(buf)
    idx = (start + np.arange(len(sig))) % n
    np.add.at(buf, idx, sig)


def pad_note(f, dur):
    t = t_(dur)
    s = sum(a * np.sin(2 * np.pi * f * k * (1 + dt) * t + ph) for k, a, dt, ph in
            [(1, 1.0, 0.0, 0), (1, 0.6, 0.003, 1), (2, 0.35, -0.002, 2), (3, 0.12, 0.001, 0.5), (0.5, 0.45, 0.0, 0)])
    e = np.minimum(1, t / 0.9) * np.minimum(1, (dur - t) / 1.2)
    return s * np.clip(e, 0, 1)


def music(name, bpm, chords, scale, melody_density, perc, seed, peak=0.55):
    r = np.random.default_rng(seed)
    beat = 60.0 / bpm
    bar = 4 * beat
    total = bar * len(chords)
    n = int(total * SR)
    buf = np.zeros(n)
    for i, (root, quality) in enumerate(chords):
        third = 3 if quality == "m" else 4
        notes = [root, root + third, root + 7, root + 12]
        for m in notes:
            sig = pad_note(midi(m - 12), bar * 1.25) * 0.20
            add_wrap(buf, int(i * bar * SR), sig)
        add_wrap(buf, int(i * bar * SR), pad_note(midi(root - 24), bar * 1.25) * 0.28)
        # arpeggio pluck
        for k in range(8):
            if r.random() < melody_density:
                m = notes[r.integers(0, 4)] + 12 * r.integers(0, 2)
                add_wrap(buf, int((i * bar + k * beat / 2) * SR), pluck(midi(m), 1.4) * 0.12)
        # sparse melody bell from the scale
        for k in range(4):
            if r.random() < melody_density * 0.55:
                m = root + scale[r.integers(0, len(scale))] + 12
                add_wrap(buf, int((i * bar + k * beat + r.choice([0, beat / 2])) * SR), bell(midi(m + 12), 2.2, 0.5) * 0.12)
        if perc:
            for k in range(4):
                tt = t_(0.25)
                kick = np.sin(2 * np.pi * (95 - 190 * tt) * tt) * expdec(len(tt), 0.07) if k in (0, 2) else None
                if kick is not None:
                    add_wrap(buf, int((i * bar + k * beat) * SR), kick * 0.5)
            for k in range(8):
                nz = highpass(r.normal(size=int(0.05 * SR)), 5000) * expdec(int(0.05 * SR), 0.012)
                add_wrap(buf, int((i * bar + k * beat / 2) * SR), nz * (0.07 if k % 2 else 0.04))
    buf = reverb(buf, 0.35, 2.2, circular=True)
    # gentle wind bed (periodic via circular filtering)
    wind = lowpass(r.normal(size=n), 500)
    wind = np.real(np.fft.ifft(np.fft.fft(wind) * np.fft.fft(np.pad(np.ones(2000) / 2000, (0, n - 2000)))))
    buf = buf + wind * 0.06 * (0.6 + 0.4 * np.sin(2 * np.pi * np.arange(n) / n * 3))
    save(name, buf, peak)


def build():
    sfx()
    music("music_menu", 72, [(57, "m"), (53, "M"), (60, "M"), (55, "M"), (57, "m"), (53, "M"), (55, "M"), (52, "m")],
          [0, 3, 5, 7, 10], 0.55, False, 1)
    music("music_game", 96, [(50, "m"), (46, "M"), (53, "M"), (48, "M"), (50, "m"), (46, "M"), (48, "M"), (45, "M")],
          [0, 2, 3, 7, 9], 0.7, True, 2, 0.5)


if __name__ == "__main__":
    build()
