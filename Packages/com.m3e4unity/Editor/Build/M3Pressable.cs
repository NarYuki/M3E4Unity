using M3E4Unity.Tokens;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Colors, shapes and motion of a pressable container (Surface(onClick) in Compose).</summary>
    public sealed class PressableStyle
    {
        /// <summary>Container role; null = transparent.</summary>
        public ColorRole? Container;
        /// <summary>Container when selected (toggles); null = same as unselected.</summary>
        public ColorRole? ContainerChecked;
        /// <summary>Disabled container color role and opacity (composited over the background).</summary>
        public ColorRole DisabledContainer = ColorRole.OnSurface;
        public float DisabledContainerOpacity = 0.1f;
        /// <summary>Whether a disabled selected toggle keeps a container even if the unselected one is transparent.</summary>
        public bool DisabledCheckedHasContainer = true;
        /// <summary>ToggleButtonColors: a disabled toggle uses disabledContainerColor whether checked or not.</summary>
        public bool DisabledUncheckedHasContainer;

        public ColorRole Content;
        public ColorRole? ContentChecked;
        public ColorRole DisabledContent = ColorRole.OnSurface;
        public float DisabledContentOpacity = 0.38f;

        public ColorRole? Outline;
        public float OutlineWidth = 1f;
        public float DisabledOutlineOpacity = 0.12f;
        /// <summary>Selected toggles drop the border (Compose *ToggleButtonDefaults.border).</summary>
        public bool OutlineWhenChecked;

        /// <summary>Elevation level per state (rest, hover, pressed, disabled); null = flat.</summary>
        public int[] Elevation;

        public float Height;
        public float RestShape, PressedShape, CheckedShape, CheckedPressedShape = -1, MaxShape;
        public Spring Spring = new Spring(1f, 1600f);

        public bool Toggle, Selected, Enabled = true;
        public bool Ripple = true;

        /// <summary>
        /// Maps the animated shape value to a full corner shape (asymmetric shapes such as
        /// connected button groups and split buttons). Default: all corners = value.
        /// </summary>
        public System.Func<float, CornerShape> ShapeFor;

        /// <summary>Hovered shape value (list items); null = no hover morph.</summary>
        public float? HoveredShape;

        /// <summary>Checked overlay of the content color (SplitButton trailing: PressedStateLayerOpacity).</summary>
        public float CheckedOverlay;
    }

    /// <summary>
    /// Builds the layers of a pressable container (shadows, container, outline, state layer, ripple)
    /// and wires the interaction once the content has been added.
    /// </summary>
    public sealed class M3Pressable
    {
        public readonly RectTransform Root;
        public readonly InteractionSpec Spec;
        readonly PressableStyle style;
        readonly M3Context ctx;
        readonly ColorRole containerRole, containerChecked;

        public M3Pressable(RectTransform root, M3Context ctx, PressableStyle style)
        {
            Root = root;
            this.ctx = ctx;
            this.style = style;
            var bg = ctx.Background;
            containerRole = style.Container ?? bg;
            containerChecked = style.ContainerChecked ?? containerRole;
            float checkedPressed = style.CheckedPressedShape >= 0 ? style.CheckedPressedShape : style.PressedShape;
            Spec = new InteractionSpec
            {
                Root = root,
                Context = ctx,
                Toggle = style.Toggle,
                Selected = style.Selected,
                Enabled = style.Enabled,
                RestShape = style.RestShape,
                PressedShape = style.PressedShape,
                SelectedShape = style.Toggle ? style.CheckedShape : style.RestShape,
                SelectedPressedShape = checkedPressed,
                MaxShape = style.MaxShape,
                Spring = style.Spring,
                HoveredShape = style.HoveredShape,
            };
            float rest = style.Selected ? Spec.SelectedShape : style.RestShape;
            var shapeFor = style.ShapeFor ?? (r => new CornerShape(r));

            bool hasContainer = style.Container.HasValue || (style.Toggle && style.ContainerChecked.HasValue);
            if (hasContainer)
            {
                var container = M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(rest)), ColorRef.Role(containerRole));
                Ignore(container);
                Spec.ShapeLayers.Add((container, r => ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(r))));
                var disabled = ColorRef.Over(bg, style.DisabledContainer, style.DisabledContainerOpacity);
                Spec.Color(container,
                    style.Container.HasValue ? ColorRef.Role(containerRole) : ColorRef.Clear,
                    style.CheckedOverlay > 0 ? ColorRef.Over(containerChecked, style.ContentChecked ?? style.Content, style.CheckedOverlay) : ColorRef.Role(containerChecked),
                    style.Container.HasValue || style.DisabledUncheckedHasContainer ? disabled : ColorRef.Clear,
                    style.DisabledCheckedHasContainer ? disabled : ColorRef.Clear);
            }

            if (style.Outline.HasValue)
            {
                float w = style.OutlineWidth;
                var outline = M3Build.Shape("Outline", root, ShapeCell.Create(ShapeKind.Stroke).WithShape(shapeFor(rest)).WithStroke(w), ColorRef.Role(style.Outline.Value));
                Ignore(outline);
                Spec.ShapeLayers.Add((outline, r => ShapeCell.Create(ShapeKind.Stroke).WithShape(shapeFor(r)).WithStroke(w)));
                var on = ColorRef.Over(bg, style.Outline.Value, 1f);
                var off = ColorRef.Over(bg, style.Outline.Value, style.DisabledOutlineOpacity);
                Spec.Color(outline, on, style.OutlineWhenChecked ? on : ColorRef.Clear, off, style.OutlineWhenChecked ? off : ColorRef.Clear);
            }

            var contentChecked = style.ContentChecked ?? style.Content;
            var stateLayer = M3Build.Shape("StateLayer", root, ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(rest)), ColorRef.Clear);
            Ignore(stateLayer);
            Spec.ShapeLayers.Add((stateLayer, r => ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(r))));
            Spec.StateLayer = stateLayer;
            Spec.Color(stateLayer,
                ColorRef.Over(containerRole, style.Content, StateLayer.Hover),
                ColorRef.Over(containerChecked, contentChecked, StateLayer.Hover),
                ColorRef.Clear, ColorRef.Clear);

            if (style.Ripple)
            {
                var ripple = M3Build.Shape("Ripple", root, ShapeCell.Create(ShapeKind.Ripple).WithShape(shapeFor(style.PressedShape)), ColorRef.Clear);
                Ignore(ripple);
                Spec.Ripple = ripple;
                Spec.RippleClipRadius = style.PressedShape;
                Spec.RippleClipShape = shapeFor(style.PressedShape);
                Spec.RippleColor = ColorRef.Over(containerRole, style.Content, StateLayer.Hover, style.Content, StateLayer.Pressed);
                Spec.RippleColorSelected = ColorRef.Over(containerChecked, contentChecked, StateLayer.Hover, contentChecked, StateLayer.Pressed);
            }

            if (style.Elevation != null && hasContainer && (style.Elevation[0] > 0 || style.Elevation[1] > 0 || style.Elevation[2] > 0))
            {
                var shadows = M3Build.Shadows(root, CornerShape.Full, 1, 0);
                foreach (var sh in shadows) Ignore(sh);
                Spec.Shadows = shadows;
                Spec.Elevation = style.Elevation;
                Spec.ShadowShape = shapeFor;
            }
        }

        /// <summary>Content colors of a graphic (text or icon) in all states.</summary>
        public void Content(Graphic g, ColorRole? enabled = null, ColorRole? enabledChecked = null)
        {
            var bg = ctx.Background;
            var e = enabled ?? style.Content;
            var ec = enabledChecked ?? style.ContentChecked ?? e;
            bool containerDisabled = style.Container.HasValue || style.DisabledUncheckedHasContainer;
            var disabled = containerDisabled
                ? ColorRef.Over(bg, style.DisabledContainer, style.DisabledContainerOpacity, style.DisabledContent, style.DisabledContentOpacity)
                : ColorRef.Over(bg, style.DisabledContent, style.DisabledContentOpacity);
            var disabledChecked = style.Toggle && style.DisabledCheckedHasContainer && (style.ContainerChecked.HasValue || style.Container.HasValue)
                ? ColorRef.Over(bg, style.DisabledContainer, style.DisabledContainerOpacity, style.DisabledContent, style.DisabledContentOpacity)
                : disabled;
            Spec.Color(g, ColorRef.Role(e), ColorRef.Role(ec), disabled, disabledChecked);
        }

        public Udon.M3Interactive Finish() => M3Interaction.Apply(Spec);

        public static void Ignore(Graphic g)
        {
            var le = g.GetComponent<LayoutElement>();
            if (le == null) le = g.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        public static float Radius(ShapeRole role, float height)
        {
            var shape = ShapeScale.Get(role);
            return shape.IsFull ? height / 2f : shape.TopStart;
        }
    }

    public static class M3Colors
    {
        /// <summary>ColorScheme.contentColorFor(backgroundColor) from ColorScheme.kt.</summary>
        public static ColorRole ContentFor(ColorRole background)
        {
            switch (background)
            {
                case ColorRole.Primary: return ColorRole.OnPrimary;
                case ColorRole.Secondary: return ColorRole.OnSecondary;
                case ColorRole.Tertiary: return ColorRole.OnTertiary;
                case ColorRole.Background: return ColorRole.OnBackground;
                case ColorRole.Error: return ColorRole.OnError;
                case ColorRole.PrimaryContainer: return ColorRole.OnPrimaryContainer;
                case ColorRole.SecondaryContainer: return ColorRole.OnSecondaryContainer;
                case ColorRole.TertiaryContainer: return ColorRole.OnTertiaryContainer;
                case ColorRole.ErrorContainer: return ColorRole.OnErrorContainer;
                case ColorRole.InverseSurface: return ColorRole.InverseOnSurface;
                case ColorRole.SurfaceVariant: return ColorRole.OnSurfaceVariant;
                case ColorRole.PrimaryFixed: case ColorRole.PrimaryFixedDim: return ColorRole.OnPrimaryFixed;
                case ColorRole.SecondaryFixed: case ColorRole.SecondaryFixedDim: return ColorRole.OnSecondaryFixed;
                case ColorRole.TertiaryFixed: case ColorRole.TertiaryFixedDim: return ColorRole.OnTertiaryFixed;
                default: return ColorRole.OnSurface;
            }
        }
    }
}
