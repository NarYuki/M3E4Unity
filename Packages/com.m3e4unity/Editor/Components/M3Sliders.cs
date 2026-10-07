using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Sliders (Slider.kt, SliderDefaults).</summary>
    public static class M3Sliders
    {
        /// <param name="centered">Centered slider (SliderState with the active track from the center).</param>
        public static RectTransform Create(Transform parent, M3Context ctx, float width = 280f, float value = 0.5f,
            float min = 0f, float max = 1f, int steps = 0, bool centered = false, bool enabled = true, bool valueIndicator = false)
        {
            var bg = ctx.Background;
            float thumbW = SliderTokens.HandleWidth, thumbH = SliderTokens.HandleHeight;
            float trackH = SliderTokens.InactiveTrackHeight;
            float corner = trackH / 2f;                 // trackCornerSize Unspecified -> height / 2
            const float inside = 2f;                    // TrackInsideCornerSize
            float gap = SliderTokens.ActiveHandleLeadingSpace;
            float stop = SliderTokens.StopIndicatorSize;
            float height = Mathf.Max(48f, thumbH);      // minimumInteractiveComponentSize

            var root = M3Build.Rect("Slider", parent);
            M3Build.Size(root, width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minHeight = le.preferredHeight = height;

            // Input: uGUI Slider over the track area (handle of zero width).
            var hit = M3Build.Rect("Hit", root);
            M3Build.Fill(hit);
            var hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(0, 0, 0, 0);
            var handleArea = M3Build.Rect("HandleArea", root);
            M3Build.Fill(handleArea, thumbW / 2f, 0, thumbW / 2f, 0);
            var handle = M3Build.Rect("Handle", handleArea);
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(0, 0);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.targetGraphic = hitImg;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = steps > 0 ? 0 : min;
            slider.maxValue = steps > 0 ? steps + 1 : max;
            slider.wholeNumbers = steps > 0;
            slider.value = steps > 0 ? Mathf.Round((value - min) / (max - min) * (steps + 1)) : value;
            slider.interactable = enabled;

            var track = M3Build.Rect("Track", root);
            var inactive = M3Build.Shape("InactiveTrack", track, ShapeCell.Create(ShapeKind.Fill).WithRadii(inside, corner, corner, inside), ColorRef.Clear, false);
            var startInactive = centered ? M3Build.Shape("StartInactiveTrack", track, ShapeCell.Create(ShapeKind.Fill).WithRadii(corner, inside, inside, corner), ColorRef.Clear, false) : null;
            var active = M3Build.Shape("ActiveTrack", track,
                centered ? ShapeCell.Create(ShapeKind.Fill).WithRadius(inside) : ShapeCell.Create(ShapeKind.Fill).WithRadii(corner, inside, inside, corner),
                ColorRef.Clear, false);
            Image Dot(string name)
            {
                var img = M3Build.Shape(name, track, ShapeCell.Create(ShapeKind.Ellipse), ColorRef.Clear, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                rt.sizeDelta = new Vector2(stop, stop);
                return img;
            }
            var stopImg = Dot("StopIndicator");
            var startStopImg = centered ? Dot("StartStopIndicator") : null;
            var ticks = new List<Image>();
            if (steps > 0)
                for (int i = 0; i < steps + 2; i++) ticks.Add(Dot($"Tick{i}"));
            var thumb = M3Build.Shape("Thumb", root, ShapeCell.Create(ShapeKind.Fill).WithRadius(ShapeCell.Full), ColorRef.Clear, false);
            ((RectTransform)thumb.transform).sizeDelta = new Vector2(thumbW, thumbH);

            RectTransform indicator = null;
            TextMeshProUGUI indicatorText = null;
            if (valueIndicator)
            {
                // SliderDefaults Label sample: a PlainTooltip above the thumb
                indicator = M3Build.Rect("ValueIndicator", root);
                indicator.anchorMin = indicator.anchorMax = new Vector2(0, 0.5f);
                indicator.pivot = new Vector2(0.5f, 0f);
                indicator.anchoredPosition = new Vector2(0, thumbH / 2f + SliderTokens.ValueIndicatorActiveBottomSpace);
                var row = M3Build.Row(indicator, 0);
                row.padding = new RectOffset(8, 8, 4, 4);
                var fit = indicator.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var c = M3Build.Shape("Container", indicator, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(PlainTooltipTokens.ContainerShape)),
                    ColorRef.Role(PlainTooltipTokens.ContainerColor));
                M3Pressable.Ignore(c);
                indicatorText = M3Build.Text("Value", indicator, "0", PlainTooltipTokens.SupportingTextFont, ColorRef.Role(PlainTooltipTokens.SupportingTextColor), ctx);
                indicator.gameObject.SetActive(false);
            }

            var behaviour = M3UdonBridge.Add<M3Slider>(root.gameObject);
            behaviour.slider = slider;
            behaviour.mode = centered ? 1 : 0;
            behaviour.steps = steps;
            behaviour.track = track;
            behaviour.activeTrack = (RectTransform)active.transform;
            behaviour.inactiveTrack = (RectTransform)inactive.transform;
            behaviour.startInactiveTrack = startInactive != null ? (RectTransform)startInactive.transform : null;
            behaviour.stopIndicator = (RectTransform)stopImg.transform;
            behaviour.startStopIndicator = startStopImg != null ? (RectTransform)startStopImg.transform : null;
            behaviour.thumb = (RectTransform)thumb.transform;
            behaviour.ticks = ticks.ConvertAll(t => (RectTransform)t.transform).ToArray();
            behaviour.valueIndicator = indicator;
            behaviour.valueText = indicatorText;
            behaviour.thumbWidth = thumbW;
            behaviour.trackHeight = trackH;
            behaviour.cornerSize = corner;
            behaviour.gap = gap;

            M3UdonBridge.Wire(slider.onValueChanged, behaviour, nameof(M3Slider._ValueChanged));
            M3UdonBridge.WirePointer(root.gameObject, EventTriggerType.PointerDown, behaviour, nameof(M3Slider._Down));
            M3UdonBridge.WirePointer(root.gameObject, EventTriggerType.PointerUp, behaviour, nameof(M3Slider._Up));

            // SliderDefaults.colors()
            var activeC = ColorRef.Role(SliderTokens.ActiveTrackColor);
            var inactiveC = ColorRef.Role(SliderTokens.InactiveTrackColor);
            var thumbC = ColorRef.Role(SliderTokens.HandleColor);
            var dActive = ColorRef.Over(bg, SliderTokens.DisabledActiveTrackColor, SliderTokens.DisabledActiveTrackOpacity);
            var dInactive = ColorRef.Over(bg, SliderTokens.DisabledInactiveTrackColor, SliderTokens.DisabledInactiveTrackOpacity);
            var dThumb = ColorRef.Over(bg, SliderTokens.DisabledHandleColor, SliderTokens.DisabledHandleOpacity);
            var entries = new List<SelectionColorEntry>
            {
                Entry(active, activeC, dActive),
                Entry(inactive, inactiveC, dInactive),
                Entry(thumb, thumbC, dThumb),
                // drawStopIndicator: trackColor(enabled, active = true)
                Entry(stopImg, activeC, dActive),
            };
            if (startInactive != null) entries.Add(Entry(startInactive, inactiveC, dInactive));
            if (startStopImg != null) entries.Add(Entry(startStopImg, activeC, dActive));
            // ticks: on the inactive track = activeTickColor? (tickColor(active = false) = ActiveTrackColor)
            foreach (var t in ticks)
                entries.Add(new SelectionColorEntry
                {
                    graphic = t,
                    off = ColorRef.Over(SliderTokens.InactiveTrackColor, SliderTokens.ActiveTrackColor, 1f),
                    on = ColorRef.Over(SliderTokens.ActiveTrackColor, SliderTokens.InactiveTrackColor, 1f),
                    disabledOff = dActive,
                    disabledOn = dInactive,
                });
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            foreach (var e in entries)
            {
                var b = e.graphic.GetComponent<M3ColorBinding>();
                if (b != null) Object.DestroyImmediate(b);
            }
            M3UdonBridge.Sync(behaviour);
            // first layout in the editor
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            behaviour._M3Refresh();
            return root;
        }

        /// <summary>RangeSlider (continuous): two thumbs on the expressive track.</summary>
        public static RectTransform Range(Transform parent, M3Context ctx, float width = 280f, float start = 0.25f, float end = 0.75f,
            float min = 0f, float max = 1f, bool enabled = true)
        {
            var bg = ctx.Background;
            float thumbW = SliderTokens.HandleWidth, thumbH = SliderTokens.HandleHeight;
            float trackH = SliderTokens.InactiveTrackHeight;
            float corner = trackH / 2f;
            const float inside = 2f;
            float stop = SliderTokens.StopIndicatorSize;
            float height = Mathf.Max(48f, thumbH);

            var root = M3Build.Rect("RangeSlider", parent);
            M3Build.Size(root, width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minHeight = le.preferredHeight = height;

            var behaviour = M3UdonBridge.Add<M3RangeSlider>(root.gameObject);
            Slider Input(string name, out RectTransform rt, string changed, string down)
            {
                rt = M3Build.Rect(name, root);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                var area = M3Build.Rect("HandleArea", rt);
                M3Build.Fill(area, thumbW / 2f, 0, thumbW / 2f, 0);
                var handle = M3Build.Rect("Handle", area);
                handle.anchorMin = new Vector2(0, 0);
                handle.anchorMax = new Vector2(0, 1);
                handle.sizeDelta = Vector2.zero;
                var s = rt.gameObject.AddComponent<Slider>();
                s.transition = Selectable.Transition.None;
                s.navigation = new Navigation { mode = Navigation.Mode.None };
                s.targetGraphic = img;
                s.handleRect = handle;
                s.direction = Slider.Direction.LeftToRight;
                s.interactable = enabled;
                M3UdonBridge.Wire(s.onValueChanged, behaviour, changed);
                M3UdonBridge.WirePointer(rt.gameObject, EventTriggerType.PointerDown, behaviour, down);
                M3UdonBridge.WirePointer(rt.gameObject, EventTriggerType.PointerUp, behaviour, nameof(M3RangeSlider._Up));
                return s;
            }
            var startSlider = Input("StartInput", out var startInput, nameof(M3RangeSlider._StartChanged), nameof(M3RangeSlider._StartDown));
            var endSlider = Input("EndInput", out var endInput, nameof(M3RangeSlider._EndChanged), nameof(M3RangeSlider._EndDown));

            var track = M3Build.Rect("Track", root);
            var startInactive = M3Build.Shape("StartInactiveTrack", track, ShapeCell.Create(ShapeKind.Fill).WithRadii(corner, inside, inside, corner), ColorRef.Clear, false);
            var active = M3Build.Shape("ActiveTrack", track, ShapeCell.Create(ShapeKind.Fill).WithRadius(inside), ColorRef.Clear, false);
            var endInactive = M3Build.Shape("EndInactiveTrack", track, ShapeCell.Create(ShapeKind.Fill).WithRadii(inside, corner, corner, inside), ColorRef.Clear, false);
            Image Dot(string name)
            {
                var img = M3Build.Shape(name, track, ShapeCell.Create(ShapeKind.Ellipse), ColorRef.Clear, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                rt.sizeDelta = new Vector2(stop, stop);
                return img;
            }
            var startStop = Dot("StartStopIndicator");
            var endStop = Dot("EndStopIndicator");
            Image Thumb(string name)
            {
                var img = M3Build.Shape(name, root, ShapeCell.Create(ShapeKind.Fill).WithRadius(ShapeCell.Full), ColorRef.Clear, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(thumbW, thumbH);
                img.raycastTarget = false;
                return img;
            }
            var startThumb = Thumb("StartThumb");
            var endThumb = Thumb("EndThumb");
            foreach (var g in new Graphic[] { startInactive, active, endInactive, startStop, endStop }) g.raycastTarget = false;

            behaviour.minValue = min; behaviour.maxValue = max;
            behaviour.startValue = start; behaviour.endValue = end;
            behaviour.startSlider = startSlider; behaviour.endSlider = endSlider;
            behaviour.startInput = startInput; behaviour.endInput = endInput;
            behaviour.track = track;
            behaviour.startInactiveTrack = (RectTransform)startInactive.transform;
            behaviour.activeTrack = (RectTransform)active.transform;
            behaviour.endInactiveTrack = (RectTransform)endInactive.transform;
            behaviour.startStopIndicator = (RectTransform)startStop.transform;
            behaviour.endStopIndicator = (RectTransform)endStop.transform;
            behaviour.startThumb = (RectTransform)startThumb.transform;
            behaviour.endThumb = (RectTransform)endThumb.transform;
            behaviour.thumbWidth = thumbW;
            behaviour.trackHeight = trackH;
            behaviour.cornerSize = corner;
            behaviour.insideCornerSize = inside;
            behaviour.gap = SliderTokens.ActiveHandleLeadingSpace;

            var activeC = ColorRef.Role(SliderTokens.ActiveTrackColor);
            var inactiveC = ColorRef.Role(SliderTokens.InactiveTrackColor);
            var thumbC = ColorRef.Role(SliderTokens.HandleColor);
            var dActive = ColorRef.Over(bg, SliderTokens.DisabledActiveTrackColor, SliderTokens.DisabledActiveTrackOpacity);
            var dInactive = ColorRef.Over(bg, SliderTokens.DisabledInactiveTrackColor, SliderTokens.DisabledInactiveTrackOpacity);
            var dThumb = ColorRef.Over(bg, SliderTokens.DisabledHandleColor, SliderTokens.DisabledHandleOpacity);
            var entries = new List<SelectionColorEntry>
            {
                Entry(active, activeC, dActive),
                Entry(startInactive, inactiveC, dInactive),
                Entry(endInactive, inactiveC, dInactive),
                Entry(startThumb, thumbC, dThumb),
                Entry(endThumb, thumbC, dThumb),
                Entry(startStop, activeC, dActive),
                Entry(endStop, activeC, dActive),
            };
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            foreach (var e in entries)
            {
                var b = e.graphic.GetComponent<M3ColorBinding>();
                if (b != null) Object.DestroyImmediate(b);
            }
            M3UdonBridge.Sync(behaviour);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            behaviour._M3Refresh();
            return root;
        }

        static SelectionColorEntry Entry(Graphic g, ColorRef enabled, ColorRef disabled) => new SelectionColorEntry
        {
            graphic = g, off = enabled, on = enabled, indeterminate = enabled,
            disabledOff = disabled, disabledOn = disabled, disabledIndeterminate = disabled,
        };
    }
}
