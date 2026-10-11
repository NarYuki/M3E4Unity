#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// AnimatedVisibility for one piece of content, driven by M3Interactive (revealWhenSelected) or by
    /// code (SetVisible / SnapVisible):
    /// <list type="bullet">
    /// <item>mode 0, expandHorizontally / shrinkHorizontally: the width of the slot (a LayoutElement
    /// clipped by a RectMask2D) springs between 0 and the content width; the content stays at the
    /// expandFrom edge (start or end);</item>
    /// <item>mode 1, scaleIn: the content scales from initialScale around its pivot (the transform
    /// origin);</item>
    /// </list>
    /// with fadeIn / fadeOut on a CanvasGroup. Springs are given as damping ratio and stiffness; a
    /// zero stiffness snaps. exitNone hides at once (ExitTransition.None).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Reveal : UdonSharpBehaviour
#else
    public class M3Reveal : M3BehaviourBase
#endif
    {
        public bool visible;
        [Tooltip("0 = expand horizontally (slot width), 1 = scale in.")]
        public int mode;
        public LayoutElement slot;
        public float fullSize = 18f;
        public RectTransform scaleTarget;
        public float initialScale;
        public CanvasGroup fade;
        public GameObject content;
        public bool exitNone;

        public float enterDamping = 0.6f, enterStiffness = 800f;   // size / scale in
        public float exitDamping = 0.6f, exitStiffness = 800f;     // size out
        public float fadeInStiffness = 1600f, fadeOutStiffness = 1600f;

        float size;      // 0..1 of fullSize (mode 0) or the scale progress (mode 1)
        float sizeV;
        float alpha;
        float alphaV;
        float lastTime;
        bool ticking;
        bool started;

        void Start()
        {
            started = true;
            SnapVisible(visible);
        }

        public void SnapVisible(bool v)
        {
            visible = v;
            size = v ? 1f : 0f;
            sizeV = 0f;
            alpha = size;
            alphaV = 0f;
            Apply();
        }

        public void SetVisible(bool v)
        {
            if (!started) { visible = v; return; }
            if (v == visible) return;
            visible = v;
            if (!v && exitNone) { SnapVisible(false); return; }
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            Apply();
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _M3ThemeChanged() { }

        public void _Show() { SetVisible(true); }
        public void _Hide() { SetVisible(false); }

        float stepX;
        float stepV;

        bool Spring(float x0, float v0, float dt, float z, float k, float threshold)
        {
            if (k <= 0f) { stepX = 0f; stepV = 0f; return false; }
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
            float target = visible ? 1f : 0f;
            bool busy = false;
            // size springs use a 1 px visibility threshold (IntSize), scale and alpha 1/1000
            float sizeThreshold = mode == 0 && fullSize > 0f ? 0.5f / fullSize : 0.001f;
            if (Spring(size - target, sizeV, dt, visible ? enterDamping : exitDamping, visible ? enterStiffness : exitStiffness, sizeThreshold)) busy = true;
            size = target + stepX; sizeV = stepV;
            if (Spring(alpha - target, alphaV, dt, 1f, visible ? fadeInStiffness : fadeOutStiffness, 0.002f)) busy = true;
            alpha = target + stepX; alphaV = stepV;
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            bool shown = visible || size > 0.0001f || alpha > 0.0001f;
            if (content != null && content.activeSelf != shown) content.SetActive(shown);
            if (mode == 0)
            {
                if (slot != null)
                {
                    float w = Mathf.Max(0f, size) * fullSize;
                    slot.minWidth = w;
                    slot.preferredWidth = w;
                }
            }
            else if (scaleTarget != null)
            {
                float s = initialScale + (1f - initialScale) * size;
                scaleTarget.localScale = new Vector3(s, s, 1f);
            }
            if (fade != null) fade.alpha = Mathf.Clamp01(alpha);
        }
    }
}
