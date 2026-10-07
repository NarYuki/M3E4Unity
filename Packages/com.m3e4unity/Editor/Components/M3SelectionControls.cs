using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Checkbox, radio button and switch (Checkbox.kt, RadioButton.kt, Switch.kt).</summary>
    public static class M3SelectionControls
    {
        /// <summary>minimumInteractiveComponentSize.</summary>
        const float Touch = 48f;

        static RectTransform Root(Transform parent, string name, float w, float h)
        {
            var root = M3Build.Rect(name, parent);
            M3Build.Size(root, w, h);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            return root;
        }

        static RectTransform Centered(string name, Transform parent, float w, float h)
        {
            var rt = M3Build.Rect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>
        /// The unbounded state layer + ripple circle of selection controls
        /// (ripple(bounded = false, radius = StateLayerSize / 2)).
        /// </summary>
        static M3Interactive HoverRipple(RectTransform root, Transform center, M3Context ctx, float size, ColorRole content, bool enabled)
        {
            var layer = M3Build.Rect("StateLayerHost", center);
            layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
            layer.sizeDelta = new Vector2(size, size);
            layer.SetAsFirstSibling();
            var state = M3Build.Shape("StateLayer", layer, ShapeCell.Create(ShapeKind.Fill).WithRadius(ShapeCell.Full), ColorRef.Clear);
            var ripple = M3Build.Shape("Ripple", layer, ShapeCell.Create(ShapeKind.Ripple).WithRadius(ShapeCell.Full), ColorRef.Clear);
            var spec = new InteractionSpec
            {
                Root = root,
                Context = ctx,
                Enabled = enabled,
                StateLayer = state,
                Ripple = ripple,
                RippleClipRadius = ShapeCell.Full,
                RestShape = 0, PressedShape = 0, SelectedShape = 0, SelectedPressedShape = 0, MaxShape = 0,
                // the circle sits on the background: hover 8 %, ripple 10 % of the content color
                RippleColor = ColorRef.Over(ctx.Background, content, StateLayer.Hover, content, StateLayer.Pressed),
            };
            spec.RippleColorSelected = spec.RippleColor;
            spec.Color(state, ColorRef.Over(ctx.Background, content, StateLayer.Hover), null, ColorRef.Clear, ColorRef.Clear);
            return M3Interaction.Apply(spec);
        }

        static void WireSelection(RectTransform root, M3Selection sel)
        {
            var button = root.GetComponent<Button>();
            sel.selectable = button;
            M3UdonBridge.Wire(button.onClick, sel, nameof(M3Selection._Click));
            M3UdonBridge.WirePointer(root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerDown, sel, nameof(M3Selection._Down));
            M3UdonBridge.WirePointer(root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerUp, sel, nameof(M3Selection._Up));
            M3UdonBridge.WirePointer(root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerExit, sel, nameof(M3Selection._Up));
        }

        static SelectionColorEntry Entry(Graphic g, ColorRef off, ColorRef on, ColorRef ind, ColorRef dOff, ColorRef dOn, ColorRef dInd) =>
            new SelectionColorEntry { graphic = g, off = off, on = on, indeterminate = ind, disabledOff = dOff, disabledOn = dOn, disabledIndeterminate = dInd };

        static void Colors(GameObject go, params SelectionColorEntry[] entries)
        {
            var sc = go.AddComponent<M3SelectionColors>();
            sc.entries = new List<SelectionColorEntry>(entries);
            foreach (var e in entries)
            {
                var b = e.graphic.GetComponent<M3ColorBinding>();
                if (b != null) Object.DestroyImmediate(b);
            }
        }

        /// <param name="state">0 off, 1 on, 2 indeterminate (TriStateCheckbox).</param>
        public static RectTransform Checkbox(Transform parent, M3Context ctx, int state = 0, bool enabled = true, bool error = false)
        {
            var bg = ctx.Background;
            // CheckboxImpl (isCheckboxStylingFixEnabled = false): 20 dp canvas, 2 dp radius, 2 dp stroke
            const float size = 20f, radius = 2f, stroke = 2f;
            var root = Root(parent, "Checkbox", Touch, Touch);
            var box = Centered("Box", root, size, size);
            var stateHost = HoverRipple(root, box, ctx, CheckboxTokens.StateLayerSize, ColorRole.OnSurface, enabled);
            var fill = M3Build.Shape("Fill", box, ShapeCell.Create(ShapeKind.Fill).WithRadius(radius), ColorRef.Clear);
            var border = M3Build.Shape("Border", box, ShapeCell.Create(ShapeKind.Stroke).WithRadius(radius).WithStroke(stroke), ColorRef.Clear);
            // check mark frames: fraction 0..1, then gravitation 0..1 at fraction 1
            var frames = new List<Sprite>();
            const int checkFrames = 24, dashFrames = 16;
            for (int i = 0; i < checkFrames; i++)
                frames.Add(M3ShapeAtlas.Get(ShapeCell.Create(ShapeKind.CheckMark).WithExtra(i / (float)(checkFrames - 1), 0, stroke)));
            for (int i = 0; i < dashFrames; i++)
                frames.Add(M3ShapeAtlas.Get(ShapeCell.Create(ShapeKind.CheckMark).WithExtra(1, i / (float)(dashFrames - 1), stroke)));
            var mark = M3Build.Shape("Check", box, ShapeCell.Create(ShapeKind.CheckMark).WithExtra(1, 0, stroke), ColorRef.Clear);
            mark.sprite = state == 0 ? frames[0] : state == 1 ? frames[checkFrames - 1] : frames[frames.Count - 1];

            var sel = M3UdonBridge.Add<M3Selection>(root.gameObject);
            sel.kind = 0;
            sel.state = state;
            sel.mark = mark;
            sel.markRect = (RectTransform)mark.transform;
            sel.markFrames = frames.ToArray();
            sel.checkFrameCount = checkFrames;
            // Checkbox: DefaultSpatial for the check fraction
            var spring = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            sel.checkDamping = spring.DampingRatio;
            sel.checkStiffness = spring.Stiffness;
            WireSelection(root, sel);
            stateHost.selectable.interactable = enabled;

            var boxOn = error ? CheckboxTokens.SelectedErrorContainerColor : CheckboxTokens.SelectedContainerColor;
            var borderOff = error ? CheckboxTokens.UnselectedErrorOutlineColor : CheckboxTokens.UnselectedOutlineColor;
            var markOn = error ? CheckboxTokens.SelectedErrorIconColor : CheckboxTokens.SelectedIconColor;
            var disabledBox = ColorRef.Over(bg, CheckboxTokens.SelectedDisabledContainerColor, CheckboxTokens.SelectedDisabledContainerOpacity);
            var disabledBorderOff = ColorRef.Over(bg, CheckboxTokens.UnselectedDisabledOutlineColor, CheckboxTokens.UnselectedDisabledContainerOpacity);
            Colors(root.gameObject,
                Entry(fill, ColorRef.Clear, ColorRef.Role(boxOn), ColorRef.Role(boxOn), ColorRef.Clear, disabledBox, disabledBox),
                Entry(border, ColorRef.Role(borderOff), ColorRef.Role(boxOn), ColorRef.Role(boxOn), disabledBorderOff, disabledBox, disabledBox),
                // checkmarkColor(state) ignores enabled while the styling-fix flag is off
                Entry(mark, ColorRef.Clear, ColorRef.Role(markOn), ColorRef.Role(markOn), ColorRef.Clear, ColorRef.Role(markOn), ColorRef.Role(markOn)));
            M3UdonBridge.Sync(sel);
            return root;
        }

        public static RectTransform Radio(Transform parent, M3Context ctx, bool selected = false, bool enabled = true)
        {
            var bg = ctx.Background;
            float size = RadioButtonTokens.IconSize;
            const float stroke = 2f, dot = 12f;
            var root = Root(parent, "RadioButton", Touch, Touch);
            var icon = Centered("Icon", root, size, size);
            var stateHost = HoverRipple(root, icon, ctx, RadioButtonTokens.StateLayerSize, ColorRole.OnSurface, enabled);
            // ring: drawCircle(radius = IconSize / 2 - stroke / 2, Stroke(2 dp))
            var ring = M3Build.Shape("Ring", icon, ShapeCell.Create(ShapeKind.Stroke).WithRadius(ShapeCell.Full).WithStroke(stroke), ColorRef.Clear);
            var dotRt = Centered("Dot", icon, selected ? dot - stroke : 0, selected ? dot - stroke : 0);
            var dotImg = dotRt.gameObject.AddComponent<Image>();
            M3Build.SetShape(dotImg, ShapeCell.Create(ShapeKind.Ellipse));
            dotImg.raycastTarget = false;

            var sel = M3UdonBridge.Add<M3Selection>(root.gameObject);
            sel.kind = 1;
            sel.state = selected ? 1 : 0;
            sel.mark = dotImg;
            sel.markRect = dotRt;
            sel.radioDot = dot;
            var spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            sel.springDamping = spring.DampingRatio;
            sel.springStiffness = spring.Stiffness;
            WireSelection(root, sel);
            stateHost.selectable.interactable = enabled;

            var on = ColorRef.Role(RadioButtonTokens.SelectedIconColor);
            var off = ColorRef.Role(RadioButtonTokens.UnselectedIconColor);
            var dOn = ColorRef.Over(bg, RadioButtonTokens.DisabledSelectedIconColor, RadioButtonTokens.DisabledSelectedIconOpacity);
            var dOff = ColorRef.Over(bg, RadioButtonTokens.DisabledUnselectedIconColor, RadioButtonTokens.DisabledUnselectedIconOpacity);
            Colors(root.gameObject,
                Entry(ring, off, on, on, dOff, dOn, dOn),
                Entry(dotImg, off, on, on, dOff, dOn, dOn));
            M3UdonBridge.Sync(sel);
            return root;
        }

        public static RectTransform Switch(Transform parent, M3Context ctx, bool on = false, bool enabled = true, string thumbIcon = null)
        {
            var bg = ctx.Background;
            float w = SwitchTokens.TrackWidth, h = SwitchTokens.TrackHeight, outline = SwitchTokens.TrackOutlineWidth;
            var root = Root(parent, "Switch", w, Touch);
            var trackRt = Centered("Track", root, w, h);
            var track = M3Build.Shape("TrackFill", trackRt, ShapeCell.Create(ShapeKind.Fill).WithRadius(ShapeCell.Full), ColorRef.Clear);
            var border = M3Build.Shape("TrackBorder", trackRt, ShapeCell.Create(ShapeKind.Stroke).WithRadius(ShapeCell.Full).WithStroke(outline), ColorRef.Clear);
            // thumb host anchored at the track's start, moved by M3Selection
            var thumbRoot = M3Build.Rect("ThumbHost", trackRt);
            thumbRoot.anchorMin = thumbRoot.anchorMax = new Vector2(0f, 0.5f);
            thumbRoot.sizeDelta = Vector2.zero;
            var stateHost = HoverRipple(root, thumbRoot, ctx, SwitchTokens.StateLayerSize, ColorRole.OnSurface, enabled);
            var thumbRt = Centered("Thumb", thumbRoot, 16, 16);
            var thumb = thumbRt.gameObject.AddComponent<Image>();
            M3Build.SetShape(thumb, ShapeCell.Create(ShapeKind.Ellipse));
            thumb.raycastTarget = false;
            TMPro.TextMeshProUGUI icon = null;
            if (thumbIcon != null)
            {
                // SwitchDefaults.IconSize = 16 dp
                icon = M3Build.Icon("Icon", thumbRoot, thumbIcon, 16f, ColorRef.Clear, ctx);
                var irt = (RectTransform)icon.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
            }

            var sel = M3UdonBridge.Add<M3Selection>(root.gameObject);
            sel.kind = 2;
            sel.state = on ? 1 : 0;
            sel.mark = thumb;
            sel.markRect = thumbRt;
            sel.thumbRoot = thumbRoot;
            sel.thumbIcon = icon;
            sel.switchWidth = w;
            sel.switchHeight = h;
            sel.thumbUnchecked = SwitchTokens.UnselectedHandleWidth;
            sel.thumbChecked = SwitchTokens.SelectedHandleWidth;
            sel.thumbPressed = SwitchTokens.PressedHandleWidth;
            sel.trackOutline = outline;
            var spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            sel.springDamping = spring.DampingRatio;
            sel.springStiffness = spring.Stiffness;
            // initial geometry
            float size = icon != null || on ? SwitchTokens.SelectedHandleWidth : SwitchTokens.UnselectedHandleWidth;
            float pad = (h - SwitchTokens.SelectedHandleWidth) / 2f;
            float offset = on ? w - SwitchTokens.SelectedHandleWidth - pad : (h - size) / 2f;
            thumbRt.sizeDelta = new Vector2(size, size);
            thumbRoot.anchoredPosition = new Vector2(offset + size / 2f, 0f);
            WireSelection(root, sel);
            stateHost.selectable.interactable = enabled;

            var entries = new List<SelectionColorEntry>
            {
                Entry(track, ColorRef.Role(SwitchTokens.UnselectedTrackColor), ColorRef.Role(SwitchTokens.SelectedTrackColor), ColorRef.Role(SwitchTokens.SelectedTrackColor),
                    ColorRef.Over(bg, SwitchTokens.DisabledUnselectedTrackColor, SwitchTokens.DisabledTrackOpacity),
                    ColorRef.Over(bg, SwitchTokens.DisabledSelectedTrackColor, SwitchTokens.DisabledTrackOpacity),
                    ColorRef.Over(bg, SwitchTokens.DisabledSelectedTrackColor, SwitchTokens.DisabledTrackOpacity)),
                // checkedBorderColor = Transparent; unchecked = UnselectedFocusTrackOutlineColor
                Entry(border, ColorRef.Role(SwitchTokens.UnselectedFocusTrackOutlineColor), ColorRef.Clear, ColorRef.Clear,
                    ColorRef.Over(bg, SwitchTokens.DisabledUnselectedTrackOutlineColor, SwitchTokens.DisabledTrackOpacity), ColorRef.Clear, ColorRef.Clear),
                Entry(thumb, ColorRef.Role(SwitchTokens.UnselectedHandleColor), ColorRef.Role(SwitchTokens.SelectedHandleColor), ColorRef.Role(SwitchTokens.SelectedHandleColor),
                    ColorRef.Over(bg, SwitchTokens.DisabledUnselectedTrackColor, SwitchTokens.DisabledTrackOpacity, SwitchTokens.DisabledUnselectedHandleColor, SwitchTokens.DisabledUnselectedHandleOpacity),
                    ColorRef.Role(SwitchTokens.DisabledSelectedHandleColor), ColorRef.Role(SwitchTokens.DisabledSelectedHandleColor)),
            };
            if (icon != null)
                entries.Add(Entry(icon, ColorRef.Role(SwitchTokens.UnselectedIconColor), ColorRef.Role(SwitchTokens.SelectedIconColor), ColorRef.Role(SwitchTokens.SelectedIconColor),
                    ColorRef.Over(bg, SwitchTokens.DisabledUnselectedIconColor, SwitchTokens.DisabledUnselectedIconOpacity),
                    ColorRef.Over(SwitchTokens.DisabledSelectedHandleColor, SwitchTokens.DisabledSelectedIconColor, SwitchTokens.DisabledSelectedIconOpacity),
                    ColorRef.Over(SwitchTokens.DisabledSelectedHandleColor, SwitchTokens.DisabledSelectedIconColor, SwitchTokens.DisabledSelectedIconOpacity)));
            Colors(root.gameObject, entries.ToArray());
            M3UdonBridge.Sync(sel);
            return root;
        }
    }
}
