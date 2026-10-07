#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Expressive wide navigation rail (WideNavigationRail.kt WideNavigationRailLayout +
    /// NavigationItem.kt AnimatedMeasurePolicy / placeAnimatedLabelAndIcon). One DefaultSpatial
    /// spring drives the expansion (rail width, item spacing / min height, icon position progress);
    /// per-item DefaultSpatial springs drive the selection indicators. All geometry in dp, y down.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3NavigationRail : UdonSharpBehaviour
#else
    public class M3NavigationRail : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public bool expanded;
        public int selectedIndex;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Rail")]
        public RectTransform rail;
        public RectTransform header;
        public float headerHeight;
        public float collapsedWidth = 96f;
        public float expandedMinWidth = 220f;
        public float expandedMaxWidth = 360f;
        public float topPadding = 44f;
        public float headerPadding = 40f;
        public float collapsedItemSpacing = 4f;
        public float collapsedItemMinHeight = 64f;
        public float expandedItemMinHeight = 48f;

        [Header("Items")]
        public Selectable[] items;
        public M3Interactive[] interactives;
        public RectTransform[] itemRects;
        public RectTransform[] indicators;
        public Graphic[] indicatorGraphics;
        public RectTransform[] stateLayers;
        public RectTransform[] ripples;
        public RectTransform[] iconsOff;
        public RectTransform[] iconsOn;
        public RectTransform[] labelsTop;     // LabelMedium (top icon)
        public RectTransform[] labelsStart;   // LabelLarge (start icon)
        public CanvasGroup[] labelGroups;     // alpha of both labels
        public float[] topLabelW;
        public float[] topLabelH;
        public float[] startLabelW;
        public float[] startLabelH;

        [Header("Item geometry (dp)")]
        public float iconSize = 24f;
        public float itemHorizontalPadding = 20f;      // WNRItemHorizontalPadding
        public float indicatorPadH = 16f;              // ItemTopIconIndicatorHorizontalPadding = FullWidthLeadingSpace
        public float indicatorPadVCollapsed = 4f;      // ItemTopIconIndicatorVerticalPadding
        public float indicatorPadVExpanded = 16f;      // ItemStartIconIndicatorVerticalPadding
        public float topIconToLabel = 4f;              // NavigationRailVerticalItemTokens.IconLabelSpace
        public float startIconToLabel = 8f;            // NavigationRailHorizontalItemTokens.IconLabelSpace
        public float minInteractive = 48f;

        [Header("Motion")]
        public float springDamping = 0.8f;
        public float springStiffness = 380f;
        [Tooltip("Spring for the rail width (modal rails: FastSpatial); a negative damping uses the spring above.")]
        public float widthDamping = -1f;
        public float widthStiffness = 800f;

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("6 palette indices per graphic: unselected, selected, -, disabled unselected, disabled selected, -.")]
        public int[] colorIndices;
        public int[] coloredItem;

        float expandP;
        float expandV;
        float widthP;
        float widthV;
        float[] selP;
        float[] selV;
        bool targetTop;
        float lastTime;
        bool ticking;

        void Start()
        {
            Snap();
        }

        void Snap()
        {
            if (items == null) return;
            expandP = expanded ? 1f : 0f;
            expandV = 0f;
            widthP = expandP;
            widthV = 0f;
            targetTop = !expanded;
            selP = new float[items.Length];
            selV = new float[items.Length];
            for (int i = 0; i < items.Length; i++) selP[i] = i == selectedIndex ? 1f : 0f;
            ApplySelection();
            Layout();
        }

        public void _Expand() { expanded = true; targetTop = false; Kick(); }
        public void _Collapse() { expanded = false; targetTop = true; Kick(); }
        public void _Toggle() { if (expanded) _Collapse(); else _Expand(); }
        public void _M3ThemeChanged() { ApplySelection(); }
        public void _M3Refresh() { Snap(); }

        public void Select(int index)
        {
            if (items == null || index < 0 || index >= items.Length) return;
            if (items[index] != null && !items[index].interactable) return;
            if (index == selectedIndex) return;
            selectedIndex = index;
            ApplySelection();
            if (changeListener != null) changeListener.SendCustomEvent("_M3NavChanged");
            Kick();
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

        void Kick()
        {
            if (selP == null) Snap();
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        float stepX;
        float stepV;

        bool Step(float value, float velocity, float target, float dt)
        {
            float x0 = value - target;
            if (Mathf.Abs(x0) < 0.0005f && Mathf.Abs(velocity) < 0.005f)
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
            bool busy = Step(expandP, expandV, expanded ? 1f : 0f, dt);
            expandP = stepX; expandV = stepV;
            if (widthDamping < 0f) { widthP = expandP; widthV = expandV; }
            else
            {
                float d0 = springDamping, k0 = springStiffness;
                springDamping = widthDamping; springStiffness = widthStiffness;
                if (Step(widthP, widthV, expanded ? 1f : 0f, dt)) busy = true;
                widthP = stepX; widthV = stepV;
                springDamping = d0; springStiffness = k0;
            }
            for (int i = 0; i < selP.Length; i++)
            {
                if (Step(selP[i], selV[i], i == selectedIndex ? 1f : 0f, dt)) busy = true;
                selP[i] = stepX; selV[i] = stepV;
            }
            Layout();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void ApplySelection()
        {
            if (interactives != null)
                for (int i = 0; i < interactives.Length; i++)
                {
                    if (interactives[i] == null) continue;
                    if (i == selectedIndex) interactives[i]._Select(); else interactives[i]._Deselect();
                }
            if (iconsOn != null)
                for (int i = 0; i < iconsOn.Length; i++)
                {
                    if (iconsOn[i] != null) iconsOn[i].gameObject.SetActive(i == selectedIndex);
                    if (iconsOff[i] != null) iconsOff[i].gameObject.SetActive(i != selectedIndex);
                }
            ApplyColors();
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
                if (indicatorGraphics != null)
                    for (int k = 0; k < indicatorGraphics.Length; k++)
                        if (indicatorGraphics[k] == g) c.a = g.color.a;
                g.color = c;
            }
        }

        static void Put(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void Layout()
        {
            if (items == null || selP == null) return;
            float p = Mathf.Max(0f, expandP);
            int n = items.Length;

            // WideNavigationRailLayout: animated min width / full range width, spacing and item min height
            float minW = Mathf.LerpUnclamped(collapsedWidth, expandedMinWidth, widthP);
            float fullW = Mathf.LerpUnclamped(collapsedWidth, expandedMaxWidth, widthP);
            float spacing = Mathf.LerpUnclamped(collapsedItemSpacing, 0f, expandP);
            float itemMinH = Mathf.LerpUnclamped(collapsedItemMinHeight, expandedItemMinHeight, expandP);
            float padV = Mathf.Lerp(indicatorPadVCollapsed, indicatorPadVExpanded, Mathf.Clamp01(p)); // DynamicPaddingValues

            float widest = 0f;
            float y = topPadding;
            if (header != null && headerHeight > 0f)
            {
                Put(header, 0f, topPadding, header.sizeDelta.x, headerHeight);
                y += headerHeight + headerPadding;
            }
            for (int i = 0; i < n; i++)
            {
                bool startStyle = p >= 0.5f; // textStyle switches at progress 0.5
                float lw = startStyle ? startLabelW[i] : topLabelW[i];
                float lh = startStyle ? startLabelH[i] : topLabelH[i];
                float indPadSum = indicatorPadH * 2f;
                float indWProgress = Mathf.LerpUnclamped(iconSize, iconSize + lw + startIconToLabel, p) + indPadSum;
                float indW = Mathf.Round(indWProgress * Mathf.Max(0f, selP[i]));
                float indH = Mathf.LerpUnclamped(iconSize, Mathf.Max(iconSize, lh), p) + padV * 2f;

                float widthTop = Mathf.Max(lw, iconSize + itemHorizontalPadding * 2f + indPadSum);
                float widthStart = indWProgress + itemHorizontalPadding;
                float width = widthTop + (widthStart - widthTop) * p;
                float heightTop = indH + topIconToLabel + lh;
                float heightStart = indH;
                float height = Mathf.LerpUnclamped(heightTop, heightStart, p);

                float rippleX = Mathf.LerpUnclamped(itemHorizontalPadding, (itemHorizontalPadding + width - indWProgress) / 2f, p);
                float indX = itemHorizontalPadding;
                float iconX = itemHorizontalPadding + indicatorPadH;
                float iconYTop = padV;
                float iconYStart = (height - iconSize) / 2f - iconYTop;
                float iconY = Mathf.LerpUnclamped(0f, iconYStart, p) + iconYTop;
                float labelXTop = (iconSize + indPadSum + itemHorizontalPadding * 2f - lw) / 2f;
                float labelYTop = iconY + iconSize + padV + topIconToLabel;
                float offset = targetTop && p > 0f ? 0f : itemHorizontalPadding * (1f - p);
                float labelXStart = iconX + iconSize + startIconToLabel - offset;
                float labelYStart = (height - lh) / 2f;
                float labelX = p < 0.5f ? labelXTop : labelXStart * p;
                float labelY = p < 0.5f ? labelYTop : labelYStart;

                // measured item size (coerced by the min constraints; the content is centered in it)
                float itemW = Mathf.Max(width, minInteractive);
                float itemH = Mathf.Max(height, itemMinH);
                float ox = (itemW - width) / 2f;
                float oy = (itemH - height) / 2f;
                widest = Mathf.Max(widest, itemW + itemHorizontalPadding);

                Put(itemRects[i], 0f, y, itemW, itemH);
                Put(indicators[i], ox + indX, oy, indW, indH);
                Put(stateLayers[i], ox + rippleX, oy, indWProgress, indH);
                Put(ripples[i], ox + rippleX, oy, indWProgress, indH);
                Put(iconsOff[i], ox + iconX, oy + iconY, iconSize, iconSize);
                Put(iconsOn[i], ox + iconX, oy + iconY, iconSize, iconSize);
                if (labelsTop[i] != null)
                {
                    labelsTop[i].gameObject.SetActive(!startStyle);
                    labelsStart[i].gameObject.SetActive(startStyle);
                    Put(startStyle ? labelsStart[i] : labelsTop[i], ox + labelX, oy + labelY, lw, lh);
                    if (labelGroups[i] != null) labelGroups[i].alpha = 4f * (p - 0.5f) * (p - 0.5f);
                }
                if (indicatorGraphics[i] != null)
                {
                    Color c = indicatorGraphics[i].color;
                    c.a = Mathf.Clamp01(selP[i]);
                    indicatorGraphics[i].color = c;
                }
                y += itemH + (i < n - 1 ? spacing : 0f);
            }

            // rail width: collapsed fixed, expanded as wide as the widest element within the animated range
            float railW = minW;
            if (widest > minW && widest > expandedMinWidth && (expanded || widthP > 0f))
                railW = Mathf.Min(fullW, Mathf.Max(widest, expandedMinWidth));
            railW = Mathf.Max(railW, minW);
            if (rail != null) rail.sizeDelta = new Vector2(railW, rail.sizeDelta.y);
        }
    }
}
