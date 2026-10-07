// Golden-data generator, run against the original material-color-utilities Java sources.
// Tests/CoreTests/Program.cs prints the same lines from the C# port; the outputs must be identical.
import blend.Blend;
import dynamiccolor.ColorSpec.SpecVersion;
import dynamiccolor.DynamicColor;
import dynamiccolor.DynamicScheme;
import dynamiccolor.DynamicScheme.Platform;
import dynamiccolor.MaterialDynamicColors;
import dynamiccolor.Variant;
import hct.Hct;
import palettes.TonalPalette;
import quantize.QuantizerCelebi;
import scheme.*;
import score.Score;
import temperature.TemperatureCache;
import java.io.PrintStream;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.TreeMap;
import java.util.function.Supplier;

public class Golden {
  static final int[] SEEDS = {
    0xff6750a4, 0xff4285f4, 0xffff0000, 0xff00ff00, 0xff0000ff, 0xffffff00, 0xffffffff,
    0xff000000, 0xff808080, 0xffb3261e, 0xff00687a, 0xffe8def8, 0xff3f51b5, 0xffff9800,
    0xff795548, 0xff9c27b0
  };
  static final double[] CONTRASTS = {-1.0, 0.0, 0.5, 1.0};

  static long lcg = 12345;

  static int nextPixel() {
    lcg = (lcg * 6364136223846793005L + 1442695040888963407L);
    return 0xff000000 | (int) ((lcg >>> 33) & 0xffffff);
  }

  static String f(double d) {
    return String.format(Locale.ROOT, "%.6f", d);
  }

  static String hex(int argb) {
    return String.format("%08x", argb);
  }

  static DynamicScheme make(Variant v, Hct hct, boolean dark, double c, SpecVersion s, Platform p) {
    switch (v) {
      case MONOCHROME: return new SchemeMonochrome(hct, dark, c, s, p);
      case NEUTRAL: return new SchemeNeutral(hct, dark, c, s, p);
      case TONAL_SPOT: return new SchemeTonalSpot(hct, dark, c, s, p);
      case VIBRANT: return new SchemeVibrant(hct, dark, c, s, p);
      case EXPRESSIVE: return new SchemeExpressive(hct, dark, c, s, p);
      case FIDELITY: return new SchemeFidelity(hct, dark, c, s, p);
      case CONTENT: return new SchemeContent(hct, dark, c, s, p);
      case RAINBOW: return new SchemeRainbow(hct, dark, c, s, p);
      case FRUIT_SALAD: return new SchemeFruitSalad(hct, dark, c, s, p);
      case CMF: return new SchemeCmf(hct, dark, c, s, p);
    }
    throw new IllegalArgumentException();
  }

  public static void main(String[] args) throws Exception {
    PrintStream out = new PrintStream(System.out, false, "UTF-8");
    MaterialDynamicColors mdc = new MaterialDynamicColors();
    List<Supplier<DynamicColor>> all = mdc.allDynamicColors();

    for (int seed : SEEDS) {
      Hct hct = Hct.fromInt(seed);
      out.println("hct " + hex(seed) + " " + f(hct.getHue()) + " " + f(hct.getChroma()) + " " + f(hct.getTone()));
      TonalPalette tp = TonalPalette.fromInt(seed);
      StringBuilder sb = new StringBuilder("palette " + hex(seed) + " key=" + hex(tp.getKeyColor().toInt()));
      for (int t = 0; t <= 100; t += 5) sb.append(" ").append(hex(tp.tone(t)));
      sb.append(" ").append(hex(tp.tone(99)));
      out.println(sb);
      TemperatureCache tc = new TemperatureCache(hct);
      StringBuilder ab = new StringBuilder("temp " + hex(seed) + " comp=" + hex(tc.getComplement().toInt()));
      for (Hct a : tc.getAnalogousColors()) ab.append(" ").append(hex(a.toInt()));
      out.println(ab);
      for (int other : SEEDS) {
        out.println("harmonize " + hex(seed) + " " + hex(other) + " " + hex(Blend.harmonize(seed, other)));
      }
    }

    for (int seed : SEEDS) {
      Hct hct = Hct.fromInt(seed);
      for (Variant v : Variant.values()) {
        for (SpecVersion s : SpecVersion.values()) {
          for (Platform p : Platform.values()) {
            for (boolean dark : new boolean[] {false, true}) {
              for (double c : CONTRASTS) {
                String head = "scheme " + hex(seed) + " " + v + " " + s + " " + p + " " + dark + " " + f(c);
                DynamicScheme scheme;
                try {
                  scheme = make(v, hct, dark, c, s, p);
                } catch (RuntimeException e) {
                  out.println(head + " ERR");
                  continue;
                }
                StringBuilder sb = new StringBuilder(head);
                for (Supplier<DynamicColor> sup : all) {
                  DynamicColor dc = sup.get();
                  String val;
                  try {
                    val = hex(dc.getArgb(scheme));
                  } catch (RuntimeException e) {
                    val = "ERR";
                  }
                  sb.append(" ").append(dc.name).append("=").append(val);
                }
                out.println(sb);
              }
            }
          }
        }
      }
    }

    for (int img = 0; img < 4; img++) {
      int n = 64 * 64;
      int[] pixels = new int[n];
      for (int i = 0; i < n; i++) {
        // a few dominant colours plus noise
        int pick = (int) ((lcg >>> 40) % 5);
        pixels[i] = pick < 3 ? SEEDS[(img * 3 + pick) % SEEDS.length] : nextPixel();
        nextPixel();
      }
      Map<Integer, Integer> qr = QuantizerCelebi.quantize(pixels, 128);
      TreeMap<Integer, Integer> sorted = new TreeMap<>(qr);
      StringBuilder sb = new StringBuilder("quantize " + img + " n=" + sorted.size());
      for (Map.Entry<Integer, Integer> e : sorted.entrySet()) sb.append(" ").append(hex(e.getKey())).append(":").append(e.getValue());
      out.println(sb);
      StringBuilder sc = new StringBuilder("score " + img);
      for (int c : Score.score(qr)) sc.append(" ").append(hex(c));
      out.println(sc);
    }
    out.flush();
  }
}
