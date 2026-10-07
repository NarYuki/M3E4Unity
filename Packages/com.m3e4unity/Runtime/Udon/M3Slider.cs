#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Material 3 Expressive slider (Slider.kt): bar thumb with gaps, inside corners, stop
    /// indicator and tick marks. Input comes from a uGUI Slider on the same object; this
    /// behaviour lays the parts out exactly like SliderImpl / drawTrack (LTR, horizontal).
    /// Modes: 0 = standard, 1 = centered (active track grows from the center).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Slider : UdonSharpBehaviour
#else
    public class M3Slider : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public Slider slider;
        public int mode;
        [Tooltip("Number of steps between the ends (Compose `steps`); 0 = continuous.")]
        public int steps;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts")]
        public RectTransform track;      // spans the track area (slider width minus the thumb width)
        public RectTransform activeTrack;
        public RectTransform inactiveTrack;
        public RectTransform startInactiveTrack; // centered mode: the inactive track before the center
        public RectTransform stopIndicator;
        public RectTransform startStopIndicator;
        public RectTransform thumb;
        public RectTransform[] ticks;
        public RectTransform valueIndicator;
        public TextMeshProUGUI valueText;
        public string valueFormat = "0";

        [Header("Geometry (dp)")]
        public float thumbWidth = 4f;
        public float trackHeight = 16f;
        public float cornerSize = 8f;
        public float gap = 6f;

        [Header("Colors")]
        public Graphic[] colored;
        [Tooltip("6 palette indices per graphic (selection layout: off, on, -, disabled off, disabled on, -). Ticks use off = on inactive track, on = on active track.")]
        public int[] colorIndices;

        bool pressed;

        void Start()
        {
            Layout();
        }

        public void _ValueChanged()
        {
            Layout();
            if (changeListener != null) changeListener.SendCustomEvent("_M3SliderChanged");
        }

        public void _Down()
        {
            if (slider != null && !slider.interactable) return;
            pressed = true;
            Layout();
        }

        public void _Up()
        {
            pressed = false;
            Layout();
        }

        public void _M3ThemeChanged() { Layout(); }
        public void _M3Refresh() { Layout(); }

        public float GetFraction()
        {
            if (slider == null) return 0f;
            float range = slider.maxValue - slider.minValue;
            return range > 0f ? (slider.value - slider.minValue) / range : 0f;
        }

        void Place(RectTransform rt, float x0, float x1)
        {
            if (rt == null) return;
            bool visible = x1 > x0;
            rt.gameObject.SetActive(visible);
            if (!visible) return;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x0, 0f);
            rt.sizeDelta = new Vector2(x1 - x0, trackHeight);
        }

        void Layout()
        {
            if (track == null) return;
            float f = Mathf.Clamp01(GetFraction());
            // SliderDefaults.Thumb: half width while interacting
            float tw = pressed ? thumbWidth / 2f : thumbWidth;
            float total = ((RectTransform)transform).rect.width;
            float W = total - tw;
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.anchoredPosition = new Vector2(tw / 2f, 0f);
            track.sizeDelta = new Vector2(W, trackHeight);

            bool onEnd = f <= 0f || f >= 1f;
            float c = cornerSize;
            float valueEnd = steps > 0 && !onEnd ? (W - c * 2f) * f + c : W * f;
            float endGap = tw / 2f + gap;

            if (mode == 1)
            {
                // centered: active between the center and the value, gaps on the thumb side only
                float center = W / 2f;
                float lo = Mathf.Min(valueEnd, center);
                float hi = Mathf.Max(valueEnd, center);
                float startGap = endGap;
                Place(startInactiveTrack, 0f, lo - (lo < center ? startGap : 0f));
                Place(inactiveTrack, hi + (hi > center ? endGap : 0f), W);
                Place(activeTrack, lo + (lo < center ? startGap : 0f), hi - (hi > center ? endGap : 0f));
                if (startStopIndicator != null) startStopIndicator.anchoredPosition = new Vector2(c, 0f);
            }
            else
            {
                // inactive track from value + gap to the end (drawn only before end - gap - corner)
                float threshold = W - endGap - c;
                if (valueEnd < threshold) Place(inactiveTrack, valueEnd + endGap, W);
                else Place(inactiveTrack, 0f, 0f);
                Place(activeTrack, 0f, valueEnd - endGap);
            }
            if (stopIndicator != null)
            {
                stopIndicator.anchoredPosition = new Vector2(W - c, 0f);
                stopIndicator.gameObject.SetActive(mode == 1 || valueEnd < W - endGap - c);
            }

            if (thumb != null)
            {
                thumb.anchorMin = new Vector2(0f, 0.5f);
                thumb.anchorMax = new Vector2(0f, 0.5f);
                thumb.pivot = new Vector2(0.5f, 0.5f);
                thumb.anchoredPosition = new Vector2(tw / 2f + valueEnd, 0f);
                thumb.sizeDelta = new Vector2(tw, thumb.sizeDelta.y);
            }

            PlaceTicks(valueEnd, endGap, W, c);
            ApplyColors();

            if (valueIndicator != null)
            {
                valueIndicator.gameObject.SetActive(pressed);
                valueIndicator.anchoredPosition = new Vector2(tw / 2f + valueEnd, valueIndicator.anchoredPosition.y);
                if (valueText != null && slider != null) valueText.text = slider.value.ToString(valueFormat);
            }
        }

        bool[] tickActive;

        // drawTrack: ticks at lerp(corner, W - corner, fraction); skip the last one (stop indicator),
        // the first one in centered mode, and any tick inside a thumb gap.
        void PlaceTicks(float valueEnd, float endGap, float W, float c)
        {
            if (ticks == null || ticks.Length == 0) return;
            if (tickActive == null || tickActive.Length != ticks.Length) tickActive = new bool[ticks.Length];
            float center = W / 2f;
            float activeStart = mode == 1 ? Mathf.Min(valueEnd, center) : 0f;
            float activeEnd = mode == 1 ? Mathf.Max(valueEnd, center) : valueEnd - endGap;
            for (int t = 0; t < ticks.Length; t++)
            {
                if (ticks[t] == null) continue;
                float tf = ticks.Length > 1 ? t / (float)(ticks.Length - 1) : 0f;
                float x = Mathf.Lerp(c, W - c, tf);
                bool skip = t == ticks.Length - 1 || (mode == 1 && t == 0);
                if (Mathf.Abs(x - valueEnd) <= endGap) skip = true;
                if (mode == 1 && Mathf.Abs(x - center) <= endGap) skip = true;
                ticks[t].gameObject.SetActive(!skip);
                ticks[t].anchoredPosition = new Vector2(x, 0f);
                tickActive[t] = x >= activeStart && x <= activeEnd;
            }
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null) return;
            bool enabled = slider == null || slider.interactable;
            int baseSlot = enabled ? 0 : 3;
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                int slot = baseSlot;
                // ticks: "on" color when they sit on the active track
                if (ticks != null && tickActive != null)
                {
                    for (int t = 0; t < ticks.Length; t++)
                    {
                        if (ticks[t] != null && ticks[t].gameObject == g.gameObject) slot = baseSlot + (tickActive[t] ? 1 : 0);
                    }
                }
                int idx = colorIndices[i * 6 + slot];
                if (idx >= 0) g.color = theme.Get(idx);
            }
        }
    }
}
