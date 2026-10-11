#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Top app bar scroll behaviour (AppBar.kt SingleRowTopAppBar / TwoRowsTopAppBar). A linked
    /// ScrollRect drives the state: single-row bars switch to the scrolled container color once
    /// content is under them (overlappedFraction > 0.01, DefaultEffects); two-row bars collapse with
    /// the scroll offset (heightOffset), cross-fading the titles (bottom 1 - f, top
    /// TopTitleAlphaEasing(f)) and lerping the container color with FastOutLinearIn(f). When the drag
    /// and its fling end with the bar partly collapsed, the bar settles (settleAppBar: snapAnimationSpec
    /// = DefaultEffects) to expanded below a collapsed fraction of 0.5, else to collapsed; here the bar
    /// follows the content offset, so the content is scrolled to 0 or to the collapse range.
    /// Events: _Scrolled (ScrollRect.onValueChanged), _BeginDrag / _EndDrag (EventTrigger on the ScrollRect).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TopAppBar : UdonSharpBehaviour
#else
    public class M3TopAppBar : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public ScrollRect scrollRect;
        public bool twoRows;
        public RectTransform bar;
        public RectTransform bottomRow;
        public float collapsedHeight = 64f;
        public float expandedHeight = 112f;
        public CanvasGroup topTitle;
        public CanvasGroup bottomTitle;
        [Tooltip("colored[0] is the container; its slots 0 / 1 are the container and scrolled container colors.")]
        public Graphic[] colored;
        public int[] colorIndices;
        public float effectsStiffness = 1600f;
        public float snapStiffness = 1600f;   // TopAppBarDefaults.snapAnimationSpec: DefaultEffects

        bool settlePending;
        bool settling;
        float settleTarget;
        float settleV;
        float settleTime;

        float fraction;
        float colorT;
        float colorV;
        float lastTime;
        bool ticking;

        void Start()
        {
            Apply();
        }

        public void _M3ThemeChanged() { Apply(); }
        public void _M3Refresh() { Apply(); }

        public void _Scrolled()
        {
            if (scrollRect == null || scrollRect.content == null) return;
            // scrolled distance from the top of the content (dp)
            float scrolled = Mathf.Max(0f, scrollRect.content.anchoredPosition.y);
            if (twoRows)
            {
                float range = expandedHeight - collapsedHeight;
                float offset = -Mathf.Min(scrolled, range); // heightOffset in [heightOffsetLimit, 0]
                fraction = range > 0f ? -offset / range : 0f; // collapsedFraction
                if (bottomRow != null) bottomRow.sizeDelta = new Vector2(bottomRow.sizeDelta.x, range + offset);
                if (bar != null) bar.sizeDelta = new Vector2(bar.sizeDelta.x, expandedHeight + offset);
                Apply();
            }
            else
            {
                // overlappedFraction > 0.01: the color animates to the scrolled container color
                float target = scrolled > 0.5f ? 1f : 0f;
                if (target != colorTarget)
                {
                    colorTarget = target;
                    if (!ticking)
                    {
                        ticking = true;
                        lastTime = Time.time;
                        SendCustomEventDelayedFrames("_Tick", 1);
                    }
                }
            }
        }

        float colorTarget;

        public void _BeginDrag()
        {
            settlePending = false;
            settling = false;
        }

        public void _EndDrag()
        {
            if (!twoRows || scrollRect == null) return;
            settlePending = true;
            settleTime = Time.time;
            SendCustomEventDelayedFrames("_Settle", 1);
        }

        float Scrolled()
        {
            return scrollRect != null && scrollRect.content != null ? scrollRect.content.anchoredPosition.y : 0f;
        }

        public void _Settle()
        {
            if (!settlePending && !settling) return;
            float now = Time.time;
            float range = expandedHeight - collapsedHeight;
            if (settlePending)
            {
                // wait for the fling to end (onPostFling), then snap if the bar is partly collapsed
                if (Mathf.Abs(scrollRect.velocity.y) > 1f) { settleTime = now; SendCustomEventDelayedFrames("_Settle", 1); return; }
                settlePending = false;
                float f = range > 0f ? Mathf.Clamp01(Scrolled() / range) : 0f;
                if (f < 0.01f || f >= 1f) return;
                settleTarget = f < 0.5f ? 0f : range;
                settleV = 0f;
                settling = true;
                settleTime = now;
            }
            float dt = Mathf.Min(now - settleTime, 0.05f);
            settleTime = now;
            float w = Mathf.Sqrt(snapStiffness);
            float x0 = Scrolled() - settleTarget;
            float cb = settleV + w * x0;
            float e = Mathf.Exp(-w * dt);
            float x = (x0 + cb * dt) * e;
            settleV = (cb - w * (x0 + cb * dt)) * e;
            bool busy = Mathf.Abs(x) > 0.5f || Mathf.Abs(settleV) > 1f;
            if (!busy) { x = 0f; settleV = 0f; settling = false; }
            Vector2 pos = scrollRect.content.anchoredPosition;
            scrollRect.content.anchoredPosition = new Vector2(pos.x, settleTarget + x);
            scrollRect.velocity = Vector2.zero;
            _Scrolled();
            if (busy) SendCustomEventDelayedFrames("_Settle", 1);
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            // DefaultEffects: critically damped spring
            float w = Mathf.Sqrt(effectsStiffness);
            float x0 = colorT - colorTarget;
            float cb = colorV + w * x0;
            float e = Mathf.Exp(-w * dt);
            float x = (x0 + cb * dt) * e;
            colorV = (cb - w * (x0 + cb * dt)) * e;
            colorT = colorTarget + x;
            bool busy = Mathf.Abs(x) > 0.002f || Mathf.Abs(colorV) > 0.01f;
            if (!busy) { colorT = colorTarget; colorV = 0f; }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        // CubicBezierEasing evaluated by bisection on x
        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float lo = 0f;
            float hi = 1f;
            float t = x;
            for (int i = 0; i < 24; i++)
            {
                t = (lo + hi) * 0.5f;
                float u = 1f - t;
                float bx = 3f * u * u * t * x1 + 3f * u * t * t * x2 + t * t * t;
                if (bx < x) lo = t; else hi = t;
            }
            float v = 1f - t;
            return 3f * v * v * t * y1 + 3f * v * t * t * y2 + t * t * t;
        }

        void Apply()
        {
            float f = twoRows ? fraction : colorT;
            if (theme != null && colored != null && colored.Length > 0 && colored[0] != null && colorIndices != null && colorIndices.Length >= 2)
            {
                Graphic container = colored[0];
                int containerColor = colorIndices[0];
                int scrolledContainerColor = colorIndices[1];
                // TopAppBarColors.containerColor: lerp(container, scrolled, FastOutLinearInEasing(f))
                float k = twoRows ? Bezier(0.4f, 0f, 1f, 1f, f) : f;
                // Color lerp interpolates in Oklab
                container.color = theme.MixOklab(theme.Get(containerColor), theme.Get(scrolledContainerColor), k);
            }
            if (twoRows)
            {
                // TopTitleAlphaEasing = CubicBezierEasing(.8, 0, .8, .15)
                if (topTitle != null) topTitle.alpha = Bezier(0.8f, 0f, 0.8f, 0.15f, fraction);
                if (bottomTitle != null) bottomTitle.alpha = 1f - fraction;
            }
        }
    }
}
