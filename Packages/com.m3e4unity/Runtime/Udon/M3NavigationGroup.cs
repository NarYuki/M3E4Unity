#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Single selection over navigation items (NavigationItem.kt): the selected item's indicator
    /// grows from the center with alpha = width = indicator progress (animateIndicatorProgressAsState,
    /// DefaultSpatial); icon / label colors switch with the selection, filled icons replace outlined
    /// ones. Items call _Select0 … _Select11.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3NavigationGroup : UdonSharpBehaviour
#else
    public class M3NavigationGroup : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int selectedIndex;
        public Selectable[] items;
        [Tooltip("Per-item interactives (state layer / ripple); told about the selection so their colors match the indicator.")]
        public M3Interactive[] interactives;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Indicators")]
        public RectTransform[] indicators;
        public float[] indicatorWidths;
        public Graphic[] indicatorGraphics;
        public float springDamping = 0.8f;
        public float springStiffness = 380f;

        [Header("Selected / unselected content")]
        public GameObject[] showWhenSelected;
        public int[] showItem;
        public GameObject[] hideWhenSelected;
        public int[] hideItem;

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("6 palette indices per graphic: unselected, selected, -, disabled unselected, disabled selected, -.")]
        public int[] colorIndices;
        [Tooltip("Item index of every colored graphic.")]
        public int[] coloredItem;

        float[] progress;
        float[] velocity;
        float lastTime;
        bool ticking;

        void Start()
        {
            Init();
            Snap();
        }

        void Init()
        {
            if (items == null) return;
            if (progress == null || progress.Length != items.Length)
            {
                progress = new float[items.Length];
                velocity = new float[items.Length];
                for (int i = 0; i < items.Length; i++) progress[i] = i == selectedIndex ? 1f : 0f;
            }
        }

        void Snap()
        {
            Init();
            if (progress == null) return;
            for (int i = 0; i < progress.Length; i++) { progress[i] = i == selectedIndex ? 1f : 0f; velocity[i] = 0f; }
            Apply();
        }

        public void Select(int index)
        {
            Init();
            if (items == null || index < 0 || index >= items.Length) return;
            if (items[index] != null && !items[index].interactable) return;
            if (index == selectedIndex) return;
            selectedIndex = index;
            Apply();
            if (changeListener != null) changeListener.SendCustomEvent("_M3NavChanged");
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

        public void _M3ThemeChanged() { Apply(); }
        public void _M3Refresh() { progress = null; Snap(); }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            float w = Mathf.Sqrt(springStiffness);
            float z = springDamping;
            for (int i = 0; i < progress.Length; i++)
            {
                float target = i == selectedIndex ? 1f : 0f;
                float x0 = progress[i] - target;
                float v0 = velocity[i];
                if (Mathf.Abs(x0) < 0.001f && Mathf.Abs(v0) < 0.01f)
                {
                    progress[i] = target;
                    velocity[i] = 0f;
                    continue;
                }
                busy = true;
                float x;
                float v;
                if (z < 1f)
                {
                    float wd = w * Mathf.Sqrt(1f - z * z);
                    float sc = (z * w * x0 + v0) / wd;
                    float e = Mathf.Exp(-z * w * dt);
                    float cs = Mathf.Cos(wd * dt);
                    float sn = Mathf.Sin(wd * dt);
                    x = e * (x0 * cs + sc * sn);
                    v = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
                }
                else
                {
                    float cb = v0 + w * x0;
                    float e = Mathf.Exp(-w * dt);
                    x = (x0 + cb * dt) * e;
                    v = (cb - w * (x0 + cb * dt)) * e;
                }
                progress[i] = target + x;
                velocity[i] = v;
            }
            ApplyIndicators();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            if (interactives != null)
                for (int i = 0; i < interactives.Length; i++)
                {
                    if (interactives[i] == null) continue;
                    if (i == selectedIndex) interactives[i]._Select(); else interactives[i]._Deselect();
                }
            if (showWhenSelected != null)
                for (int i = 0; i < showWhenSelected.Length; i++)
                    if (showWhenSelected[i] != null) showWhenSelected[i].SetActive(showItem[i] == selectedIndex);
            if (hideWhenSelected != null)
                for (int i = 0; i < hideWhenSelected.Length; i++)
                    if (hideWhenSelected[i] != null) hideWhenSelected[i].SetActive(hideItem[i] != selectedIndex);
            ApplyColors();
            ApplyIndicators();
        }

        void ApplyIndicators()
        {
            if (indicators == null || progress == null) return;
            for (int i = 0; i < indicators.Length && i < progress.Length; i++)
            {
                RectTransform rt = indicators[i];
                if (rt == null) continue;
                // indicatorAnimationProgress.coerceAtLeast(0): width = full * p, alpha = p
                float p = Mathf.Max(0f, progress[i]);
                rt.sizeDelta = new Vector2(Mathf.Round(indicatorWidths[i] * p), rt.sizeDelta.y);
                Graphic g = indicatorGraphics != null && i < indicatorGraphics.Length ? indicatorGraphics[i] : null;
                if (g != null)
                {
                    Color c = g.color;
                    c.a = Mathf.Clamp01(p);
                    g.color = c;
                }
            }
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null || coloredItem == null) return;
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                int item = coloredItem[i];
                bool enabled = items == null || item >= items.Length || items[item] == null || items[item].interactable;
                int slot = (item == selectedIndex ? 1 : 0) + (enabled ? 0 : 3);
                int idx = colorIndices[i * 6 + slot];
                if (idx < 0) continue;
                Color c = theme.Get(idx);
                // indicators keep their animated alpha
                if (indicatorGraphics != null)
                    for (int k = 0; k < indicatorGraphics.Length; k++)
                        if (indicatorGraphics[k] == g) c.a = g.color.a;
                g.color = c;
            }
        }
    }
}
