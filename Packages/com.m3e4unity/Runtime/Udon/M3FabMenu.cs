#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// FAB menu (FloatingActionButtonMenu.kt): ToggleFloatingActionButton (checkedProgress with
    /// FastSpatial: corner radius, container color PrimaryContainer → Primary, icon add → close at 50 %,
    /// icon size and color) and the menu items, revealed bottom-up by a staggered item count
    /// (SlowEffects) with per-item width (FastSpatial) and alpha (FastEffects) springs.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3FabMenu : UdonSharpBehaviour
#else
    public class M3FabMenu : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public bool expanded;

        [Header("Toggle FAB")]
        public Image fabContainer;
        public Image[] fabShadows;
        public Sprite[] fabFrames;          // container sprites for radius minRadius..maxRadius
        public Sprite[] fabShadowFrames;    // per shadow layer × frame
        public RectTransform iconOpen;      // "add"
        public RectTransform iconClose;     // "close"
        public TextMeshProUGUI iconOpenText;
        public TextMeshProUGUI iconCloseText;
        public float iconSizeFrom = 24f;
        public float iconSizeTo = 20f;

        [Header("Items (top to bottom)")]
        public RectTransform[] items;       // the clipped item containers (pivot at the end)
        public float[] itemWidths;
        public CanvasGroup[] itemGroups;

        [Header("Motion")]
        public float spatialDamping = 0.6f;
        public float spatialStiffness = 800f;
        public float fastEffectsStiffness = 3800f;
        public float slowEffectsStiffness = 800f;

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: container initial, container final, icon initial, icon final.")]
        public int[] extraColors;

        float progress;
        float progressV;
        float stagger;
        float staggerV;
        float[] w;
        float[] wV;
        float[] a;
        float[] aV;
        float lastTime;
        bool ticking;
        bool initialized;

        void Start() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            int n = items != null ? items.Length : 0;
            w = new float[n]; wV = new float[n]; a = new float[n]; aV = new float[n];
            progress = expanded ? 1f : 0f;
            stagger = expanded ? n : 0f;
            for (int i = 0; i < n; i++) { w[i] = expanded ? 1f : 0f; a[i] = w[i]; }
            Apply();
        }

        public void _M3Refresh() { initialized = false; Init(); }
        public void _M3ThemeChanged() { Init(); Apply(); } // may arrive before Start (inactive pages)
        public void _Toggle() { Init(); expanded = !expanded; Kick(); }
        public void _Open() { Init(); expanded = true; Kick(); }
        public void _Close() { Init(); expanded = false; Kick(); }

        void Kick()
        {
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        float sx;
        float sv;

        bool Spring(float x, float v, float target, float dt, float z, float k, float threshold)
        {
            float x0 = x - target;
            if (Mathf.Abs(x0) < threshold && Mathf.Abs(v) < threshold * 10f) { sx = target; sv = 0f; return false; }
            float wn = Mathf.Sqrt(k);
            float y;
            float nv;
            if (z < 1f)
            {
                float wd = wn * Mathf.Sqrt(1f - z * z);
                float sc = (z * wn * x0 + v) / wd;
                float e = Mathf.Exp(-z * wn * dt);
                y = e * (x0 * Mathf.Cos(wd * dt) + sc * Mathf.Sin(wd * dt));
                nv = -z * wn * y + e * (-x0 * wd * Mathf.Sin(wd * dt) + sc * wd * Mathf.Cos(wd * dt));
            }
            else
            {
                float cb = v + wn * x0;
                float e = Mathf.Exp(-wn * dt);
                y = (x0 + cb * dt) * e;
                nv = (cb - wn * (x0 + cb * dt)) * e;
            }
            sx = target + y;
            sv = nv;
            return true;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            int n = items != null ? items.Length : 0;
            bool busy = Spring(progress, progressV, expanded ? 1f : 0f, dt, spatialDamping, spatialStiffness, 0.0005f);
            progress = sx; progressV = sv;
            // stagger: an Int animatable with a SlowEffects spring (visibilityThreshold = 1)
            if (Spring(stagger, staggerV, expanded ? n : 0f, dt, 1f, slowEffectsStiffness, 0.5f)) busy = true;
            stagger = sx; staggerV = sv;
            int visibleCount = Mathf.RoundToInt(stagger);
            for (int i = 0; i < n; i++)
            {
                // itemVisible = index >= itemCount - stagger (bottom items first)
                float target = i >= n - visibleCount ? 1f : 0f;
                if (Spring(w[i], wV[i], target, dt, spatialDamping, spatialStiffness, 0.0005f)) busy = true;
                w[i] = sx; wV[i] = sv;
                if (Spring(a[i], aV[i], target, dt, 1f, fastEffectsStiffness, 0.001f)) busy = true;
                a[i] = sx; aV[i] = sv;
            }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            float p = Mathf.Clamp01(progress);
            if (fabFrames != null && fabFrames.Length > 0 && fabContainer != null)
            {
                int f = Mathf.Clamp(Mathf.RoundToInt(p * (fabFrames.Length - 1)), 0, fabFrames.Length - 1);
                fabContainer.sprite = fabFrames[f];
                if (fabShadows != null && fabShadowFrames != null)
                    for (int s = 0; s < fabShadows.Length; s++)
                        if (fabShadows[s] != null) fabShadows[s].sprite = fabShadowFrames[s * fabFrames.Length + f];
            }
            if (theme != null && extraColors != null && extraColors.Length >= 4)
            {
                if (fabContainer != null) fabContainer.color = Color.Lerp(theme.Get(extraColors[0]), theme.Get(extraColors[1]), p);
                Color ic = Color.Lerp(theme.Get(extraColors[2]), theme.Get(extraColors[3]), p);
                if (iconOpenText != null) iconOpenText.color = ic;
                if (iconCloseText != null) iconCloseText.color = ic;
            }
            // the sample swaps Add / Close at checkedProgress 0.5; animateIcon scales the icon size
            bool close = progress > 0.5f;
            float scale = Mathf.LerpUnclamped(iconSizeFrom, iconSizeTo, progress) / iconSizeFrom;
            if (iconOpen != null) { iconOpen.gameObject.SetActive(!close); iconOpen.localScale = new Vector3(scale, scale, 1f); }
            if (iconClose != null) { iconClose.gameObject.SetActive(close); iconClose.localScale = new Vector3(scale, scale, 1f); }
            if (items != null)
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null) continue;
                    float width = Mathf.Round(itemWidths[i] * Mathf.Max(w[i], 0f));
                    items[i].sizeDelta = new Vector2(width, items[i].sizeDelta.y);
                    bool visible = a[i] > 0f;
                    items[i].gameObject.SetActive(visible);
                    if (itemGroups != null && itemGroups[i] != null) itemGroups[i].alpha = Mathf.Clamp01(a[i]);
                }
        }
    }
}
