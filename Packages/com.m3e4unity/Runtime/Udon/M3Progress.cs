#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Determinate progress (linear / circular / wavy progress indicators, determinate loading
    /// indicator). Set the value with SetProgress(value) from UdonSharp, or set
    /// <see cref="target"/> and send _Apply. When <see cref="animate"/> is on, changes animate with
    /// the recommended spec of the indicator:
    /// <list type="bullet">
    /// <item>flat indicators: ProgressIndicatorDefaults.ProgressAnimationSpec, a spring (no bounce,
    /// StiffnessVeryLow, visibility threshold 1/1000);</item>
    /// <item>wavy indicators: WavyProgressIndicatorDefaults.ProgressAnimationSpec, tween 500 ms linear.</item>
    /// </list>
    /// Wavy indicators also animate their amplitude (indicatorAmplitude: none below 10 % and from
    /// 95 %) with IncreasingAmplitudeAnimationSpec (500 ms EasingStandard) or
    /// DecreasingAmplitudeAnimationSpec (500 ms EmphasizedAccelerate); like the Compose node, a new
    /// amplitude target waits for a running amplitude animation to finish.
    /// The value reaches the shader through the indicator's own material (_M3Dyn = progress, amplitude).
    /// Indeterminate indicators are animated by the shader alone.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Progress : UdonSharpBehaviour
#else
    public class M3Progress : M3BehaviourBase
#endif
    {
        [Range(0f, 1f)] public float target;
        public bool animate = true;
        [Tooltip("Wavy indicator: tween progress and animate the wave amplitude.")]
        public bool wavy;
        public float durationSeconds = 0.5f;
        public float springStiffness = 50f;

        [Tooltip("Images showing the progress; they share dynamicMaterial.")]
        public Image[] parts;
        [Tooltip("This indicator's own M3Shape material (_M3DynOn = 1).")]
        public Material dynamicMaterial;

        [Tooltip("Legacy (0.1.0) builds: sprites per frame, frames[part * frameCount + frame].")]
        public Sprite[] frames;
        public int frameCount;

        float value;
        float velocity;
        float from;
        float startTime;
        float lastTime;
        bool ticking;
        bool started;

        float amplitude = -1f;
        float ampFrom;
        float ampTo;
        float ampStart;
        bool ampAnimating;

        void Start()
        {
#if !UDONSHARP
            // duplicated indicators would share the material asset: draw with a private copy
            if (dynamicMaterial != null)
            {
                dynamicMaterial = new Material(dynamicMaterial);
                for (int i = 0; i < parts.Length; i++) if (parts[i] != null) parts[i].material = dynamicMaterial;
            }
#endif
            started = true;
            value = Mathf.Clamp01(target);
            amplitude = AmplitudeFor(value);
            ampTo = amplitude;
            Show();
        }

        public void SetProgress(float progress)
        {
            target = Mathf.Clamp01(progress);
            _Apply();
        }

        public float GetProgress()
        {
            return value;
        }

        /// <summary>WavyProgressIndicatorDefaults.indicatorAmplitude.</summary>
        float AmplitudeFor(float progress)
        {
            if (!wavy) return 0f;
            return progress <= 0.1f || progress >= 0.95f ? 0f : 1f;
        }

        public void _Apply()
        {
            target = Mathf.Clamp01(target);
            if (!started) return;
            if (!animate)
            {
                value = target;
                velocity = 0f;
                amplitude = AmplitudeFor(value);
                ampTo = amplitude;
                ampAnimating = false;
                Show();
                return;
            }
            from = value;
            startTime = Time.time;
            Kick();
        }

        void Kick()
        {
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
            bool busy = false;

            if (wavy)
            {
                float t = durationSeconds > 0f ? Mathf.Clamp01((now - startTime) / durationSeconds) : 1f;
                value = Mathf.Lerp(from, target, t);
                if (t < 1f) busy = true;
            }
            else
            {
                // critically damped spring (DampingRatioNoBouncy), closed form over dt
                float w = Mathf.Sqrt(springStiffness);
                float x0 = value - target;
                float cb = velocity + w * x0;
                float e = Mathf.Exp(-w * dt);
                float x = (x0 + cb * dt) * e;
                velocity = (cb - w * (x0 + cb * dt)) * e;
                if (Mathf.Abs(x) < 0.001f && Mathf.Abs(velocity) < 0.01f) { value = target; velocity = 0f; }
                else { value = target + x; busy = true; }
            }

            if (wavy)
            {
                float ampTarget = AmplitudeFor(value);
                if (!ampAnimating && ampTarget != ampTo)
                {
                    ampFrom = amplitude;
                    ampTo = ampTarget;
                    ampStart = now;
                    ampAnimating = true;
                }
                if (ampAnimating)
                {
                    float at = Mathf.Clamp01((now - ampStart) / 0.5f);
                    // Increasing: EasingStandard (0.2, 0, 0, 1); decreasing: EmphasizedAccelerate (0.3, 0, 0.8, 0.15)
                    float k = ampTo > ampFrom ? Bezier(0.2f, 0f, 0f, 1f, at) : Bezier(0.3f, 0f, 0.8f, 0.15f, at);
                    amplitude = Mathf.Lerp(ampFrom, ampTo, k);
                    if (at >= 1f) ampAnimating = false;
                    busy = true;
                }
            }

            Show();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Show()
        {
            if (parts == null) return;
            if (dynamicMaterial != null)
            {
                Vector4 v = new Vector4(value, Mathf.Max(amplitude, 0f), 0f, 0f);
                dynamicMaterial.SetVector("_M3Dyn", v);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null) continue;
                    // masked graphics render with a stencil copy of the material
                    Material m = parts[i].materialForRendering;
                    if (m != null && m != dynamicMaterial) m.SetVector("_M3Dyn", v);
                }
                return;
            }
            if (frames == null || frameCount <= 0) return;
            int f = Mathf.Clamp(Mathf.RoundToInt(value * (frameCount - 1)), 0, frameCount - 1);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Sprite s = frames[i * frameCount + f];
                if (parts[i].sprite != s) parts[i].sprite = s;
                parts[i].enabled = s != null;
            }
        }
    }
}
