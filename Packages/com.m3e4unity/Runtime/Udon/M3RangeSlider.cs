#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Range slider (Slider.kt RangeSlider, drawTrack with isRangeSlider): inactive track before the
    /// start thumb with a stop indicator, active track between the thumbs, inactive track after the end
    /// thumb with a stop indicator, gaps of thumbWidth / 2 + 6 dp around each thumb (a pressed thumb
    /// halves its width). Input: two uGUI Sliders split at the middle between the thumbs, each mapping
    /// its half of the track linearly, so a press always picks the closer thumb; the split is updated
    /// when the pointer is released.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3RangeSlider : UdonSharpBehaviour
#else
    public class M3RangeSlider : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public float minValue;
        public float maxValue = 1f;
        public float startValue = 0.25f;
        public float endValue = 0.75f;
        public Slider startSlider;
        public Slider endSlider;
        public RectTransform startInput;
        public RectTransform endInput;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts")]
        public RectTransform track;
        public RectTransform startInactiveTrack;
        public RectTransform activeTrack;
        public RectTransform endInactiveTrack;
        public RectTransform startStopIndicator;
        public RectTransform endStopIndicator;
        public RectTransform startThumb;
        public RectTransform endThumb;

        [Header("Geometry (dp)")]
        public float thumbWidth = 4f;
        public float trackHeight = 16f;
        public float cornerSize = 8f;
        public float insideCornerSize = 2f;
        public float gap = 6f;

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;

        bool startPressed;
        bool endPressed;
        bool splitting;

        void Start()
        {
            Split();
            Layout();
        }

        public void _M3Refresh() { Split(); Layout(); }
        public void _M3ThemeChanged() { ApplyColors(); }

        public void _StartChanged()
        {
            if (splitting || startSlider == null) return;
            startValue = startSlider.value;
            Layout();
            if (changeListener != null) changeListener.SendCustomEvent("_M3RangeChanged");
        }

        public void _EndChanged()
        {
            if (splitting || endSlider == null) return;
            endValue = endSlider.value;
            Layout();
            if (changeListener != null) changeListener.SendCustomEvent("_M3RangeChanged");
        }

        public void _StartDown() { startPressed = true; Layout(); }
        public void _EndDown() { endPressed = true; Layout(); }
        public void _Up() { startPressed = false; endPressed = false; Split(); Layout(); }

        float Fraction(float v)
        {
            float range = maxValue - minValue;
            return range > 0f ? Mathf.Clamp01((v - minValue) / range) : 0f;
        }

        float TotalWidth() { return ((RectTransform)transform).rect.width; }

        // Each input covers the track from its end of the slider up to the midpoint between the thumbs
        void Split()
        {
            if (startSlider == null || endSlider == null || startInput == null || endInput == null) return;
            splitting = true;
            float total = TotalWidth();
            float W = total - thumbWidth;
            float mid = (startValue + endValue) / 2f;
            float midX = thumbWidth / 2f + W * Fraction(mid);
            startInput.anchorMin = new Vector2(0f, 0f);
            startInput.anchorMax = new Vector2(0f, 1f);
            startInput.pivot = new Vector2(0f, 0.5f);
            startInput.anchoredPosition = Vector2.zero;
            startInput.sizeDelta = new Vector2(midX + thumbWidth / 2f, 0f);
            endInput.anchorMin = new Vector2(0f, 0f);
            endInput.anchorMax = new Vector2(0f, 1f);
            endInput.pivot = new Vector2(0f, 0.5f);
            endInput.anchoredPosition = new Vector2(midX - thumbWidth / 2f, 0f);
            endInput.sizeDelta = new Vector2(total - midX + thumbWidth / 2f, 0f);
            startSlider.minValue = minValue;
            startSlider.maxValue = mid;
            startSlider.value = startValue;
            endSlider.minValue = mid;
            endSlider.maxValue = maxValue;
            endSlider.value = endValue;
            splitting = false;
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
            float total = TotalWidth();
            float W = total - thumbWidth;
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.anchoredPosition = new Vector2(thumbWidth / 2f, 0f);
            track.sizeDelta = new Vector2(W, trackHeight);
            float startW = startPressed ? thumbWidth / 2f : thumbWidth;
            float endW = endPressed ? thumbWidth / 2f : thumbWidth;
            float vs = W * Fraction(startValue);
            float ve = W * Fraction(endValue);
            float startGap = startW / 2f + gap;
            float endGap = endW / 2f + gap;
            float c = cornerSize;
            // inactive track before the start thumb (drawn past startGap + corner) with its stop indicator
            bool startVisible = vs > startGap + c;
            Place(startInactiveTrack, 0f, startVisible ? vs - startGap : 0f);
            if (startStopIndicator != null)
            {
                startStopIndicator.gameObject.SetActive(startVisible);
                startStopIndicator.anchoredPosition = new Vector2(c, 0f);
            }
            // inactive track after the end thumb
            bool endVisible = ve < W - endGap - c;
            Place(endInactiveTrack, endVisible ? ve + endGap : 0f, endVisible ? W : 0f);
            if (endStopIndicator != null)
            {
                endStopIndicator.gameObject.SetActive(endVisible);
                endStopIndicator.anchoredPosition = new Vector2(W - c, 0f);
            }
            // active track between the thumbs (threshold: the inside corner radius)
            float a0 = vs + startGap;
            float a1 = ve - endGap;
            Place(activeTrack, a0, a1 - a0 > insideCornerSize ? a1 : a0);
            if (startThumb != null)
            {
                startThumb.anchoredPosition = new Vector2(thumbWidth / 2f + vs, 0f);
                startThumb.sizeDelta = new Vector2(startW, startThumb.sizeDelta.y);
            }
            if (endThumb != null)
            {
                endThumb.anchoredPosition = new Vector2(thumbWidth / 2f + ve, 0f);
                endThumb.sizeDelta = new Vector2(endW, endThumb.sizeDelta.y);
            }
            ApplyColors();
        }

        void ApplyColors()
        {
            if (theme == null || colored == null || colorIndices == null) return;
            bool enabled = startSlider == null || startSlider.interactable;
            int slot = enabled ? 0 : 3;
            for (int i = 0; i < colored.Length; i++)
            {
                if (colored[i] == null) continue;
                int idx = colorIndices[i * 6 + slot];
                if (idx >= 0) colored[i].color = theme.Get(idx);
            }
        }
    }
}
