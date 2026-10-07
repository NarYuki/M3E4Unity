"""Hand-made fixes applied on top of java2cs.py output.

Each entry is an exact (old, new) replacement inside one generated file. A
fixup that no longer matches is reported, so upstream changes surface
instead of being silently skipped.

usage: python java2cs_fixups.py <generated dir>
"""
import os
import re
import sys

ROOT = sys.argv[1]
RE = "regex"

FIX = {
    "quantize/QuantizerWsmeans.cs": [
        (": Comparable<Distance>", ": IComparable<Distance>"),
        ("int pixelCount = pixelToCount.get(inputPixel);\n      if (pixelCount == null) {",
         "if (!pixelToCount.TryGetValue(inputPixel, out int pixelCount)) {"),
        ("public int compareTo(Distance other) {\n      return ((double) this.distance).compareTo(other.distance);",
         "public int CompareTo(Distance other) {\n      return this.distance.CompareTo(other.distance);"),
    ],
    "score/Score.cs": [
        ("internal class ScoredComparator : Comparator<ScoredHCT>", "internal class ScoredComparator : IComparer<ScoredHCT>"),
        ("public int compare(ScoredHCT entry1, ScoredHCT entry2) {\n      return double.compare(entry2.score, entry1.score);",
         "public int Compare(ScoredHCT entry1, ScoredHCT entry2) {\n      return entry2.score.CompareTo(entry1.score);"),
    ],
    # Java's Double is nullable; opacity uses null for "opaque".
    "dynamiccolor/DynamicColor.cs": [
        (RE, r"Func<DynamicScheme, double> opacity", "Func<DynamicScheme, double?> opacity"),
        ("double percentage = opacity(scheme);", "double percentage = opacity(scheme).Value;"),
        ("return tone != null ? tone(s) : null;", "return tone(s);"),
    ],
    "dynamiccolor/DynamicScheme.cs": [
        (RE, r"public string toString\(\) \{.*?\n  \}\n",
         "public override string ToString() {\n"
         "    return string.Format(System.Globalization.CultureInfo.InvariantCulture,\n"
         "        \"Scheme: variant={0}, mode={1}, platform={2}, contrastLevel={3:0.0}, seed={4}, {5}specVersion={6}\",\n"
         "        variant, isDark ? \"dark\" : \"light\", platform.ToString().ToLowerInvariant(), contrastLevel,\n"
         "        sourceColorHct,\n"
         "        sourceColorHctList.Count <= 1 ? \"\" : \"sourceColorHctList=[\" + string.Join(\", \", sourceColorHctList) + \"], \",\n"
         "        specVersion);\n"
         "  }\n"),
    ],
    "dynamiccolor/MaterialDynamicColors.cs": [
        ("return Arrays.asList(", "return Arrays.asList<Func<DynamicColor>>("),
    ],
    "quantize/QuantizerMap.cs": [
        ("int currentPixelCount = pixelByCount.get(pixel);\n      int newPixelCount = currentPixelCount == null ? 1 : currentPixelCount + 1;",
         "int newPixelCount = pixelByCount.TryGetValue(pixel, out int currentPixelCount) ? currentPixelCount + 1 : 1;"),
    ],
    "palettes/TonalPalette.cs": [
        # Map<Integer, Integer>.get returns null when missing; C# value types cannot.
        ("int color = cache.get(tone);\n    if (color == null) {", "if (!cache.TryGetValue(tone, out int color)) {"),
        ("if (chromaCache.get(tone) == null) {", "if (!chromaCache.ContainsKey(tone)) {"),
        ("if (newChroma != null) {", "{"),
        ("return Hct.fromInt(tone(99));", "return Hct.fromInt(this.tone(99));"),
    ],
    "quantize/QuantizerWu.cs": [
        ("private static enum Direction", "internal enum Direction"),
        ("int volume = volume(cube, weights);\n    return xx - hypotenuse / ((double) volume);",
         "int vol = volume(cube, weights);\n    return xx - hypotenuse / ((double) vol);"),
    ],
    "scheme/Scheme.cs": [
        ("System.identityHashCode(this)", "System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)"),
    ],
    "temperature/TemperatureCache.cs": [
        ("answers.Add(0, allColors.get(index));", "answers.Insert(0, allColors.get(index));"),
        (RE, r"Comparator<Hct> temperaturesComparator =\s*Comparator\.comparing\(\(Hct arg\) => getTempsByHct\(\)\.get\(arg\), double\.compareTo\);\s*Collections\.sort\(hcts, temperaturesComparator\);",
         "Collections.sort(hcts, (Hct a, Hct b) => getTempsByHct().get(a).CompareTo(getTempsByHct().get(b)));"),
    ],
    "utils/StringUtils.cs": [
        ('return string.format("#%02x%02x%02x", red, green, blue);', 'return $"#{red:x2}{green:x2}{blue:x2}";'),
    ],
}

# regex fixes applied to every file
GLOBAL = [
]


def main():
    bad = 0
    for rel, fixes in FIX.items():
        path = os.path.join(ROOT, rel)
        text = open(path, encoding="utf-8").read()
        for fix in fixes:
            if fix[0] == RE:
                _, pat, new = fix
                text, n = re.subn(pat, lambda _m: new, text, flags=re.S)
                if n == 0:
                    print(f"FIXUP NOT APPLIED in {rel}: /{pat[:70]}/")
                    bad += 1
                continue
            old, new = fix
            if old not in text:
                print(f"FIXUP NOT APPLIED in {rel}: {old[:70]!r}")
                bad += 1
                continue
            text = text.replace(old, new)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    for dirpath, _, names in os.walk(ROOT):
        for n in names:
            if not n.endswith(".cs") or n == "JavaCompat.cs":
                continue
            p = os.path.join(dirpath, n)
            text = open(p, encoding="utf-8").read()
            for a, b in GLOBAL:
                text = re.sub(a, b, text)
            with open(p, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
