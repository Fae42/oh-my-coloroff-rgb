# Regenerate assets/banner.png (1280x640) for oh-my-coloroff-rgb.
# Pure PIL: continuous-hue fan ring with layered bloom, curved blades, supersampled x3.
# Run from repo root:  py -3 tools\gen_banner.py [out.png]   (default: assets/banner.png)
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import math, colorsys, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "assets", "banner.png")

SS = 3                      # supersample factor; downscale with LANCZOS at the end
W, H = 1280, 640
PW, PH = W * SS, H * SS
def S(v): return int(round(v * SS))

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

# ---------------- background: deep gradient + ambient glows ----------------
top, bot = (9, 12, 18), (20, 27, 39)
grad = Image.new("RGB", (1, PH))
for y in range(PH):
    grad.putpixel((0, y), lerp(top, bot, y / PH))
bg = grad.resize((PW, PH)).convert("RGBA")

amb = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
ad = ImageDraw.Draw(amb)
for (gx, gy, gr, col, al) in [
    (1050, 170, 300, (41, 171, 226), 60),
    (960, 500, 240, (255, 59, 92), 48),
    (850, 90, 200, (46, 204, 113), 40),
]:
    ad.ellipse([S(gx - gr), S(gy - gr), S(gx + gr), S(gy + gr)], fill=col + (al,))
amb = amb.filter(ImageFilter.GaussianBlur(S(70)))
bg = Image.alpha_composite(bg, amb)

# ---------------- fan: continuous-hue ring + bloom + curved blades ----------------
FX, FY, R_OUT, THICK = S(1020), S(320), S(190), S(34)
R_IN = R_OUT - THICK

def fade(im, f):
    a = im.split()[3].point(lambda v: int(v * f))
    im = im.copy(); im.putalpha(a); return im

# ring: 1440 annular sectors, hue rotates smoothly around the circle
ring = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
rd = ImageDraw.Draw(ring)
STEPS = 1440
for i in range(STEPS):
    a0 = 2 * math.pi * i / STEPS
    a1 = 2 * math.pi * (i + 1) / STEPS
    r, g, b = colorsys.hsv_to_rgb(i / STEPS, 0.82, 1.0)
    col = (int(r * 255), int(g * 255), int(b * 255), 255)
    pts = []
    for s2 in range(5):
        a = a0 + (a1 - a0) * s2 / 4
        pts.append((FX + R_OUT * math.cos(a), FY + R_OUT * math.sin(a)))
    for s2 in range(4, -1, -1):
        a = a0 + (a1 - a0) * s2 / 4
        pts.append((FX + R_IN * math.cos(a), FY + R_IN * math.sin(a)))
    rd.polygon(pts, fill=col)

# layered bloom: wide halo + tight glow + crisp core
bg = Image.alpha_composite(bg, fade(ring.filter(ImageFilter.GaussianBlur(S(26))), 0.55))
bg = Image.alpha_composite(bg, fade(ring.filter(ImageFilter.GaussianBlur(S(8))), 0.75))
bg = Image.alpha_composite(bg, ring)

# soft light well under the blades
well = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
wd = ImageDraw.Draw(well)
for i in range(40):
    t = i / 40
    rr = (R_IN + S(4)) * (1 - t)
    wd.ellipse([FX - rr, FY - rr, FX + rr, FY + rr], fill=(215, 226, 245, int(48 * (1 - t))))
well = well.filter(ImageFilter.GaussianBlur(S(6)))
bg = Image.alpha_composite(bg, well)
d = ImageDraw.Draw(bg, "RGBA")

# blades: swept-back curved polygons (leading edge bulges outward like a real PC fan)
def blade_points(k, n, r_hub, r_tip, sweep_deg=18, chord_deg=24, steps=24):
    a = 2 * math.pi * k / n
    leading, trailing = [], []
    for i in range(steps + 1):
        t = i / steps
        r = r_hub + t * (r_tip - r_hub)
        bend = math.radians(sweep_deg) * (t ** 1.4)
        half = math.radians(chord_deg) * (math.sin(math.pi * (0.10 + 0.80 * t)) ** 0.9) / 2 \
               * (r_tip / r) ** 0.25
        leading.append((FX + r * math.cos(a + bend + half), FY + r * math.sin(a + bend + half)))
        trailing.append((FX + r * math.cos(a + bend - half), FY + r * math.sin(a + bend - half)))
    return leading, trailing

