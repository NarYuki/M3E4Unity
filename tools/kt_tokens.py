"""Translate androidx Compose Material3 token objects (tokens/*.kt) into C#.

Every `internal object XxxTokens { val Name = value }` becomes a
`public static class XxxTokens` with the same member names, so component
code can cite the exact upstream token it uses.

usage: python kt_tokens.py <androidx tokens dir> <output .cs file> [--report]
"""
import os
import re
import sys

SRC, OUT = sys.argv[1], sys.argv[2]
REPORT = "--report" in sys.argv

# Files that are not plain value tables; they are ported by hand in Runtime/Core/Tokens.
SKIP = {"TypographyTokens.kt", "ColorSchemeKeyTokens.kt", "ShapeKeyTokens.kt",
        "TypographyKeyTokens.kt", "MotionSchemeKeyTokens.kt", "TypefaceTokens.kt"}

ROLE_REFS = {
    "ColorSchemeKeyTokens": "ColorRole",
    "ShapeKeyTokens": "ShapeRole",
    "TypographyKeyTokens": "TypeRole",
    "MotionSchemeKeyTokens": "MotionRole",
}

FLOAT = r"-?\d+(?:\.\d+)?"


def translate(expr):
    """returns (csharp type, csharp expression) or None"""
    e = expr.strip()
    m = re.fullmatch(rf"({FLOAT})\.(dp|sp)", e)
    if m:
        return "float", f"{float(m.group(1))!r}f".replace(".0f", "f") if float(m.group(1)).is_integer() else f"{m.group(1)}f"
    m = re.fullmatch(rf"\(?({FLOAT})\)?\.dp\s*\*\s*({FLOAT})f?", e)
    if m:
        return "float", f"{m.group(1)}f * {m.group(2)}f"
    m = re.fullmatch(rf"({FLOAT})f", e)
    if m:
        return "float", f"{m.group(1)}f"
    m = re.fullmatch(rf"({FLOAT})", e)
    if m:
        return ("float", f"{m.group(1)}f") if "." in m.group(1) else ("int", m.group(1))
    m = re.fullmatch(r"(\d+)L", e)
    if m:
        return "long", f"{m.group(1)}L"
    m = re.fullmatch(r"Color\(\s*(0x[0-9A-Fa-f]{8})\s*\)", e)
    if m:
        return "uint", f"{m.group(1)}u"
    m = re.fullmatch(r"(\w+Tokens)\.(\w+)", e)
    if m:
        owner, name = m.groups()
        if owner in ROLE_REFS:
            role = ROLE_REFS[owner]
            return role, f"{role}.{name}"
        if owner == "TypefaceTokens" and name in ("Brand", "Plain"):
            return "FontRole", f"FontRole.{name}"
        if owner == "TypefaceTokens":
            return "int", {"WeightBold": "700", "WeightMedium": "500", "WeightRegular": "400"}.get(name, None) or None
        return None, f"{owner}.{name}"  # type resolved in a second pass
    m = re.fullmatch(r"CubicBezierEasing\(\s*([^)]*)\)", e)
    if m:
        args = [a.strip().rstrip("f") + "f" for a in m.group(1).split(",")]
        return "CubicBezier", f"new CubicBezier({', '.join(args)})"
    m = re.fullmatch(r"FontWeight\.(\w+)", e)
    if m:
        return "int", {"Bold": "700", "Medium": "500", "Normal": "400", "SemiBold": "600"}[m.group(1)]
    m = re.fullmatch(r"FontWeight\((\d+)\)", e)
    if m:
        return "int", m.group(1)
    m = re.fullmatch(r"Color\(\s*red\s*=\s*(\d+),\s*green\s*=\s*(\d+),\s*blue\s*=\s*(\d+)\s*\)", e)
    if m:
        r, g, b = (int(x) for x in m.groups())
        return "uint", f"0xFF{r:02X}{g:02X}{b:02X}u"
    m = re.fullmatch(rf"CornerSize\(({FLOAT})\.dp\)", e)
    if m:
        return "float", f"{m.group(1)}f"
    m = re.fullmatch(rf"RoundedCornerShape\(({FLOAT})\.dp\)", e)
    if m:
        return "CornerShape", f"new CornerShape({m.group(1)}f)"
    m = re.fullmatch(rf"RoundedCornerShape\(\s*topStart\s*=\s*({FLOAT})\.dp,\s*topEnd\s*=\s*({FLOAT})\.dp,\s*bottomEnd\s*=\s*({FLOAT})\.dp,\s*bottomStart\s*=\s*({FLOAT})\.dp,?\s*\)", e)
    if m:
        return "CornerShape", "new CornerShape(" + ", ".join(f"{x}f" for x in m.groups()) + ")"
    if e == "CircleShape":
        return "CornerShape", "CornerShape.Full"
    if e == "RectangleShape":
        return "CornerShape", "CornerShape.None"
    m = re.fullmatch(r"([A-Z]\w*)", e)
    if m:
        return None, f"__SELF__.{e}"
    m = re.fullmatch(r"(true|false)", e)
    if m:
        return "bool", e
    return None


