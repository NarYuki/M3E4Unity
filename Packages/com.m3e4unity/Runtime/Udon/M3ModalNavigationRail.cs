#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Modal wide navigation rail (WideNavigationRail.kt ModalWideNavigationRail / ModalWideNavigationRailImpl).
    /// A positionProgress animates to the target state with DefaultEffects; the modal (a second, expanded
    /// rail over a scrim) shows while it is above 0, and is expanded while it is at least 0.3. Without
    /// hideOnCollapse the collapsed rail stays in the layout and only shows its items while collapsed;
    /// with hideOnCollapse the modal rail slides in from its own width offscreen (anchored draggable,
    /// DefaultSpatial). The scrim fades with DefaultEffects and closes the rail when tapped.
    /// Events: _Open, _Close, _Toggle, _M3NavChanged (from the rails).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3ModalNavigationRail : UdonSharpBehaviour
#else
    public class M3ModalNavigationRail : M3BehaviourBase
#endif
    {
        public bool open;
        public bool hideOnCollapse;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts")]
        public M3NavigationRail baseRail;        // the collapsed rail in the layout (null with hideOnCollapse)
        public GameObject[] baseItems;           // its items, shown only while collapsed
        public M3NavigationRail modalRail;
        public GameObject overlay;
        public CanvasGroup scrim;
        public RectTransform modalRect;          // slid for hideOnCollapse
        public float modalWidth = 360f;

        [Header("Motion")]
        public float effectsStiffness = 1600f;   // DefaultEffects
        public float spatialDamping = 0.8f;      // DefaultSpatial
        public float spatialStiffness = 380f;

        float progress;
        float progressV;
        float scrimA;
        float scrimV;
        float slide;      // 0 = offscreen, 1 = in place
        float slideV;
        bool modalExpanded;
        float lastTime;
        bool ticking;

        void Start() { Snap(); }
        public void _M3Refresh() { Snap(); }

        void Snap()
        {
            progress = open ? 1f : 0f; progressV = 0f;
            modalExpanded = open || hideOnCollapse;
            scrimA = open ? 1f : 0f; scrimV = 0f;
            slide = open ? 1f : 0f; slideV = 0f;
            if (modalRail != null) { modalRail.expanded = modalExpanded; modalRail._M3Refresh(); }
            Apply();
        }

        public void _Open() { open = true; Kick(); }
        public void _Close() { open = false; Kick(); }
        public void _Toggle() { open = !open; Kick(); }

        /// <summary>A rail's selection changed: keep both rails on the same destination.</summary>
        public void _M3NavChanged()
        {
            int index = -1;
            if (modalRail != null && overlay != null && overlay.activeSelf) index = modalRail.selectedIndex;
            else if (baseRail != null) index = baseRail.selectedIndex;
            if (index < 0 || syncing) return;
            syncing = true;
            if (baseRail != null && baseRail.selectedIndex != index) baseRail.Select(index);
            if (modalRail != null && modalRail.selectedIndex != index) modalRail.Select(index);
            syncing = false;
            if (changeListener != null) changeListener.SendCustomEvent("_M3NavChanged");
        }

        bool syncing;

        void Kick()
        {
            if (overlay != null && open) overlay.SetActive(true);
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        float stepX;
        float stepV;

        bool Step(float x, float v, float target, float dt, float z, float k, float threshold)
        {
            float x0 = x - target;
            if (Mathf.Abs(x0) < threshold && Mathf.Abs(v) < threshold * 10f) { stepX = target; stepV = 0f; return false; }
            float w = Mathf.Sqrt(k);
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + v) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                stepX = target + e * (x0 * cs + sc * sn);
                stepV = -z * w * (stepX - target) + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else
            {
                float cb = v + w * x0;
                float e = Mathf.Exp(-w * dt);
                stepX = target + (x0 + cb * dt) * e;
                stepV = (cb - w * (x0 + cb * dt)) * e;
            }
            return true;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            // positionProgress (animateFloatAsState default visibility threshold 0.01)
            if (Step(progress, progressV, open ? 1f : 0f, dt, 1f, effectsStiffness, 0.01f)) busy = true;
            progress = stepX; progressV = stepV;
            bool expandedNow = hideOnCollapse || progress >= 0.3f;
            if (expandedNow != modalExpanded)
            {
                modalExpanded = expandedNow;
                if (modalRail != null) { if (modalExpanded) modalRail._Expand(); else modalRail._Collapse(); }
            }
            // scrim: hideOnCollapse -> the modal state's target, else modalExpanded
            bool scrimVisible = hideOnCollapse ? open : modalExpanded;
            if (Step(scrimA, scrimV, scrimVisible ? 1f : 0f, dt, 1f, effectsStiffness, 0.01f)) busy = true;
            scrimA = stepX; scrimV = stepV;
            if (hideOnCollapse)
            {
                if (Step(slide, slideV, open ? 1f : 0f, dt, spatialDamping, spatialStiffness, 1f / Mathf.Max(1f, modalWidth))) busy = true;
                slide = stepX; slideV = stepV;
            }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            bool collapsed = progress <= 0f && !open;
            if (overlay != null) overlay.SetActive(!collapsed);
            if (baseItems != null)
                for (int i = 0; i < baseItems.Length; i++)
                    if (baseItems[i] != null) baseItems[i].SetActive(collapsed);
            if (scrim != null)
            {
                scrim.alpha = Mathf.Clamp01(scrimA);
                scrim.blocksRaycasts = !collapsed;
            }
            if (modalRect != null && hideOnCollapse)
                modalRect.anchoredPosition = new Vector2(-modalWidth * (1f - slide), modalRect.anchoredPosition.y);
        }
    }
}