for k in range(9):
    leading, trailing = blade_points(k, 9, S(50), S(138))
    d.polygon(leading + trailing[::-1], fill=(30, 37, 51, 245))

# hub + center LED dot (yellow = the tray icon's "lights on")
d.ellipse([FX - S(58), FY - S(58), FX + S(58), FY + S(58)], fill=(13, 17, 23, 255),
          outline=(96, 104, 118, 255), width=S(4))
d.ellipse([FX - S(40), FY - S(40), FX + S(40), FY + S(40)], outline=(58, 64, 74, 255), width=S(2))
dot = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
dd = ImageDraw.Draw(dot)
dd.ellipse([FX - S(18), FY - S(18), FX + S(18), FY + S(18)], fill=(255, 214, 51, 255))
bg = Image.alpha_composite(bg, fade(dot.filter(ImageFilter.GaussianBlur(S(10))), 0.8))
bg = Image.alpha_composite(bg, dot)

# ---------------- text ----------------
def load_font(bold, size):
    cands = [r"C:\Windows\Fonts\segoeuib.ttf", r"C:\Windows\Fonts\msyhbd.ttc"] if bold \
        else [r"C:\Windows\Fonts\segoeui.ttf", r"C:\Windows\Fonts\msyh.ttc"]
    for p in cands:
        if os.path.exists(p):
            return ImageFont.truetype(p, S(size))
    return ImageFont.load_default()

f_title = load_font(True, 84)
f_sub = load_font(False, 33)
# CJK glyphs live only in YaHei — Segoe UI renders tofu
f_cjk = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", S(30))
f_badge = load_font(False, 23)
d = ImageDraw.Draw(bg, "RGBA")

TX = S(70)
TITLE = "oh-my-coloroff-rgb"
# gradient title via alpha mask
tb = d.textbbox((0, 0), TITLE, font=f_title)
tw, th = tb[2] - tb[0] + S(8), tb[3] - tb[1] + S(16)
timg = Image.new("L", (tw, th), 0)
ImageDraw.Draw(timg).text((-tb[0] + S(4), -tb[1] + S(8)), TITLE, font=f_title, fill=255)
gradt = Image.new("RGBA", (tw, th))
gp = gradt.load()
cA, cB = (255, 59, 92), (41, 171, 226)
strip = Image.new("RGBA", (tw, 1))
for xx in range(tw):
    strip.putpixel((xx, 0), lerp(cA, cB, xx / max(1, tw)) + (255,))
gradt = strip.resize((tw, th))
bg.paste(gradt, (TX, S(148)), timg)

d.text((TX + S(4), S(272)), "Screen off \u2192 lights off. On \u2192 back.", font=f_sub, fill=(148, 157, 169, 255))
d.text((TX + S(4), S(336)), "七彩虹主板 RGB 风扇 · 息屏联动守护", font=f_cjk, fill=(203, 211, 220, 255))

# pill badges
bx = TX + S(4)
for b in ["iGC.Lite", "Windows", "C#", "tray"]:
    wpx = d.textlength(b, font=f_badge) + S(34)
    d.rounded_rectangle([bx, S(424), bx + wpx, S(424 + 46)], radius=S(23),
                        outline=(63, 70, 78, 255), width=S(2), fill=(22, 27, 34, 180))
    d.text((bx + S(17), S(424 + 9)), b, font=f_badge, fill=(160, 170, 180, 255))
    bx += int(wpx) + S(14)

# ---------------- vignette + grain + downscale ----------------
mask = Image.new("L", (PW, PH), 0)
ImageDraw.Draw(mask).ellipse([-PW * 0.3, -PH * 0.5, PW * 1.3, PH * 1.5], fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(S(90)))
bg = Image.composite(bg, Image.new("RGBA", (PW, PH), (4, 6, 10, 255)), mask)

grain = Image.effect_noise((PW, PH), 24).convert("L")
g3 = Image.merge("RGB", (grain, grain, grain))
bg_rgb = bg.convert("RGB")
bg_rgb = Image.blend(bg_rgb, ImageChops.overlay(bg_rgb, g3), 0.08)

bg_rgb.resize((W, H), Image.LANCZOS).save(OUT)
print("saved", OUT)
