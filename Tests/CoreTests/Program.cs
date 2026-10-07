// Prints the same lines as Tests/golden/Golden.java, using the C# port, and
// compares them with golden.txt produced by the original Java sources.
//   dotnet run --project Tests/CoreTests -- Tests/golden/golden.txt
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using M3E4Unity.MaterialColor;

static class Program
{
    static readonly int[] SEEDS =
    {
        unchecked((int)0xff6750a4), unchecked((int)0xff4285f4), unchecked((int)0xffff0000), unchecked((int)0xff00ff00),
        unchecked((int)0xff0000ff), unchecked((int)0xffffff00), unchecked((int)0xffffffff), unchecked((int)0xff000000),
        unchecked((int)0xff808080), unchecked((int)0xffb3261e), unchecked((int)0xff00687a), unchecked((int)0xffe8def8),
        unchecked((int)0xff3f51b5), unchecked((int)0xffff9800), unchecked((int)0xff795548), unchecked((int)0xff9c27b0),
    };

    static readonly double[] CONTRASTS = { -1.0, 0.0, 0.5, 1.0 };
    static long lcg = 12345;

    static int NextPixel()
    {
        lcg = unchecked(lcg * 6364136223846793005L + 1442695040888963407L);
        return unchecked((int)0xff000000) | (int)(((ulong)lcg >> 33) & 0xffffff);
    }

