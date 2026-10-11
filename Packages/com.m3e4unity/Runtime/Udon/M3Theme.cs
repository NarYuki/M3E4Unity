#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Runtime side of an M3E4Unity theme. The editor bakes every color the UI under this
    /// object needs (roles, composited state layers, disabled colors...) into a palette per
    /// scheme (light, dark, extra seeds...). Switching scheme recolors all bound graphics
    /// and tells the interactive components to refresh. Works in VRChat (Udon) and in plain Unity.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Theme : UdonSharpBehaviour
#else
    public class M3Theme : M3BehaviourBase
#endif
    {
        [Tooltip("Display names of the baked schemes, e.g. Light, Dark.")]
        public string[] schemeNames;

        [Tooltip("Number of colors per scheme.")]
        public int paletteSize;

        [Tooltip("All schemes' palettes, scheme-major (scheme * paletteSize + index).")]
        public Color[] palettes;

        [Tooltip("Scheme shown at start.")]
        public int currentScheme;

        [Tooltip("Graphics colored straight from the palette.")]
        public Graphic[] graphics;

        [Tooltip("Palette index of each graphic.")]
        public int[] graphicColors;

#if UDONSHARP
        [Tooltip("Components that read colors from this theme; they get _M3ThemeChanged.")]
        public UdonSharpBehaviour[] listeners;
#else
        [Tooltip("Components that read colors from this theme; they get _M3ThemeChanged.")]
        public M3BehaviourBase[] listeners;
#endif

        [Tooltip("Shared ripple materials (M3Shape copies with _M3DynOn); components take one per press.")]
        public Material[] rippleMaterials;
        int nextRipple = -1;

        void Start()
        {
            ApplyScheme();
        }

        /// <summary>The next ripple material of the pool (round robin), or null without a pool.</summary>
        public Material _NextRippleMaterial()
        {
            if (rippleMaterials == null || rippleMaterials.Length == 0) return null;
            // start at a random slot so that several canvases sharing the pool rarely collide
            if (nextRipple < 0) nextRipple = Random.Range(0, rippleMaterials.Length);
            nextRipple = (nextRipple + 1) % rippleMaterials.Length;
            return rippleMaterials[nextRipple];
        }

        /// <summary>Color at palette index for the current scheme.</summary>
        public Color Get(int index)
        {
            if (index < 0 || index >= paletteSize) return Color.magenta;
            return palettes[currentScheme * paletteSize + index];
        }

        /// <summary>
        /// animateColor's interpolation (ColorVectorConverter): sRGB to Oklab, linear blend of
        /// L, a, b and alpha, back to sRGB.
        /// </summary>
        public Color MixOklab(Color from, Color to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            ToOklab(from);
            float l0 = okL, a0 = okA, b0 = okB;
            ToOklab(to);
            return FromOklab(l0 + (okL - l0) * t, a0 + (okA - a0) * t, b0 + (okB - b0) * t, from.a + (to.a - from.a) * t);
        }

        float okL, okA, okB;

        void ToOklab(Color c)
        {
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
            okL = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
            okA = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
            okB = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
        }

        Color FromOklab(float L, float A, float B, float alpha)
        {
            float l = L + 0.3963377774f * A + 0.2158037573f * B;
            float m = L - 0.1055613458f * A - 0.0638541728f * B;
            float s = L - 0.0894841775f * A - 1.2914855480f * B;
            l = l * l * l; m = m * m * m; s = s * s * s;
            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            return new Color(Enc(r), Enc(g), Enc(b), alpha);
        }

        static float Cbrt(float x) { return x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f); }
        static float Lin(float c) { return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f); }
        static float Enc(float c)
        {
            c = Mathf.Clamp01(c);
            return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }

        public int SchemeCount()
        {
            return paletteSize > 0 ? palettes.Length / paletteSize : 0;
        }

        public void SetScheme(int scheme)
        {
            int count = SchemeCount();
            if (count == 0) return;
            currentScheme = Mathf.Clamp(scheme, 0, count - 1);
            ApplyScheme();
        }

        /// <summary>Cycles to the next scheme (wire a button's OnClick to this event).</summary>
        public void NextScheme()
        {
            int count = SchemeCount();
            if (count == 0) return;
            currentScheme = (currentScheme + 1) % count;
            ApplyScheme();
        }

        public void ApplyScheme()
        {
            if (graphics != null)
            {
                int offset = currentScheme * paletteSize;
                for (int i = 0; i < graphics.Length; i++)
                {
                    Graphic g = graphics[i];
                    if (g == null) continue;
                    g.color = palettes[offset + graphicColors[i]];
                }
            }
            if (listeners != null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] != null) listeners[i].SendCustomEvent("_M3ThemeChanged");
                }
            }
        }
    }
}