def parse(path):
    text = open(path, encoding="utf-8").read()
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    m = re.search(r"internal object (\w+)\s*\{(.*)\}\s*$", text, re.S)
    if not m:
        return None, []
    obj, body = m.group(1), m.group(2)
    entries = []
    # inline val Name: Type\n get() = expr    |   val Name = expr    |  const val Name = expr
    for mm in re.finditer(
        r"(?:inline\s+|const\s+)?val\s+(\w+)\s*(?::\s*[\w.<>]+)?\s*(?:get\(\)\s*=|=)\s*(.+?)(?=\n\s*(?:inline\s+|const\s+)?val\s|\Z)",
        body, re.S):
        name, expr = mm.group(1), " ".join(mm.group(2).split())
        expr = re.sub(r"\n\s*get\(\)\s*=\s*", "", expr)
        expr = expr.replace("get() = ", "").strip()
        entries.append((name, expr))
    return obj, entries


def main():
    objects = {}
    unknown = []
    for name in sorted(os.listdir(SRC)):
        if not name.endswith(".kt") or name in SKIP:
            continue
        obj, entries = parse(os.path.join(SRC, name))
        if not obj:
            continue
        objects[obj] = entries

    # resolve types: first pass for literal values, then references
    types = {}
    values = {}
    for obj, entries in objects.items():
        for n, e in entries:
            t = translate(e)
            if t is None:
                unknown.append(f"{obj}.{n} = {e}")
                continue
            if t[1] is None:
                unknown.append(f"{obj}.{n} = {e}")
                continue
            t = (t[0], t[1].replace("__SELF__", obj))
            types[f"{obj}.{n}"], values[f"{obj}.{n}"] = t
    for _ in range(5):
        for k, v in values.items():
            if types[k] is None:
                types[k] = types.get(v)
    lines = [
        "// <auto-generated> by tools/kt_tokens.py from androidx compose/material3 tokens/*.kt.",
        "// Copyright 2024-2025 The Android Open Source Project, Apache License 2.0.",
        "// Do not edit by hand; rerun the script against a newer androidx checkout instead.",
        "// Dimensions are in dp (1 dp = 1 canvas unit at the M3E4Unity reference scale).",
        "namespace M3E4Unity.Tokens",
        "{",
    ]
    # key-token objects become enums whose values match the upstream ids
    for file_name, enum in (("ColorSchemeKeyTokens.kt", "ColorRole"), ("ShapeKeyTokens.kt", "ShapeRole"),
                            ("TypographyKeyTokens.kt", "TypeRole"), ("MotionSchemeKeyTokens.kt", "MotionRole")):
        text = open(os.path.join(SRC, file_name), encoding="utf-8").read()
        members = re.findall(r"val (\w+) = \w+Token\((\d+)\)", text)
        lines.append(f"    /// <summary>{file_name[:-3]} (ids match upstream).</summary>")
        lines.append(f"    public enum {enum}")
        lines.append("    {")
        lines.extend(f"        {n} = {i}," for n, i in members)
        lines.append("    }")
        lines.append("")
    lines.append("    public enum FontRole { Brand, Plain }")
    lines.append("")
    # role -> value lookups, following the naming convention of the token objects
    type_roles = re.findall(r"val (\w+) = \w+Token\(\d+\)", open(os.path.join(SRC, "TypographyKeyTokens.kt"), encoding="utf-8").read())
    shape_roles = re.findall(r"val (\w+) = \w+Token\(\d+\)", open(os.path.join(SRC, "ShapeKeyTokens.kt"), encoding="utf-8").read())
    lines.append("    /// <summary>TypeRole -> TypeScaleTokens entry.</summary>")
    lines.append("    public static class TypeScale")
    lines.append("    {")
    lines.append("        public static TypeStyle Get(TypeRole role)")
    lines.append("        {")
    lines.append("            switch (role)")
    lines.append("            {")
    for r in type_roles:
        lines.append(f"                case TypeRole.{r}: return new TypeStyle(TypeScaleTokens.{r}Font, TypeScaleTokens.{r}Weight, "
                     f"TypeScaleTokens.{r}Size, TypeScaleTokens.{r}LineHeight, TypeScaleTokens.{r}Tracking);")
    lines.append("                default: throw new System.ArgumentOutOfRangeException(nameof(role));")
    lines.append("            }")
    lines.append("        }")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>ShapeRole -> ShapeTokens entry.</summary>")
    lines.append("    public static class ShapeScale")
    lines.append("    {")
    lines.append("        public static CornerShape Get(ShapeRole role)")
    lines.append("        {")
    lines.append("            switch (role)")
    lines.append("            {")
    for r in shape_roles:
        lines.append(f"                case ShapeRole.{r}: return ShapeTokens.{r};")
    lines.append("                default: throw new System.ArgumentOutOfRangeException(nameof(role));")
    lines.append("            }")
    lines.append("        }")
    lines.append("    }")
    lines.append("")
    for obj, entries in objects.items():
        body = []
        for n, e in entries:
            key = f"{obj}.{n}"
            if key not in values or types.get(key) is None:
                body.append(f"        // {n} = {e}  (not translated)")
                if key in values:
                    unknown.append(f"{key} = {e} (unresolved type)")
                continue
            t, v = types[key], values[key]
            const = t in ("float", "int", "long", "uint", "bool", "FontRole", "ColorRole", "ShapeRole", "TypeRole", "MotionRole")
            mod = "const" if const else "static readonly"
            body.append(f"        public {mod} {t} {n} = {v};")
        lines.append(f"    public static class {obj}")
        lines.append("    {")
        lines.extend(body)
        lines.append("    }")
        lines.append("")
    lines.append("}")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")
    print(f"{len(objects)} token objects, {sum(len(v) for v in objects.values())} tokens, {len(unknown)} untranslated")
    if REPORT:
        for u in unknown:
            print("  ", u)


if __name__ == "__main__":
    main()