    static string F(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
    static string Hex(int argb) => argb.ToString("x8");

    static DynamicScheme Make(Variant v, Hct hct, bool dark, double c, ColorSpec.SpecVersion s, DynamicScheme.Platform p)
    {
        switch (v)
        {
            case Variant.MONOCHROME: return new SchemeMonochrome(hct, dark, c, s, p);
            case Variant.NEUTRAL: return new SchemeNeutral(hct, dark, c, s, p);
            case Variant.TONAL_SPOT: return new SchemeTonalSpot(hct, dark, c, s, p);
            case Variant.VIBRANT: return new SchemeVibrant(hct, dark, c, s, p);
            case Variant.EXPRESSIVE: return new SchemeExpressive(hct, dark, c, s, p);
            case Variant.FIDELITY: return new SchemeFidelity(hct, dark, c, s, p);
            case Variant.CONTENT: return new SchemeContent(hct, dark, c, s, p);
            case Variant.RAINBOW: return new SchemeRainbow(hct, dark, c, s, p);
            case Variant.FRUIT_SALAD: return new SchemeFruitSalad(hct, dark, c, s, p);
            case Variant.CMF: return new SchemeCmf(hct, dark, c, s, p);
        }
        throw new ArgumentException();
    }

    static IEnumerable<string> Lines()
    {
        var mdc = new MaterialDynamicColors();
        var all = mdc.allDynamicColors();

        foreach (int seed in SEEDS)
        {
            var hct = Hct.fromInt(seed);
            yield return $"hct {Hex(seed)} {F(hct.getHue())} {F(hct.getChroma())} {F(hct.getTone())}";
            var tp = TonalPalette.fromInt(seed);
            var sb = new StringBuilder($"palette {Hex(seed)} key={Hex(tp.getKeyColor().toInt())}");
            for (int t = 0; t <= 100; t += 5) sb.Append(' ').Append(Hex(tp.tone(t)));
            sb.Append(' ').Append(Hex(tp.tone(99)));
            yield return sb.ToString();
            var tc = new TemperatureCache(hct);
            var ab = new StringBuilder($"temp {Hex(seed)} comp={Hex(tc.getComplement().toInt())}");
            foreach (var a in tc.getAnalogousColors()) ab.Append(' ').Append(Hex(a.toInt()));
            yield return ab.ToString();
            foreach (int other in SEEDS)
                yield return $"harmonize {Hex(seed)} {Hex(other)} {Hex(Blend.harmonize(seed, other))}";
        }

        foreach (int seed in SEEDS)
        {
            var hct = Hct.fromInt(seed);
            foreach (Variant v in Enum.GetValues(typeof(Variant)))
            foreach (ColorSpec.SpecVersion s in Enum.GetValues(typeof(ColorSpec.SpecVersion)))
            foreach (DynamicScheme.Platform p in Enum.GetValues(typeof(DynamicScheme.Platform)))
            foreach (bool dark in new[] { false, true })
            foreach (double c in CONTRASTS)
            {
                string head = $"scheme {Hex(seed)} {v} {s} {p} {(dark ? "true" : "false")} {F(c)}";
                DynamicScheme scheme;
                try { scheme = Make(v, hct, dark, c, s, p); }
                catch (Exception) { scheme = null; }
                if (scheme == null) { yield return head + " ERR"; continue; }
                var sb = new StringBuilder(head);
                foreach (var sup in all)
                {
                    var dc = sup();
                    string val;
                    try { val = Hex(dc.getArgb(scheme)); }
                    catch (Exception) { val = "ERR"; }
                    sb.Append(' ').Append(dc.name).Append('=').Append(val);
                }
                yield return sb.ToString();
            }
        }

        for (int img = 0; img < 4; img++)
        {
            int n = 64 * 64;
            var pixels = new int[n];
            for (int i = 0; i < n; i++)
            {
                int pick = (int)(((ulong)lcg >> 40) % 5);
                pixels[i] = pick < 3 ? SEEDS[(img * 3 + pick) % SEEDS.Length] : NextPixel();
                NextPixel();
            }
            var qr = QuantizerCelebi.quantize(pixels, 128);
            var sorted = new SortedDictionary<int, int>(qr);
            var sb = new StringBuilder($"quantize {img} n={sorted.Count}");
            foreach (var e in sorted) sb.Append(' ').Append(Hex(e.Key)).Append(':').Append(e.Value);
            yield return sb.ToString();
            var sc = new StringBuilder($"score {img}");
            foreach (int c in Score.score(qr)) sc.Append(' ').Append(Hex(c));
            yield return sc.ToString();
        }
    }

    static int ShapesSvg(string path)
    {
        // Every MaterialShape, and a few Morph steps, as an SVG contact sheet.
        var sb = new StringBuilder();
        var shapes = (M3E4Unity.Shapes.MaterialShape[])Enum.GetValues(typeof(M3E4Unity.Shapes.MaterialShape));
        int cols = 7, cell = 120;
        int rows = (shapes.Length + cols - 1) / cols + 2;
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{cols * cell}' height='{rows * cell}' style='background:#fff'>");
        string PathOf(IEnumerable<M3E4Unity.Shapes.Cubic> cubics, float ox, float oy, float s)
        {
            var p = new StringBuilder();
            bool first = true;
            foreach (var c in cubics)
            {
                if (first) { p.Append(FormattableString.Invariant($"M{ox + c.Anchor0X * s},{oy + c.Anchor0Y * s} ")); first = false; }
                p.Append(FormattableString.Invariant($"C{ox + c.Control0X * s},{oy + c.Control0Y * s} {ox + c.Control1X * s},{oy + c.Control1Y * s} {ox + c.Anchor1X * s},{oy + c.Anchor1Y * s} "));
            }
            return p.Append('Z').ToString();
        }
        for (int i = 0; i < shapes.Length; i++)
        {
            var poly = M3E4Unity.Shapes.MaterialShapes.Get(shapes[i]);
            float ox = (i % cols) * cell + 10, oy = (i / cols) * cell + 10;
            sb.Append($"<path d='{PathOf(poly.Cubics, ox, oy, 90)}' fill='#6750a4'/>");
            sb.Append($"<text x='{ox}' y='{oy + 105}' font-size='10' font-family='sans-serif'>{shapes[i]}</text>");
        }
        var morph = new M3E4Unity.Shapes.Morph(
            M3E4Unity.Shapes.MaterialShapes.Get(M3E4Unity.Shapes.MaterialShape.SoftBurst),
            M3E4Unity.Shapes.MaterialShapes.Get(M3E4Unity.Shapes.MaterialShape.Cookie9Sided));
        int baseRow = (shapes.Length + cols - 1) / cols;
        for (int k = 0; k < cols; k++)
        {
            float t = k / (float)(cols - 1);
            sb.Append($"<path d='{PathOf(morph.AsCubics(t), k * cell + 10, baseRow * cell + 10, 90)}' fill='#7d5260'/>");
        }
        sb.Append("</svg>");
        File.WriteAllText(path, sb.ToString());
        return 0;
    }

    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "shapes-svg") return ShapesSvg(args[1]);
        if (args.Length == 1 && args[0] == "carousel") return CarouselDump();
        var golden = args.Length > 0 ? File.ReadAllLines(args[0]) : null;
        int i = 0, mismatches = 0, total = 0;
        foreach (var line in Lines())
        {
            total++;
            if (golden == null) { Console.WriteLine(line); continue; }
            var expected = i < golden.Length ? golden[i] : "<missing>";
            i++;
            if (line == expected) continue;
            mismatches++;
            if (mismatches <= 20) ReportDiff(expected, line);
        }
        if (golden == null) return 0;
        Console.WriteLine($"{total} lines, {mismatches} mismatching");
        return mismatches == 0 && i == golden.Length ? 0 : 1;
    }

    static void ReportDiff(string expected, string actual)
    {
        var e = expected.Split(' ');
        var a = actual.Split(' ');
        var head = string.Join(" ", e.Take(Math.Min(7, e.Length)));
        var diffs = new List<string>();
        for (int k = 0; k < Math.Max(e.Length, a.Length); k++)
        {
            var ek = k < e.Length ? e[k] : "-";
            var ak = k < a.Length ? a[k] : "-";
            if (ek != ak) diffs.Add($"{ek} != {ak}");
        }
        Console.WriteLine($"MISMATCH {head}\n    " + string.Join("\n    ", diffs.Take(8)));
    }

    static int CarouselDump()
    {
        void Dump(string name, M3E4Unity.Carousel.KeylineList k)
        {
            System.Console.WriteLine(name + ": " + string.Join(" | ", k.Keylines.Select(x => $"s={x.Size:0.###} o={x.Offset:0.###} u={x.UnadjustedOffset:0.###}{(x.IsFocal ? " F" : "")}{(x.IsAnchor ? " A" : "")}{(x.IsPivot ? " P" : "")} c={x.Cutoff:0.###}")));
        }
        var mb = M3E4Unity.Carousel.CarouselKeylines.MultiBrowse(412, 186, 8, 10);
        Dump("multiBrowse 412/186/8", mb);
        var st = new M3E4Unity.Carousel.CarouselStrategy(mb, 412, 8, 0, 0);
        System.Console.WriteLine($"itemSize={st.ItemMainAxisSize} startSteps={st.StartKeylineSteps.Count} endSteps={st.EndKeylineSteps.Count} startShift={st.StartShiftDistance} endShift={st.EndShiftDistance}");
        for (int i = 0; i < st.EndKeylineSteps.Count; i++) Dump("  end" + i, st.EndKeylineSteps[i]);
        Dump("uncontained 412/186/8", M3E4Unity.Carousel.CarouselKeylines.Uncontained(412, 186, 8));
        Dump("hero centered 412/267/8", M3E4Unity.Carousel.CarouselKeylines.Hero(412, 267, 8, 10, true));
        return 0;
    }
}