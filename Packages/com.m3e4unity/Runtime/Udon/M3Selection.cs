#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Checkbox, radio button and switch (Checkbox.kt, RadioButton.kt, Switch.kt).
    /// kind 0 = checkbox (states off / on / indeterminate), 1 = radio, 2 = switch.
    /// The hover state layer and ripple come from an M3Interactive on the same object.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Selection : UdonSharpBehaviour
#else
    public class M3Selection : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public Selectable selectable;
        public int kind;
        [Tooltip("0 off, 1 on, 2 indeterminate (checkbox only).")]
        public int state;
        [Tooltip("Radio buttons that share a group (all of them, including this one).")]
        public M3Selection[] group;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts")]
        public Image mark;           // checkbox check mark, radio dot, switch thumb
        public RectTransform markRect;
        public RectTransform thumbRoot; // switch: moves with the thumb (state layer, ripple, icon)
        [Tooltip("Checkbox: check frames (fraction 0..1) followed by indeterminate frames (gravitation 0..1).")]
        public Sprite[] markFrames;
        public int checkFrameCount;
        public Graphic thumbIcon;    // switch: optional icon in the thumb

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("Palette indices, 6 per graphic: off, on, indeterminate, disabled off, disabled on, disabled indeterminate.")]
        public int[] colorIndices;

        [Header("Geometry (dp)")]
        public float radioDot = 12f;          // RadioButtonDotSize
        public float switchWidth = 52f;
        public float switchHeight = 32f;
        public float thumbUnchecked = 16f;    // 24 with an icon
        public float thumbChecked = 24f;
        public float thumbPressed = 28f;
        public float trackOutline = 2f;
        public float springDamping = 0.6f;    // FastSpatial (expressive)
        public float springStiffness = 800f;
        public float checkDamping = 0.8f;     // DefaultSpatial (expressive)
        public float checkStiffness = 380f;

        // animated values
        float a, av, aTarget;   // checkbox check fraction / radio dot size / switch thumb offset
        float b, bv, bTarget;   // checkbox gravitation / switch thumb size
        float lastTime;
        bool ticking;
        bool pressed;
        float uncheckAt = -1f;

        void Start()
        {
            Targets();
            a = aTarget;
            b = bTarget;
            av = 0f;
            bv = 0f;
            Apply();
            ApplyColors();
        }

        bool Interactable()
        {
            return selectable == null || selectable.interactable;
        }

        public void _Click()
        {
            if (!Interactable()) return;
            if (kind == 1)
            {
                if (state == 1) return;
                state = 1;
                if (group != null)
                {
                    for (int i = 0; i < group.Length; i++)
                    {
                        if (group[i] != null && group[i] != this) group[i]._SetOff();
                    }
                }
            }
            else
            {
                // TriStateCheckbox / Checkbox: off -> on, on -> off, indeterminate -> on
                state = state == 1 ? 0 : 1;
            }
            Changed();
            if (changeListener != null) changeListener.SendCustomEvent("_M3SelectionChanged");
        }

        public void _SetOff() { if (state != 0) { state = 0; Changed(); } }
        public void _SetOn() { if (state != 1) { state = 1; Changed(); } }
        public void _SetIndeterminate() { if (kind == 0 && state != 2) { state = 2; Changed(); } }

        public void _Down()
        {
            if (!Interactable()) return;
            pressed = true;
            if (kind == 2) { Targets(); a = aTarget; b = bTarget; av = 0f; bv = 0f; Apply(); }
        }

        public void _Up()
        {
            if (!pressed) return;
            pressed = false;
            if (kind == 2) Changed();
        }

        public void _M3ThemeChanged() { ApplyColors(); }
        public void _M3Refresh() { ApplyColors(); Changed(); }

        void Changed()
        {
            Targets();
            ApplyColors();
            if (kind == 0 && state == 0)
            {
                // Checkbox: unchecking snaps after SnapAnimationDelay (100 ms)
                uncheckAt = Time.time + 0.1f;
            }
            Start_();
        }

        void Start_()
        {
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        void Targets()
        {
            if (kind == 0)
            {
                aTarget = state == 0 ? 0f : 1f;
                bTarget = state == 2 ? 1f : 0f;
            }
            else if (kind == 1)
            {
                aTarget = state == 1 ? radioDot : 0f;
            }
            else
            {
                bool hasIcon = thumbIcon != null;
                bool on = state == 1;
                float size = pressed ? thumbPressed : (hasIcon || on ? thumbChecked : thumbUnchecked);
                float thumbPadding = (switchHeight - thumbChecked) / 2f;
                float minBound = (switchHeight - size) / 2f;
                float maxBound = switchWidth - thumbChecked - thumbPadding;
                float offset = pressed ? (on ? maxBound - trackOutline : trackOutline) : (on ? maxBound : minBound);
                aTarget = offset;
                bTarget = size;
            }
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            if (kind == 0 && state == 0)
            {
                if (now >= uncheckAt) { a = 0f; av = 0f; b = 0f; bv = 0f; }
                else busy = true;
            }
            else
            {
                float damping = kind == 0 ? checkDamping : springDamping;
                float stiffness = kind == 0 ? checkStiffness : springStiffness;
                if (Step(0, dt, damping, stiffness)) busy = true;
                // checkbox gravitation: snap when coming from off, else DefaultSpatial
                if (Step(1, dt, damping, stiffness)) busy = true;
            }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        // Exact spring step over dt from the current value and velocity.
        bool Step(int which, float dt, float z, float k)
        {
            float x0 = which == 0 ? a - aTarget : b - bTarget;
            float v0 = which == 0 ? av : bv;
            if (Mathf.Abs(x0) < 0.001f && Mathf.Abs(v0) < 0.01f)
            {
                if (which == 0) { a = aTarget; av = 0f; } else { b = bTarget; bv = 0f; }
                return false;
            }
            float w = Mathf.Sqrt(k);
            float x;
            float v;
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + v0) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                x = e * (x0 * cs + sc * sn);
                v = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else
            {
                float cb = v0 + w * x0;
                float e = Mathf.Exp(-w * dt);
                x = (x0 + cb * dt) * e;
                v = (cb - w * (x0 + cb * dt)) * e;
            }
            if (which == 0) { a = aTarget + x; av = v; } else { b = bTarget + x; bv = v; }
            return true;
        }

        void Apply()
        {
            if (kind == 0)
            {
                if (mark == null || markFrames == null || checkFrameCount <= 0) return;
                int indeterminateFrames = markFrames.Length - checkFrameCount;
                if (b > 0.001f && indeterminateFrames > 0)
                {
                    int f = Mathf.Clamp(Mathf.RoundToInt(b * (indeterminateFrames - 1)), 0, indeterminateFrames - 1);
                    mark.sprite = markFrames[checkFrameCount + f];
                }
                else
                {
                    int f = Mathf.Clamp(Mathf.RoundToInt(a * (checkFrameCount - 1)), 0, checkFrameCount - 1);
                    mark.sprite = markFrames[f];
                }
                mark.enabled = a > 0.001f || state != 0;
            }
            else if (kind == 1)
            {
                // drawCircle(radius = dot / 2 - strokeWidth / 2)
                float d = Mathf.Max(0f, a - 2f);
                markRect.sizeDelta = new Vector2(d, d);
                mark.enabled = d > 0.01f;
            }
            else
            {
                markRect.sizeDelta = new Vector2(b, b);
                if (thumbRoot != null) thumbRoot.anchoredPosition = new Vector2(a + b / 2f, 0f);
                else markRect.anchoredPosition = new Vector2(a + b / 2f, 0f);
            }
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null) return;
            int slot = state + (Interactable() ? 0 : 3);
            for (int i = 0; i < colored.Length; i++)
            {
                if (colored[i] == null) continue;
                int idx = colorIndices[i * 6 + slot];
                if (idx >= 0) colored[i].color = theme.Get(idx);
            }
        }
    }
}
