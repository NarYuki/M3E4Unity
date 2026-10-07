"""Make the static TTF instances that M3E4Unity ships.

TextMesh Pro reads only the default instance of a variable font, so every
weight / fill the Material 3 type scale and icon set needs is cut out here
with fontTools' instancer.

usage: python make_static_fonts.py <dir with the variable fonts> <output dir>
"""
import os
import sys
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

SRC, DST = sys.argv[1], sys.argv[2]
os.makedirs(DST, exist_ok=True)

# Material 3 uses three weights: Regular 400, Medium 500, Bold 700 (TypefaceTokens).
TEXT_WEIGHTS = {400: "Regular", 500: "Medium", 700: "Bold"}

JOBS = []
for w, name in TEXT_WEIGHTS.items():
    JOBS.append(("GoogleSansFlex[GRAD,ROND,opsz,slnt,wdth,wght].ttf",
                 f"GoogleSansFlex-{name}.ttf", {"wght": w}))
    JOBS.append(("RobotoFlex[GRAD,XOPQ,XTRA,YOPQ,YTAS,YTDE,YTFI,YTLC,YTUC,opsz,slnt,wdth,wght].ttf",
                 f"RobotoFlex-{name}.ttf", {"wght": w}))
    JOBS.append(("NotoSansJP[wght].ttf", f"NotoSansJP-{name}.ttf", {"wght": w}))

# Icons: optical size 24, weight 400, grade 0; FILL 0 (default) and 1 (selected state).
for style in ("Rounded", "Outlined", "Sharp"):
    for fill in (0, 1):
        JOBS.append((f"MaterialSymbols{style}[FILL,GRAD,opsz,wght].ttf",
                     f"MaterialSymbols{style}-Fill{fill}.ttf",
                     {"FILL": fill, "GRAD": 0, "opsz": 24, "wght": 400}))

only = set(sys.argv[3:])
for src, dst, loc in JOBS:
    if only and dst not in only:
        continue
    out = os.path.join(DST, dst)
    if os.path.exists(out):
        print("skip", dst)
        continue
    font = TTFont(os.path.join(SRC, src))
    axes = {a.axisTag: a.defaultValue for a in font["fvar"].axes}
    axes.update(loc)  # pin every axis so the result is fully static
    static = instancer.instantiateVariableFont(font, axes)
    # Name the instance ourselves (Roboto Flex's STAT table cannot describe every pinned axis).
    family, style = os.path.splitext(dst)[0].split("-", 1)
    name = static["name"]
    for nid in (16, 17, 25):
        name.removeNames(nameID=nid)
    name.setName(f"{family} {style}", 1, 3, 1, 0x409)
    name.setName("Regular", 2, 3, 1, 0x409)
    name.setName(f"{family} {style}", 4, 3, 1, 0x409)
    name.setName(f"{family}-{style}", 6, 3, 1, 0x409)
    name.setName(family, 16, 3, 1, 0x409)
    name.setName(style, 17, 3, 1, 0x409)
    static.save(out)
    print("wrote", dst, os.path.getsize(out))
