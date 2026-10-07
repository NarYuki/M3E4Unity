#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Modal sheet / drawer motion: the sheet slides along `direction` (a unit vector in canvas
    /// units, e.g. (0,-1) for a bottom sheet that hides downwards) with a tween
    /// (BottomSheetAnimationSpec: 300 ms FastOutSlowIn; drawers use their own spec), and the scrim
    /// fades with an effects spring (ModalBottomSheet: DefaultEffects). Hidden sheets are deactivated.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Sheet : UdonSharpBehaviour
#else
    public class M3Sheet : M3BehaviourBase
#endif
    {
        public RectTransform sheet;
        public CanvasGroup scrim;
        public GameObject root;
        public bool open = true;
        public Vector2 hiddenOffset = new Vector2(0f, -400f);
        public float durationSeconds = 0.3f;
        [Tooltip("Cubic bezier control points of the slide easing (FastOutSlowIn = 0.4, 0, 0.2, 1).")]
        public Vector4 easing = new Vector4(0.4f, 0f, 0.2f, 1f);
        public float scrimStiffness = 1600f;
        [Header("Spring mode (navigation drawers: open DefaultSpatial, close FastEffects; scrim follows the offset)")]
        public bool useSpring;
        public float openDamping = 0.8f;
        public float openStiffness = 380f;
        public float closeDamping = 1f;
        public float closeStiffness = 3800f;
        [Tooltip("Overshoot past the open position scales the sheet up from this edge (horizontalScaleUp) instead of leaving a gap.")]
        public float sheetWidth = 360f;
        float velocity;

        Vector2 shownPosition;
        float t = 1f;
        float from;
        float to = 1f;
        float startTime;
        float scrimValue = 1f;
        float scrimV;
        float lastTime;
        bool ticking;
        bool initialized;

        void Start() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (sheet != null) shownPosition = sheet.anchoredPosition;
            t = open ? 1f : 0f;
            to = t;
            scrimValue = t;
            Apply();
            if (!open && root != null) root.SetActive(false);
        }

        public void _Open() { Init(); open = true; Go(1f); }
        public void _Close() { Init(); open = false; Go(0f); }
        public void _Toggle() { if (open) _Close(); else _Open(); }

        void Go(float target)
        {
            if (root != null) root.SetActive(true);
            from = t;
            to = target;
            startTime = Time.time;
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float lo = 0f;
            float hi = 1f;
            float s = x;
            for (int i = 0; i < 24; i++)
            {
                s = (lo + hi) * 0.5f;
                float u = 1f - s;
                float bx = 3f * u * u * s * x1 + 3f * u * s * s * x2 + s * s * s;
                if (bx < x) lo = s; else hi = s;
            }
            float v = 1f - s;
            return 3f * v * v * s * y1 + 3f * v * s * s * y2 + s * s * s;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            float p;
            if (useSpring)
            {
                float z = open ? openDamping : closeDamping;
                float k = open ? openStiffness : closeStiffness;
                float w0 = Mathf.Sqrt(k);
                float y0 = t - to;
                float y;
                float v;
                if (z < 1f)
                {
                    float wd = w0 * Mathf.Sqrt(1f - z * z);
                    float sc = (z * w0 * y0 + velocity) / wd;
                    float ex = Mathf.Exp(-z * w0 * dt);
                    y = ex * (y0 * Mathf.Cos(wd * dt) + sc * Mathf.Sin(wd * dt));
                    v = -z * w0 * y + ex * (-y0 * wd * Mathf.Sin(wd * dt) + sc * wd * Mathf.Cos(wd * dt));
                }
                else
                {
                    float cb0 = velocity + w0 * y0;
                    float ex = Mathf.Exp(-w0 * dt);
                    y = (y0 + cb0 * dt) * ex;
                    v = (cb0 - w0 * (y0 + cb0 * dt)) * ex;
                }
                t = to + y;
                velocity = v;
                p = Mathf.Abs(y) < 0.0005f && Mathf.Abs(v) < 0.005f ? 1f : 0f;
                if (p >= 1f) { t = to; velocity = 0f; }
            }
            else
            {
                p = durationSeconds > 0f ? Mathf.Clamp01((now - startTime) / durationSeconds) : 1f;
                t = Mathf.Lerp(from, to, Bezier(easing.x, easing.y, easing.z, easing.w, p));
            }
            // scrim: critically damped effects spring towards the open state
            float w = Mathf.Sqrt(scrimStiffness);
            float x0 = scrimValue - to;
            float cb = scrimV + w * x0;
            float e = Mathf.Exp(-w * dt);
            float x = (x0 + cb * dt) * e;
            scrimV = (cb - w * (x0 + cb * dt)) * e;
            scrimValue = to + x;
            bool scrimBusy = Mathf.Abs(x) > 0.002f || Mathf.Abs(scrimV) > 0.01f;
            if (!scrimBusy) { scrimValue = to; scrimV = 0f; }
            if (useSpring) { scrimValue = Mathf.Clamp01(t); scrimBusy = false; }
            Apply();
            if (p < 1f || scrimBusy) SendCustomEventDelayedFrames("_Tick", 1);
            else
            {
                ticking = false;
                if (!open && root != null) root.SetActive(false);
            }
        }

        void Apply()
        {
            if (sheet != null)
            {
                sheet.anchoredPosition = shownPosition + hiddenOffset * (1f - Mathf.Min(t, 1f));
                // DrawerSheet.horizontalScaleUp: overshoot widens the sheet instead of moving it
                float over = Mathf.Max(0f, t - 1f) * Mathf.Abs(hiddenOffset.x);
                sheet.localScale = new Vector3(sheetWidth > 0f ? 1f + over / sheetWidth : 1f, 1f, 1f);
            }
            if (scrim != null)
            {
                scrim.alpha = Mathf.Clamp01(scrimValue);
                scrim.blocksRaycasts = open;
            }
        }
    }
}
