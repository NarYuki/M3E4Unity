#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Fixed tab row (TabRow.kt TabRowImpl + TabIndicatorOffsetNode): the indicator's offset and
    /// width follow the selected tab with DefaultSpatial springs; tab content colors fade with the
    /// selection (TabTransition: DefaultEffects towards the active color, FastEffects towards the
    /// inactive one, interpolated in Oklab like animateColor). Tabs call _Select0 … _Select11.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TabRow : UdonSharpBehaviour
#else
    public class M3TabRow : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int selectedIndex;
        public Selectable[] tabs;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Indicator")]
        public RectTransform indicator;
        public float tabWidth;
        [Tooltip("Indicator width per tab: the content width (primary) or the tab width (secondary).")]
        public float[] indicatorWidths;
        public float springDamping = 0.8f;
        public float springStiffness = 380f;
        public float effectsStiffness = 1600f;     // DefaultEffects (fade in)
        public float fastEffectsStiffness = 3800f; // FastEffects (fade out)

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("6 palette indices per graphic: unselected, selected, -, disabled unselected, disabled selected, -.")]
        public int[] colorIndices;
        public int[] coloredItem;

        float offset;
        float offsetV;
        float width;
        float widthV;
        float lastTime;
        bool ticking;
        bool initialized;
        float[] fade;       // per tab: 0 = inactive color, 1 = active color
        float[] fadeV;

        void Start()
        {
            Snap();
        }

        void Snap()
        {
            initialized = true;
            offset = tabWidth * selectedIndex;
            width = indicatorWidths != null && selectedIndex < indicatorWidths.Length ? indicatorWidths[selectedIndex] : tabWidth;
            offsetV = 0f;
            widthV = 0f;
            int n = tabs != null ? tabs.Length : 0;
            fade = new float[n];
            fadeV = new float[n];
            for (int i = 0; i < n; i++) fade[i] = i == selectedIndex ? 1f : 0f;
            ApplyColors();
            Place();
        }

        public void Select(int index)
        {
            if (!initialized) Snap();
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            if (tabs[index] != null && !tabs[index].interactable) return;
            if (index == selectedIndex) return;
            selectedIndex = index;
            ApplyColors();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TabChanged");
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Select0() { Select(0); }
        public void _Select1() { Select(1); }
        public void _Select2() { Select(2); }
        public void _Select3() { Select(3); }
        public void _Select4() { Select(4); }
        public void _Select5() { Select(5); }
        public void _Select6() { Select(6); }
        public void _Select7() { Select(7); }
        public void _Select8() { Select(8); }
        public void _Select9() { Select(9); }
        public void _Select10() { Select(10); }
        public void _Select11() { Select(11); }

        public void _M3ThemeChanged() { ApplyColors(); }
        public void _M3Refresh() { Snap(); }

        float stepX;
        float stepV;

        bool Step(float value, float velocity, float target, float dt)
        {
            float x0 = value - target;
            if (Mathf.Abs(x0) < 0.01f && Mathf.Abs(velocity) < 0.1f)
            {
                stepX = target;
                stepV = 0f;
                return false;
            }
            float w = Mathf.Sqrt(springStiffness);
            float z = springDamping;
            float x;
            float v;
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + velocity) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                x = e * (x0 * cs + sc * sn);
                v = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else
            {
                float cb = velocity + w * x0;
                float e = Mathf.Exp(-w * dt);
                x = (x0 + cb * dt) * e;
                v = (cb - w * (x0 + cb * dt)) * e;
            }
            stepX = target + x;
            stepV = v;
            return true;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = StepFades(dt);
            if (Step(offset, offsetV, tabWidth * selectedIndex, dt)) busy = true;
            offset = stepX; offsetV = stepV;
            float tw = indicatorWidths != null && selectedIndex < indicatorWidths.Length ? indicatorWidths[selectedIndex] : tabWidth;
            if (Step(width, widthV, tw, dt)) busy = true;
            width = stepX; widthV = stepV;
            Place();
            ApplyColors();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        bool StepFades(float dt)
        {
            if (fade == null) return false;
            bool busy = false;
            for (int i = 0; i < fade.Length; i++)
            {
                float t = i == selectedIndex ? 1f : 0f;
                float x0 = fade[i] - t;
                if (Mathf.Abs(x0) < 0.002f && Mathf.Abs(fadeV[i]) < 0.01f) { fade[i] = t; fadeV[i] = 0f; continue; }
                float w = Mathf.Sqrt(t > 0.5f ? effectsStiffness : fastEffectsStiffness);
                float cb = fadeV[i] + w * x0;
                float e = Mathf.Exp(-w * dt);
                fade[i] = t + (x0 + cb * dt) * e;
                fadeV[i] = (cb - w * (x0 + cb * dt)) * e;
                busy = true;
            }
            return busy;
        }

        // ColorVectorConverter: sRGB -> Oklab, lerp, back
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

        Color Mix(Color from, Color to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            ToOklab(from);
            float l0 = okL, a0 = okA, b0 = okB;
            ToOklab(to);
            return FromOklab(l0 + (okL - l0) * t, a0 + (okA - a0) * t, b0 + (okB - b0) * t, from.a + (to.a - from.a) * t);
        }

        void Place()
        {
            if (indicator == null) return;
            // the indicator layout is centered in the tab-wide constraints: offset + (tabWidth - width) / 2
            indicator.anchorMin = new Vector2(0f, 0f);
            indicator.anchorMax = new Vector2(0f, 0f);
            indicator.pivot = new Vector2(0f, 0f);
            indicator.anchoredPosition = new Vector2(Mathf.Round(offset + (tabWidth - width) / 2f), 0f);
            indicator.sizeDelta = new Vector2(Mathf.Round(width), indicator.sizeDelta.y);
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null || coloredItem == null) return;
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                int item = coloredItem[i];
                bool enabled = tabs == null || item >= tabs.Length || tabs[item] == null || tabs[item].interactable;
                if (!enabled)
                {
                    int idx = colorIndices[i * 6 + (item == selectedIndex ? 4 : 3)];
                    if (idx >= 0) g.color = theme.Get(idx);
                    continue;
                }
                int i0 = colorIndices[i * 6];
                int i1 = colorIndices[i * 6 + 1];
                if (i0 < 0 || i1 < 0) continue;
                float t = fade != null && item < fade.Length ? Mathf.Clamp01(fade[item]) : (item == selectedIndex ? 1f : 0f);
                g.color = Mix(theme.Get(i0), theme.Get(i1), t);
            }
        }
    }
}
