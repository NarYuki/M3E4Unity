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
    /// <see cref="target"/> and send _Apply. Changes animate with
    /// ProgressIndicatorDefaults.ProgressAnimationSpec (tween 500 ms, linear) when
    /// <see cref="animate"/> is on. Indeterminate indicators are animated by the shader alone.
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
        public float durationSeconds = 0.5f;

        [Tooltip("Images showing the progress, each with frameCount sprites (frames[part * frameCount + frame]).")]
        public Image[] parts;
        public Sprite[] frames;
        public int frameCount;

        float value;
        float from;
        float startTime;
        bool ticking;

        void Start()
        {
            value = target;
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

        public void _Apply()
        {
            target = Mathf.Clamp01(target);
            if (!animate)
            {
                value = target;
                Show();
                return;
            }
            from = value;
            startTime = Time.time;
            if (!ticking)
            {
                ticking = true;
                SendCustomEventDelayedFrames("_Tick", 1);
            }
        }

        public void _Tick()
        {
            float t = durationSeconds > 0f ? Mathf.Clamp01((Time.time - startTime) / durationSeconds) : 1f;
            value = Mathf.Lerp(from, target, t);
            Show();
            if (t < 1f) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Show()
        {
            if (parts == null || frames == null || frameCount <= 0) return;
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
