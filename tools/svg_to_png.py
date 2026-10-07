"""Rasterise the simple SVGs written by Tests/CoreTests (M/C/Z paths, text) with PIL.

usage: python svg_to_png.py in.svg out.png [scale]
"""
import re
import sys
from PIL import Image, ImageDraw

src, dst = sys.argv[1], sys.argv[2]
scale = float(sys.argv[3]) if len(sys.argv) > 3 else 2.0
svg = open(src, encoding="utf-8").read()
w = float(re.search(r"width='([\d.]+)'", svg).group(1))
h = float(re.search(r"height='([\d.]+)'", svg).group(1))
img = Image.new("RGB", (int(w * scale), int(h * scale)), "white")
d = ImageDraw.Draw(img)


def flatten(path):
    pts = []
    toks = re.findall(r"[MCZ]|-?[\d.]+(?:e-?\d+)?", path)
    i, cur = 0, (0.0, 0.0)
    while i < len(toks):
        t = toks[i]
        if t == "M":
            cur = (float(toks[i + 1]), float(toks[i + 2]))
            pts.append(cur)
            i += 3
        elif t == "C":
            c = [float(x) for x in toks[i + 1:i + 7]]
            p0, p1, p2, p3 = cur, (c[0], c[1]), (c[2], c[3]), (c[4], c[5])
            for k in range(1, 17):
                s = k / 16
                u = 1 - s
                pts.append((u ** 3 * p0[0] + 3 * u * u * s * p1[0] + 3 * u * s * s * p2[0] + s ** 3 * p3[0],
                            u ** 3 * p0[1] + 3 * u * u * s * p1[1] + 3 * u * s * s * p2[1] + s ** 3 * p3[1]))
            cur = p3
            i += 7
        else:
            i += 1
    return [(x * scale, y * scale) for x, y in pts]


for m in re.finditer(r"<path d='([^']*)' fill='([^']*)'/>", svg):
    d.polygon(flatten(m.group(1)), fill=m.group(2))
for m in re.finditer(r"<text x='([\d.]+)' y='([\d.]+)'[^>]*>([^<]*)</text>", svg):
    d.text((float(m.group(1)) * scale, float(m.group(2)) * scale - 10), m.group(3), fill="black")
img.save(dst)
