#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Open / close animation of a popup (DropdownMenuPopupContent in Menu.kt): scale between
    /// closedScale and 1 with a spatial spring and alpha between 0 and 1 with an effects spring,
    /// around the pivot of the target (the transform origin). Closed popups are deactivated.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Popup : UdonSharpBehaviour
#else
    public class M3Popup : M3BehaviourBase
#endif
    {
        public RectTransform target;
        public CanvasGroup group;
        public bool expanded;
        public float closedScale = 0.8f;
        public float spatialDamping = 0.6f;
        public float spatialStiffness = 800f;
        public float effectsStiffness = 3800f;
        [Tooltip("Closes itself this many seconds after opening (snackbar durations); 0 = never.")]
        public float autoCloseSeconds;

        float scale = 1f;
        float scaleV;
        float alpha = 1f;
        float alphaV;
        float lastTime;
        bool ticking;
        bool initialized;
        float openedAt;

        void Start()
        {
            Init();
        }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            scale = expanded ? 1f : closedScale;
            alpha = expanded ? 1f : 0f;
            Apply();
        }

        public void _Open() { Init(); expanded = true; Kick(); }
        public void _Close() { Init(); expanded = false; Kick(); }
        public void _Toggle() { Init(); expanded = !expanded; Kick(); }

        void Kick()
        {
            if (target != null && expanded) target.gameObject.SetActive(true);
            if (expanded && autoCloseSeconds > 0f)
            {
                openedAt = Time.time;
                SendCustomEventDelayedSeconds("_AutoClose", autoCloseSeconds);
            }
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _AutoClose()
        {
            // only the timer of the latest opening closes the popup (earlier timers fire too early)
            if (!expanded || Time.time - openedAt < autoCloseSeconds - 0.05f) return;
            _Close();
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            float ts = expanded ? 1f : closedScale;
            float x = scale - ts;
            if (Mathf.Abs(x) > 0.0005f || Mathf.Abs(scaleV) > 0.005f)
            {
                Step(x, scaleV, dt, spatialDamping, spatialStiffness);
                scale = ts + stepX; scaleV = stepV; busy = true;
            }
            else { scale = ts; scaleV = 0f; }
            float ta = expanded ? 1f : 0f;
            x = alpha - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(alphaV) > 0.01f)
            {
                Step(x, alphaV, dt, 1f, effectsStiffness);
                alpha = ta + stepX; alphaV = stepV; busy = true;
            }
            else { alpha = ta; alphaV = 0f; }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else
            {
                ticking = false;
                if (!expanded && target != null) target.gameObject.SetActive(false);
            }
        }

        float stepX;
        float stepV;

        void Step(float x0, float v0, float dt, float z, float k)
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
        }

        void Apply()
        {
            if (target != null) target.localScale = new Vector3(scale, scale, 1f);
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(alpha);
                group.interactable = expanded;
                group.blocksRaycasts = expanded;
            }
        }
    }
}
