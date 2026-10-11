using System;
using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Everything needed to make a container interactive the Material way.</summary>
    public sealed class InteractionSpec
    {
        public RectTransform Root;
        public M3Context Context;
        public bool Toggle;
        public bool Selected;
        public bool Enabled = true;

        /// <summary>Shape layers that morph together, each with its cell for a corner radius (dp).</summary>
        public readonly List<(Image image, Func<float, ShapeCell> cell)> ShapeLayers = new List<(Image, Func<float, ShapeCell>)>();
        public float RestShape, PressedShape, SelectedShape, SelectedPressedShape;
        /// <summary>Hovered shape value (list items); null = no hover morph.</summary>
        public float? HoveredShape;
        /// <summary>Shape after the pointer hovered and left (menu groups); null = none.</summary>
        public float? InactiveShape;
        /// <summary>Smallest shape value that frames must cover (defaults to the smallest target).</summary>
        public float? MinShape;
        /// <summary>Largest radius that still changes the outline (half the height for pills).</summary>
        public float MaxShape;
        public Spring Spring = new Spring(1f, 1600f);

        /// <summary>State layer (hover/focus), driven by the Selectable's ColorTint.</summary>
        public Image StateLayer;
        public Image Ripple;
        /// <summary>Corner radius used to clip the ripple (the pressed shape).</summary>
        public float RippleClipRadius;
        public CornerShape? RippleClipShape;

        public readonly List<StateColorEntry> Colors = new List<StateColorEntry>();
        /// <summary>Stiffness of the color fade spring (an effects spec); 0 = colors switch instantly.</summary>
        public float ColorStiffness;
        /// <summary>Fade every state color (list items); otherwise only the graphics in AnimatedColors.</summary>
        public bool AnimateAllColors;
        public readonly HashSet<Graphic> AnimatedColors = new HashSet<Graphic>();
        public readonly List<GameObject> ShowWhenSelected = new List<GameObject>();
        public RectTransform OpticalContent;
        public float OpticalA, OpticalB, OpticalMin, OpticalMax;
        public readonly List<GameObject> HideWhenSelected = new List<GameObject>();
        /// <summary>Content animated in and out with the selection (AnimatedVisibility).</summary>
        public readonly List<M3Reveal> Reveals = new List<M3Reveal>();

        /// <summary>
        /// Wraps content (in a layout row) in a slot whose width animates between 0 and size with the
        /// selection: expandHorizontally / shrinkHorizontally + fadeIn / fadeOut. fromStart: expandFrom
        /// Alignment.Start (the content stays at the start edge), otherwise End (the default).
        /// </summary>
        public M3Reveal ExpandReveal(Graphic content, float size, bool fromStart, Spring expand, Spring shrink, float fadeInStiffness, float fadeOutStiffness)
        {
            var parent = content.transform.parent;
            var slot = M3Build.Rect(content.name + "Slot", parent);
            slot.SetSiblingIndex(content.transform.GetSiblingIndex());
            var le = slot.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = size;
            le.minWidth = le.preferredWidth = Selected ? size : 0f;
            slot.gameObject.AddComponent<RectMask2D>();
            var group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = Selected ? 1f : 0f;
            group.blocksRaycasts = false;
            var crt = content.rectTransform;
            crt.SetParent(slot, false);
            float edge = fromStart ? 0f : 1f;
            crt.anchorMin = crt.anchorMax = new Vector2(edge, 0.5f);
            crt.pivot = new Vector2(edge, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(size, size);
            var rev = M3UdonBridge.Add<M3Reveal>(slot.gameObject);
            rev.mode = 0;
            rev.visible = Selected;
            rev.slot = le;
            rev.fullSize = size;
            rev.fade = group;
            rev.enterDamping = expand.DampingRatio; rev.enterStiffness = expand.Stiffness;
            rev.exitDamping = shrink.DampingRatio; rev.exitStiffness = shrink.Stiffness;
            rev.fadeInStiffness = fadeInStiffness; rev.fadeOutStiffness = fadeOutStiffness;
            M3UdonBridge.Sync(rev);
            Reveals.Add(rev);
            return rev;
        }

        /// <summary>
        /// Content that appears with scaleIn(initialScale, transformOrigin = its pivot) + fadeIn and
        /// leaves with ExitTransition.None (SegmentedButtonDefaults.Icon).
        /// </summary>
        public M3Reveal ScaleReveal(Graphic content, float initialScale, Spring scaleIn, float fadeInStiffness)
        {
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.alpha = Selected ? 1f : 0f;
            group.blocksRaycasts = false;
            var rev = M3UdonBridge.Add<M3Reveal>(content.gameObject);
            rev.mode = 1;
            rev.visible = Selected;
            rev.scaleTarget = content.rectTransform;
            rev.initialScale = initialScale;
            rev.fade = group;
            rev.content = null;
            rev.exitNone = true;
            rev.enterDamping = scaleIn.DampingRatio; rev.enterStiffness = scaleIn.Stiffness;
            rev.fadeInStiffness = fadeInStiffness;
            M3UdonBridge.Sync(rev);
            Reveals.Add(rev);
            return rev;
        }
        public ColorRef RippleColor, RippleColorSelected;

        /// <summary>Elevation level per state: rest, hover, pressed, disabled (null = no shadow).</summary>
        public int[] Elevation;
        public Image[] Shadows;
        public Func<float, CornerShape> ShadowShape;

        public void Color(Graphic g, ColorRef enabled, ColorRef? enabledSelected = null, ColorRef? disabled = null, ColorRef? disabledSelected = null)
        {
            Colors.Add(new StateColorEntry
            {
                graphic = g,
                enabled = enabled,
                enabledSelected = enabledSelected ?? enabled,
                disabled = disabled ?? enabled,
                disabledSelected = disabledSelected ?? disabled ?? enabledSelected ?? enabled,
            });
        }
    }

    public static class M3Interaction
    {
        public static M3Interactive Apply(InteractionSpec spec)
        {
            var go = spec.Root.gameObject;
            // Selectable: Button, with the ColorTint transition on the state layer (hover 0.08 baked into its color).
            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.interactable = spec.Enabled;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (spec.StateLayer != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = spec.StateLayer;
                var cb = ColorBlock.defaultColorBlock;
                cb.normalColor = new Color(1, 1, 1, 0);
                cb.highlightedColor = new Color(1, 1, 1, 1);
                cb.pressedColor = new Color(1, 1, 1, 1);
                cb.selectedColor = new Color(1, 1, 1, 0);
                cb.disabledColor = new Color(1, 1, 1, 0);
                cb.colorMultiplier = 1f;
                // Ripple.kt: hover state layer fades in/out over 15 ms
                cb.fadeDuration = 0.015f;
                button.colors = cb;
                spec.StateLayer.canvasRenderer.SetColor(new Color(1, 1, 1, 0));
            }
            else
            {
                button.transition = Selectable.Transition.None;
            }
            // A raycast target covering the whole component.
            var hit = go.GetComponent<Image>();
            if (hit == null)
            {
                var hitRt = M3Build.Rect("Hit", spec.Root);
                M3Build.Fill(hitRt);
                hitRt.SetAsFirstSibling();
                var le = hitRt.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                hit = hitRt.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                hit.sprite = null;
                hit.raycastTarget = true;
            }

            var it = M3UdonBridge.Add<M3Interactive>(go);
            it.selectable = button;
            it.isToggle = spec.Toggle;
            it.selected = spec.Selected;
            it.opticalContent = spec.OpticalContent;
            it.opticalA = spec.OpticalA;
            it.opticalB = spec.OpticalB;
            it.opticalMin = spec.OpticalMin;
            it.opticalMax = spec.OpticalMax;
            it.showWhenSelected = spec.ShowWhenSelected.ToArray();
            it.hideWhenSelected = spec.HideWhenSelected.ToArray();
            it.revealWhenSelected = spec.Reveals.ToArray();
            foreach (var g in spec.ShowWhenSelected) g.SetActive(spec.Selected);
            foreach (var g in spec.HideWhenSelected) g.SetActive(!spec.Selected);

            // Shape frames
            float min = spec.MinShape ?? Mathf.Min(Mathf.Min(spec.PressedShape, spec.SelectedPressedShape, spec.RestShape, spec.SelectedShape),
                Mathf.Min(spec.HoveredShape ?? spec.RestShape, spec.InactiveShape ?? spec.RestShape));
            // spatial springs overshoot: leave room below the smallest target
            float overshoot = spec.Spring.DampingRatio < 1f ? OvershootFraction(spec.Spring) * Mathf.Abs(spec.MaxShape - min) : 0f;
            float frameMin = Mathf.Max(0f, min - overshoot);
            float frameMax = spec.MaxShape;
            int frameCount = 0;
            var layers = new List<Image>();
            var frames = new List<Sprite>();
            foreach (var (image, cell) in spec.ShapeLayers)
            {
                var f = M3Build.RadiusFrames(cell, frameMin, frameMax, out frameCount);
                layers.Add(image);
                frames.AddRange(f);
            }
            it.shapeLayers = layers.ToArray();
            it.shapeFrames = frames.ToArray();
            it.frameCount = frameCount;
            it.shapeMin = frameMin;
            it.shapeMax = frameMax;
            it.restShape = Mathf.Min(spec.RestShape, frameMax);
            it.pressedShape = Mathf.Min(spec.PressedShape, frameMax);
            it.selectedShape = Mathf.Min(spec.SelectedShape, frameMax);
            it.selectedPressedShape = Mathf.Min(spec.SelectedPressedShape, frameMax);
            it.useHoveredShape = spec.HoveredShape.HasValue;
            it.hoveredShape = spec.HoveredShape ?? it.restShape;
            it.useInactiveShape = spec.InactiveShape.HasValue;
            it.inactiveShape = spec.InactiveShape.HasValue ? Mathf.Min(spec.InactiveShape.Value, frameMax) : it.restShape;
            it.shapeDamping = spec.Spring.DampingRatio;
            it.shapeStiffness = spec.Spring.Stiffness;
            // initial sprites
            float initial = spec.Selected ? it.selectedShape : it.restShape;
            int initialFrame = Mathf.Clamp(Mathf.RoundToInt((initial - frameMin) / Mathf.Max(frameMax - frameMin, 1e-4f) * (frameCount - 1)), 0, frameCount - 1);
            for (int i = 0; i < layers.Count; i++) layers[i].sprite = it.shapeFrames[i * frameCount + initialFrame];
            if (spec.OpticalContent != null)
            {
                float ox = Mathf.Clamp(spec.OpticalA + spec.OpticalB * initial, spec.OpticalMin, spec.OpticalMax);
                spec.OpticalContent.anchoredPosition = new Vector2(ox, spec.OpticalContent.anchoredPosition.y);
            }

            // Ripple: radius from 0.3 * max(w, h) to half the diagonal + 10 dp (bounded ripple), clipped to the
            // pressed shape. Center, radius and alpha come from the runtime (_M3Dyn of a pooled material);
            // the cell itself is the full-size ripple.
            if (spec.Ripple != null)
            {
                var cell = ShapeCell.Create(ShapeKind.Ripple);
                cell = spec.RippleClipShape.HasValue ? cell.WithShape(spec.RippleClipShape.Value) : cell.WithRadius(spec.RippleClipRadius);
                it.ripple = spec.Ripple;
                spec.Ripple.sprite = M3ShapeAtlas.Get(cell.WithExtra(0, 0, 1f, 10f));
                spec.Ripple.enabled = false;
                // ripple color is set by the theme (palette index); keep alpha 0 until pressed
                var c = spec.Ripple.color;
                c.a = 0f;
                spec.Ripple.color = c;
            }

            // Elevation
            if (spec.Elevation != null && spec.Shadows != null)
            {
                var shadowFrames = new Sprite[spec.Shadows.Length * 4 * frameCount];
                for (int layer = 0; layer < spec.Shadows.Length; layer++)
                for (int state = 0; state < 4; state++)
                for (int f = 0; f < frameCount; f++)
                {
                    int level = spec.Elevation[state];
                    float r = Mathf.Lerp(frameMin, frameMax, f / (float)Mathf.Max(frameCount - 1, 1));
                    shadowFrames[(layer * 4 + state) * frameCount + f] = level <= 0 ? null
                        : M3ShapeAtlas.Get(M3Build.ShadowCell(spec.ShadowShape(r), level, layer, out _));
                }
                it.shadowLayers = spec.Shadows;
                it.shadowFrames = shadowFrames;
                // continuous parameters of each layer and state for the elevation tween
                var shadowParams = new Vector4[spec.Shadows.Length * 4];
                for (int layer = 0; layer < spec.Shadows.Length; layer++)
                for (int state = 0; state < 4; state++)
                    shadowParams[layer * 4 + state] = M3Build.ShadowParams(spec.Elevation[state], layer);
                it.shadowParams = shadowParams;
                int startState = spec.Enabled ? 0 : 3;
                for (int layer = 0; layer < spec.Shadows.Length; layer++)
                {
                    var s = shadowFrames[(layer * 4 + startState) * frameCount + initialFrame];
                    spec.Shadows[layer].sprite = s;
                    spec.Shadows[layer].enabled = s != null;
                }
            }

            // Colors (palette indices are written by the theme baker)
            var sc = go.GetComponent<M3StateColors>();
            if (sc == null) sc = go.AddComponent<M3StateColors>();
            sc.entries = new List<StateColorEntry>();
            foreach (var e in spec.Colors)
            {
                var entry = e;
                entry.animate = spec.ColorStiffness > 0f && (spec.AnimateAllColors || spec.AnimatedColors.Contains(e.graphic));
                sc.entries.Add(entry);
            }
            it.colorStiffness = spec.ColorStiffness;
            sc.hasRipple = spec.Ripple != null;
            sc.ripple = spec.RippleColor;
            sc.rippleSelected = spec.RippleColorSelected;
            foreach (var e in spec.Colors)
            {
                var binding = e.graphic.GetComponent<M3ColorBinding>();
                if (binding != null) UnityEngine.Object.DestroyImmediate(binding);
            }

            // Events
            M3UdonBridge.WirePointer(go, EventTriggerType.PointerDown, it, nameof(M3Interactive._Down));
            M3UdonBridge.WirePointer(go, EventTriggerType.PointerUp, it, nameof(M3Interactive._Up));
            M3UdonBridge.WirePointer(go, EventTriggerType.PointerEnter, it, nameof(M3Interactive._Enter));
            M3UdonBridge.WirePointer(go, EventTriggerType.PointerExit, it, nameof(M3Interactive._Exit));
            if (spec.Toggle) M3UdonBridge.Wire(button.onClick, it, nameof(M3Interactive._Click));
            M3UdonBridge.Sync(it);
            return it;
        }

        /// <summary>Peak overshoot of a unit step for an under-damped spring.</summary>
        public static float OvershootFraction(Spring s)
        {
            if (s.DampingRatio >= 1f) return 0f;
            double z = s.DampingRatio;
            return (float)Math.Exp(-z * Math.PI / Math.Sqrt(1 - z * z));
        }
    }
}
