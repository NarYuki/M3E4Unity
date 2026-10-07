using M3E4Unity.Tokens;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum IconButtonStyle { Standard, Filled, Tonal, Outlined }
    public enum IconButtonWidth { Narrow, Uniform, Wide }

    /// <summary>Icon buttons and icon toggle buttons (IconButton.kt, IconButtonDefaults.kt).</summary>
    public static class M3IconButtons
    {
        struct SizeSpec
        {
            public float Height, Icon, Narrow, Uniform, Wide, Outline;
            public ShapeRole Square, Pressed, SelectedRound, SelectedSquare;
        }

        static SizeSpec For(ButtonSize size)
        {
            switch (size)
            {
                case ButtonSize.ExtraSmall:
                    return new SizeSpec
                    {
                        Height = XSmallIconButtonTokens.ContainerHeight, Icon = XSmallIconButtonTokens.IconSize,
                        Narrow = XSmallIconButtonTokens.NarrowLeadingSpace + XSmallIconButtonTokens.NarrowTrailingSpace,
                        // extraSmallContainerSize(Uniform) uses DefaultLeadingSpace twice
                        Uniform = XSmallIconButtonTokens.DefaultLeadingSpace * 2,
                        Wide = XSmallIconButtonTokens.WideLeadingSpace + XSmallIconButtonTokens.WideTrailingSpace,
                        Outline = XSmallIconButtonTokens.OutlinedOutlineWidth,
                        Square = XSmallIconButtonTokens.ContainerShapeSquare, Pressed = XSmallIconButtonTokens.PressedContainerShape,
                        SelectedRound = XSmallIconButtonTokens.SelectedContainerShapeRound, SelectedSquare = XSmallIconButtonTokens.SelectedContainerShapeSquare,
                    };
                case ButtonSize.Medium:
                    return new SizeSpec
                    {
                        Height = MediumIconButtonTokens.ContainerHeight, Icon = MediumIconButtonTokens.IconSize,
                        Narrow = MediumIconButtonTokens.NarrowLeadingSpace + MediumIconButtonTokens.NarrowTrailingSpace,
                        Uniform = MediumIconButtonTokens.DefaultLeadingSpace * 2,
                        Wide = MediumIconButtonTokens.WideLeadingSpace + MediumIconButtonTokens.WideTrailingSpace,
                        Outline = MediumIconButtonTokens.OutlinedOutlineWidth,
                        Square = MediumIconButtonTokens.ContainerShapeSquare, Pressed = MediumIconButtonTokens.PressedContainerShape,
                        SelectedRound = MediumIconButtonTokens.SelectedContainerShapeRound, SelectedSquare = MediumIconButtonTokens.SelectedContainerShapeSquare,
                    };
                case ButtonSize.Large:
                    return new SizeSpec
                    {
                        Height = LargeIconButtonTokens.ContainerHeight, Icon = LargeIconButtonTokens.IconSize,
                        Narrow = LargeIconButtonTokens.NarrowLeadingSpace + LargeIconButtonTokens.NarrowTrailingSpace,
                        Uniform = LargeIconButtonTokens.UniformLeadingSpace + LargeIconButtonTokens.UniformTrailingSpace,
                        Wide = LargeIconButtonTokens.WideLeadingSpace + LargeIconButtonTokens.WideTrailingSpace,
                        Outline = LargeIconButtonTokens.OutlinedOutlineWidth,
                        Square = LargeIconButtonTokens.ContainerShapeSquare, Pressed = LargeIconButtonTokens.PressedContainerShape,
                        SelectedRound = LargeIconButtonTokens.SelectedContainerShapeRound, SelectedSquare = LargeIconButtonTokens.SelectedContainerShapeSquare,
                    };
                case ButtonSize.ExtraLarge:
                    return new SizeSpec
                    {
                        Height = XLargeIconButtonTokens.ContainerHeight, Icon = XLargeIconButtonTokens.IconSize,
                        Narrow = XLargeIconButtonTokens.NarrowLeadingSpace + XLargeIconButtonTokens.NarrowTrailingSpace,
                        Uniform = XLargeIconButtonTokens.DefaultLeadingSpace * 2,
                        Wide = XLargeIconButtonTokens.WideLeadingSpace + XLargeIconButtonTokens.WideTrailingSpace,
                        Outline = XLargeIconButtonTokens.OutlinedOutlineWidth,
                        Square = XLargeIconButtonTokens.ContainerShapeSquare, Pressed = XLargeIconButtonTokens.PressedContainerShape,
                        SelectedRound = XLargeIconButtonTokens.SelectedContainerShapeRound, SelectedSquare = XLargeIconButtonTokens.SelectedContainerShapeSquare,
                    };
                default:
                    return new SizeSpec
                    {
                        Height = SmallIconButtonTokens.ContainerHeight, Icon = SmallIconButtonTokens.IconSize,
                        Narrow = SmallIconButtonTokens.NarrowLeadingSpace + SmallIconButtonTokens.NarrowTrailingSpace,
                        Uniform = SmallIconButtonTokens.DefaultLeadingSpace * 2,
                        Wide = SmallIconButtonTokens.WideLeadingSpace + SmallIconButtonTokens.WideTrailingSpace,
                        Outline = SmallIconButtonTokens.OutlinedOutlineWidth,
                        Square = SmallIconButtonTokens.ContainerShapeSquare, Pressed = SmallIconButtonTokens.PressedContainerShape,
                        SelectedRound = SmallIconButtonTokens.SelectedContainerShapeRound, SelectedSquare = SmallIconButtonTokens.SelectedContainerShapeSquare,
                    };
            }
        }

        /// <param name="contentColor">Standard / outlined icon buttons use LocalContentColor; defaults to contentColorFor(background).</param>
        public static RectTransform Create(Transform parent, M3Context ctx, string icon,
            IconButtonStyle style = IconButtonStyle.Standard, ButtonSize size = ButtonSize.Small,
            IconButtonWidth width = IconButtonWidth.Uniform, ButtonShape shape = ButtonShape.Round,
            bool toggle = false, bool selected = false, bool enabled = true, ColorRole? contentColor = null)
        {
            var s = For(size);
            var local = contentColor ?? M3Colors.ContentFor(ctx.Background);
            var p = new PressableStyle { Height = s.Height, Toggle = toggle, Selected = selected, Enabled = enabled };
            switch (style)
            {
                case IconButtonStyle.Filled:
                    p.Container = toggle ? FilledIconButtonTokens.UnselectedContainerColor : FilledIconButtonTokens.ContainerColor;
                    p.Content = toggle ? FilledIconButtonTokens.UnselectedColor : FilledIconButtonTokens.Color;
                    p.ContainerChecked = FilledIconButtonTokens.SelectedContainerColor;
                    p.ContentChecked = FilledIconButtonTokens.SelectedColor;
                    p.DisabledContainer = FilledIconButtonTokens.DisabledContainerColor;
                    p.DisabledContainerOpacity = FilledIconButtonTokens.DisabledContainerOpacity;
                    p.DisabledContent = FilledIconButtonTokens.DisabledColor;
                    p.DisabledContentOpacity = FilledIconButtonTokens.DisabledOpacity;
                    break;
                case IconButtonStyle.Tonal:
                    p.Container = toggle ? FilledTonalIconButtonTokens.UnselectedContainerColor : FilledTonalIconButtonTokens.ContainerColor;
                    p.Content = toggle ? FilledTonalIconButtonTokens.UnselectedColor : FilledTonalIconButtonTokens.Color;
                    p.ContainerChecked = FilledTonalIconButtonTokens.SelectedContainerColor;
                    p.ContentChecked = FilledTonalIconButtonTokens.SelectedColor;
                    p.DisabledContainer = FilledTonalIconButtonTokens.DisabledContainerColor;
                    p.DisabledContainerOpacity = FilledTonalIconButtonTokens.DisabledContainerOpacity;
                    p.DisabledContent = FilledTonalIconButtonTokens.DisabledColor;
                    p.DisabledContentOpacity = FilledTonalIconButtonTokens.DisabledOpacity;
                    break;
                case IconButtonStyle.Outlined:
                    // defaultOutlinedIconToggleButtonColors: checked container SelectedContainerColor, content contentColorFor(it)
                    p.Content = local;
                    p.ContainerChecked = toggle ? OutlinedIconButtonTokens.SelectedContainerColor : (ColorRole?)null;
                    p.ContentChecked = M3Colors.ContentFor(OutlinedIconButtonTokens.SelectedContainerColor);
                    // outlinedIconButtonBorder: LocalContentColor, disabled at DisabledOpacity; checked toggles drop it
                    p.Outline = local;
                    p.OutlineWidth = SmallIconButtonTokens.OutlinedOutlineWidth;
                    p.DisabledOutlineOpacity = OutlinedIconButtonTokens.DisabledOpacity;
                    p.DisabledContent = local;
                    p.DisabledContentOpacity = OutlinedIconButtonTokens.DisabledOpacity;
                    // disabled toggles keep a transparent container
                    p.DisabledCheckedHasContainer = false;
                    break;
                default:
                    // defaultIconButtonColors / defaultIconToggleButtonColors
                    p.Content = local;
                    p.ContentChecked = toggle ? StandardIconButtonTokens.SelectedColor : (ColorRole?)null;
                    p.DisabledContent = local;
                    p.DisabledContentOpacity = StandardIconButtonTokens.DisabledOpacity;
                    p.DisabledCheckedHasContainer = false;
                    break;
            }

            float w = s.Icon + (width == IconButtonWidth.Narrow ? s.Narrow : width == IconButtonWidth.Wide ? s.Wide : s.Uniform);
            float full = Mathf.Min(w, s.Height) / 2f;
            p.RestShape = shape == ButtonShape.Round ? full : M3Pressable.Radius(s.Square, s.Height);
            p.PressedShape = M3Pressable.Radius(s.Pressed, s.Height);
            // toggleableShapes: checked = SelectedContainerShapeRound (round buttons) / SelectedContainerShapeSquare (square)
            p.CheckedShape = Mathf.Min(full, M3Pressable.Radius(shape == ButtonShape.Round ? s.SelectedRound : s.SelectedSquare, s.Height));
            p.MaxShape = full;
            // IconButton.kt: DefaultEffects for icon buttons and icon toggle buttons
            p.Spring = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects);

            var root = M3Build.Rect($"IconButton ({style} {size} {icon})", parent);
            M3Build.Size(root, w, s.Height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = s.Height;
            var pressable = new M3Pressable(root, ctx, p);
            var iconText = M3Build.Icon("Icon", root, icon, s.Icon, ColorRef.Role(p.Content), ctx);
            var irt = (RectTransform)iconText.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            M3Pressable.Ignore(iconText);
            pressable.Content(iconText);
            pressable.Finish();
            return root;
        }
    }
}
