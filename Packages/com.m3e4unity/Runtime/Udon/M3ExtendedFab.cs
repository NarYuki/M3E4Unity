#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Expanding / collapsing an extended FAB (ExtendedFloatingActionButton(expanded = ...)).
    /// <list type="bullet">
    /// <item>sized FABs (small / medium / large, mode 0): the width is lerp(minWidth, expanded width,
    /// progress) with a FastSpatial progress, the content keeps its place at the start and is clipped,
    /// the label fades with FastEffects (the transition of the private ExtendedFloatingActionButton);</item>
    /// <item>the baseline FAB (mode 1): the label (with its 12 dp spacer) is an AnimatedVisibility with
    /// expandHorizontally(FastSpatial) + fadeIn(DefaultEffects) / shrinkHorizontally(DefaultSpatial,
    /// towards Start) + fadeOut(FastEffects); the paddings (16 / 20 → 0), the minimum width
    /// (80 → 56) and the arrangement (Start → Center) follow `expanded` at once, as in Compose.</item>
    /// </list>
    /// Events: _Expand, _Collapse, _Toggle.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3ExtendedFab : UdonSharpBehaviour
#else
    public class M3ExtendedFab : M3BehaviourBase
#endif
    {
        public bool expanded = true;
        public int mode;
        public RectTransform root;
        public LayoutElement rootLayout;
        [Tooltip("The content row (icon, label), anchored at the left of the clip.")]
        public RectTransform row;
        public CanvasGroup labelGroup;
        public RectTransform labelSlot;

        [Header("Geometry (dp)")]
        public float height = 56f;
        public float minWidthExpanded = 80f;
        public float minWidthCollapsed = 56f;
        public float padStart = 16f;
        public float padEnd = 20f;
        public float iconWidth = 24f;
        public float labelWidth;          // spacer + text
        public float iconPadding = 12f;

        [Header("Motion")]
        public float expandDamping = 0.6f, expandStiffness = 800f;    // FastSpatial
        public float shrinkDamping = 0.6f, shrinkStiffness = 800f;    // FastSpatial (sized) / DefaultSpatial (baseline)
        public float fadeInStiffness = 3800f, fadeOutStiffness = 3800f;

        float size = 1f;
        float sizeV;
        float alpha = 1f;
        float alphaV;
        float lastTime;
        bool ticking;

        void Start()
        {
            size = expanded ? 1f : 0f;
            alpha = size;
            Apply();
        }

        public void _M3ThemeChanged() { }
        public void _Expand() { SetExpanded(true); }
        public void _Collapse() { SetExpanded(false); }
        public void _Toggle() { SetExpanded(!expanded); }

        public void SetExpanded(bool value)
        {
            if (value == expanded) return;
            expanded = value;
            Apply();
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        float stepX;
        float stepV;

        bool Spring(float x0, float v0, float dt, float z, float k, float threshold)
        {
            float w = Mathf.Sqrt(k);
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + v0) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                stepX = e * (x0 * cs + sc * sn);
                stepV = -z * w * stepX + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else
            {
                float cb = v0 + w * x0;
                float e = Mathf.Exp(-w * dt);
                stepX = (x0 + cb * dt) * e;
                stepV = (cb - w * (x0 + cb * dt)) * e;
            }
            if (Mathf.Abs(stepX) < threshold && Mathf.Abs(stepV) < threshold * 10f) { stepX = 0f; stepV = 0f; return false; }
            return true;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            float target = expanded ? 1f : 0f;
            bool busy = false;
            float range = Mathf.Max(1f, labelWidth);
            if (Spring(size - target, sizeV, dt, expanded ? expandDamping : shrinkDamping, expanded ? expandStiffness : shrinkStiffness, 0.5f / range)) busy = true;
            size = target + stepX; sizeV = stepV;
            if (Spring(alpha - target, alphaV, dt, 1f, expanded ? fadeInStiffness : fadeOutStiffness, 0.002f)) busy = true;
            alpha = target + stepX; alphaV = stepV;
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            float width;
            float rowX;
            float s = Mathf.Max(0f, size);
            if (mode == 0)
            {
                float full = Mathf.Max(minWidthCollapsed, padStart + iconWidth + labelWidth + padEnd);
                width = Mathf.LerpUnclamped(minWidthCollapsed, full, size);
                rowX = padStart;
            }
            else
            {
                float ps = expanded ? padStart : 0f;
                float pe = expanded ? padEnd : 0f;
                float minW = expanded ? minWidthExpanded : minWidthCollapsed;
                float content = ps + iconWidth + labelWidth * s + pe;
                width = Mathf.Max(minW, content);
                // Arrangement.Start when expanded, Center when collapsed
                rowX = expanded ? ps : (width - (iconWidth + labelWidth * s)) / 2f;
            }
            if (root != null) root.sizeDelta = new Vector2(width, root.sizeDelta.y);
            if (rootLayout != null) { rootLayout.minWidth = width; rootLayout.preferredWidth = width; }
            if (row != null) row.anchoredPosition = new Vector2(rowX, row.anchoredPosition.y);
            if (labelSlot != null && mode == 1) labelSlot.sizeDelta = new Vector2(labelWidth * s, labelSlot.sizeDelta.y);
            if (labelGroup != null)
            {
                labelGroup.alpha = Mathf.Clamp01(alpha);
                bool shown = expanded || size > 0.0001f || alpha > 0.0001f;
                if (labelGroup.gameObject.activeSelf != shown) labelGroup.gameObject.SetActive(shown);
            }
        }
    }
}
