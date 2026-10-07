#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Filled / outlined text field (TextField.kt, OutlinedTextField.kt, internal/TextFieldImpl.kt)
    /// around a TMP_InputField. The label moves between its expanded and minimized positions with
    /// the FastSpatial spring (labelProgress), its style lerps from BodyLarge to BodySmall, the
    /// indicator / outline width animates 1 → 2 dp (FastSpatial) and colors change with FastEffects.
    /// Color states (slot layout of M3SelectionColors): 0 unfocused, 1 focused, 2 error, 3 disabled.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TextField : UdonSharpBehaviour
#else
    public class M3TextField : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public TMP_InputField input;
        public bool isError;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif
        [Tooltip("The custom event sent to changeListener when the text changes.")]
        public string changeEvent = "_M3TextChanged";

        [Header("Parts")]
        public TextMeshProUGUI label;
        public RectTransform labelRect;
        public TextMeshProUGUI placeholder;
        [Tooltip("Filled: the bottom indicator line (its height is the stroke width).")]
        public RectTransform indicator;
        [Tooltip("Outlined: the outline, one sprite per outlineStep of stroke width from widthUnfocused.")]
        public Image outline;
        public Sprite[] outlineFrames;
        public float outlineStep = 0.25f;
        [Tooltip("Outlined: the label cutout, a rect of the background color over the outline.")]
        public RectTransform notch;

        [Header("Label geometry (x = left edge, y = vertical center from the field top, y down, dp)")]
        public Vector2 labelExpanded;
        public Vector2 labelMinimized;
        [Tooltip("Label scale when minimized: BodySmall / BodyLarge font size (TMP font size is not exposed to Udon).")]
        public float minimizedScale = 0.75f;
        [Tooltip("Outlined: label widths at the expanded / minimized styles (the cutout is width × progress + 2 × notchPadding).")]
        public float labelExpandedWidth;
        public float labelMinimizedWidth;
        public float notchPadding = 4f;
        [Tooltip("The label never expands (no label text, or the label is always minimized).")]
        public bool alwaysMinimized;

        [Header("Motion")]
        public float widthUnfocused = 1f;
        public float widthFocused = 2f;
        public float spatialDamping = 0.6f;
        public float spatialStiffness = 800f;
        public float effectsStiffness = 3800f;

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("6 palette indices per graphic: unfocused, focused, error, disabled, -, -.")]
        public int[] colorIndices;
        [Tooltip("Palette indices: caret, error caret, text selection.")]
        public int[] extraColors;

        bool focused;
        float progress;
        float progressV;
        float width;
        float widthV;
        float colorT = 1f;
        float colorV;
        float placeholderAlpha;
        float placeholderV;
        Color[] fromColors;
        int lastSlot = -1;
        float lastTime;
        bool ticking;
        bool initialized;

        void Start()
        {
            Init();
        }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            progress = LabelTarget();
            width = WidthTarget();
            placeholderAlpha = PlaceholderTarget();
            colorT = 1f;
            lastSlot = Slot();
            Apply();
        }

        public void _Focus()
        {
            Init();
            focused = true;
            Kick();
        }

        public void _Blur()
        {
            Init();
            focused = false;
            Kick();
        }

        public void _Changed()
        {
            Init();
            Kick();
            if (changeListener != null) changeListener.SendCustomEvent(changeEvent);
        }

        public void _SetError() { isError = true; Kick(); }
        public void _ClearError() { isError = false; Kick(); }

        public void _M3ThemeChanged() { lastSlot = Slot(); colorT = 1f; Apply(); }
        public void _M3Refresh() { initialized = false; Init(); }

        public string GetText() { return input != null ? input.text : ""; }

        bool Enabled() { return input == null || input.interactable; }

        bool Empty() { return input == null || input.text == null || input.text.Length == 0; }

        // InputPhase: Focused -> 1, UnfocusedEmpty -> (expanded label ? 0 : 1), UnfocusedNotEmpty -> 1
        float LabelTarget()
        {
            if (alwaysMinimized) return 1f;
            if (focused && Enabled()) return 1f;
            return Empty() ? 0f : 1f;
        }

        float WidthTarget() { return focused && Enabled() ? widthFocused : widthUnfocused; }

        // placeholderOpacity: shown while focused and empty (or always when the label stays minimized)
        float PlaceholderTarget()
        {
            if (!Empty()) return 0f;
            if (focused && Enabled()) return 1f;
            return alwaysMinimized ? 1f : 0f;
        }

        int Slot()
        {
            if (!Enabled()) return 3;
            if (isError) return 2;
            return focused ? 1 : 0;
        }

        void Kick()
        {
            int slot = Slot();
            if (slot != lastSlot)
            {
                // FastEffects color transition from the currently shown colors
                if (colored != null)
                {
                    if (fromColors == null || fromColors.Length != colored.Length) fromColors = new Color[colored.Length];
                    for (int i = 0; i < colored.Length; i++) if (colored[i] != null) fromColors[i] = colored[i].color;
                }
                lastSlot = slot;
                colorT = Enabled() ? 0f : 1f;
                colorV = 0f;
            }
            if (!Enabled())
            {
                // disabled: snap()
                progress = LabelTarget(); progressV = 0f;
                width = WidthTarget(); widthV = 0f;
            }
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            Apply();
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;

            float target = LabelTarget();
            float x = progress - target;
            if (Mathf.Abs(x) > 0.0005f || Mathf.Abs(progressV) > 0.005f)
            {
                StepSpring(x, progressV, dt, spatialDamping, spatialStiffness);
                progress = target + stepX; progressV = stepV; busy = true;
            }
            else { progress = target; progressV = 0f; }

            float wt = WidthTarget();
            x = width - wt;
            if (Mathf.Abs(x) > 0.001f || Mathf.Abs(widthV) > 0.01f)
            {
                StepSpring(x, widthV, dt, spatialDamping, spatialStiffness);
                width = wt + stepX; widthV = stepV; busy = true;
            }
            else { width = wt; widthV = 0f; }

            x = colorT - 1f;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(colorV) > 0.01f)
            {
                StepSpring(x, colorV, dt, 1f, effectsStiffness);
                colorT = 1f + stepX; colorV = stepV; busy = true;
            }
            else { colorT = 1f; colorV = 0f; }

            float pt = PlaceholderTarget();
            x = placeholderAlpha - pt;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(placeholderV) > 0.01f)
            {
                StepSpring(x, placeholderV, dt, 1f, effectsStiffness);
                placeholderAlpha = pt + stepX; placeholderV = stepV; busy = true;
            }
            else { placeholderAlpha = pt; placeholderV = 0f; }

            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        float stepX;
        float stepV;

        // Closed-form damped spring (SpringSimulation) from displacement x0 and velocity v0.
        void StepSpring(float x0, float v0, float dt, float z, float k)
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
            float p = progress;
            if (label != null && labelRect != null)
            {
                RectTransform rt = labelRect;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(labelExpanded.x, labelMinimized.x, p),
                    -Mathf.LerpUnclamped(labelExpanded.y, labelMinimized.y, p));
                float s = Mathf.LerpUnclamped(1f, minimizedScale, p);
                rt.localScale = new Vector3(s, s, 1f);
            }
            if (notch != null)
            {
                // outlineCutout: (labelWidth * labelProgress) + 2 * OutlinedTextFieldInnerPadding
                float lw = Mathf.LerpUnclamped(labelExpandedWidth, labelMinimizedWidth, p) * p;
                float nw = lw > 0f ? lw + notchPadding * 2f : 0f;
                notch.gameObject.SetActive(nw > 0f);
                notch.sizeDelta = new Vector2(nw, notch.sizeDelta.y);
            }
            if (indicator != null)
                indicator.sizeDelta = new Vector2(indicator.sizeDelta.x, width);
            if (outline != null && outlineFrames != null && outlineFrames.Length > 0)
            {
                int f = Mathf.Clamp(Mathf.RoundToInt((width - widthUnfocused) / outlineStep), 0, outlineFrames.Length - 1);
                outline.sprite = outlineFrames[f];
            }
            ApplyColors();
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null) return;
            int slot = lastSlot < 0 ? Slot() : lastSlot;
            bool blend = fromColors != null && fromColors.Length == colored.Length && colorT < 1f;
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                int idx = colorIndices[i * 6 + slot];
                if (idx < 0) continue;
                Color c = theme.Get(idx);
                if (blend) c = Color.LerpUnclamped(fromColors[i], c, Mathf.Clamp01(colorT));
                if (placeholder != null && g == placeholder) c.a *= Mathf.Clamp01(placeholderAlpha);
                g.color = c;
            }
#if !UDONSHARP
            // Udon does not expose TMP_InputField.caretColor / selectionColor: in VRChat the theme
            // baker writes them for the default scheme instead (no error caret color there).
            if (input != null && extraColors != null && extraColors.Length >= 3)
            {
                int caret = isError ? extraColors[1] : extraColors[0];
                if (caret >= 0) input.caretColor = theme.Get(caret);
                if (extraColors[2] >= 0) input.selectionColor = theme.Get(extraColors[2]);
            }
#endif
        }
    }
}
