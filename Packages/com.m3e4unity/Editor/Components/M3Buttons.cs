using M3E4Unity.Tokens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum ButtonStyle { Filled, Tonal, Elevated, Outlined, Text }
    public enum ButtonSize { ExtraSmall, Small, Medium, Large, ExtraLarge }
    public enum ButtonShape { Round, Square }
    public enum ButtonSegment { None, ConnectedLeading, ConnectedMiddle, ConnectedTrailing, SplitLeading, SplitTrailing }

    /// <summary>
    /// Buttons and toggle buttons (Button.kt / ToggleButton.kt, ButtonDefaults / ToggleButtonDefaults).
    /// </summary>
    public static class M3Buttons
    {
        /// <summary>Per-size values from ButtonDefaults (contentPaddingFor, iconSizeFor, iconSpacingFor, textStyleFor, shapesFor).</summary>
        public struct SizeSpec
        {
            public float Height, PaddingStart, PaddingEnd, IconSize, IconSpacing, OutlineWidth;
            public TypeRole Text;
            public ShapeRole Square, Pressed, ToggleChecked;
            public float TogglePressed;

            public static SizeSpec For(ButtonSize size)
            {
                switch (size)
                {
                    case ButtonSize.ExtraSmall:
                        // ExtraSmallContentPadding (12/12, "TODO update with ButtonXSmallTokens"), ExtraSmallIconSpacing 4
                        return new SizeSpec
                        {
                            Height = ButtonXSmallTokens.ContainerHeight, PaddingStart = 12f, PaddingEnd = 12f,
                            IconSize = ButtonXSmallTokens.IconSize, IconSpacing = 4f, OutlineWidth = ButtonXSmallTokens.OutlinedOutlineWidth,
                            Text = TypeRole.LabelLarge, Square = ButtonXSmallTokens.ContainerShapeSquare,
                            Pressed = ButtonXSmallTokens.PressedContainerShape, ToggleChecked = ButtonXSmallTokens.ContainerShapeSquare,
                            TogglePressed = -1,
                        };
                    case ButtonSize.Medium:
                        return new SizeSpec
                        {
                            Height = ButtonMediumTokens.ContainerHeight, PaddingStart = ButtonMediumTokens.LeadingSpace, PaddingEnd = ButtonMediumTokens.TrailingSpace,
                            IconSize = ButtonMediumTokens.IconSize, IconSpacing = ButtonMediumTokens.IconLabelSpace, OutlineWidth = ButtonMediumTokens.OutlinedOutlineWidth,
                            Text = TypeRole.TitleMedium, Square = ButtonMediumTokens.ContainerShapeSquare,
                            Pressed = ButtonMediumTokens.PressedContainerShape, ToggleChecked = ButtonMediumTokens.ContainerShapeSquare,
                            TogglePressed = -1,
                        };
                    case ButtonSize.Large:
                        return new SizeSpec
                        {
                            Height = ButtonLargeTokens.ContainerHeight, PaddingStart = ButtonLargeTokens.LeadingSpace, PaddingEnd = ButtonLargeTokens.TrailingSpace,
                            IconSize = ButtonLargeTokens.IconSize, IconSpacing = ButtonLargeTokens.IconLabelSpace, OutlineWidth = ButtonLargeTokens.OutlinedOutlineWidth,
                            Text = TypeRole.HeadlineSmall, Square = ButtonLargeTokens.ContainerShapeSquare,
                            Pressed = ButtonLargeTokens.PressedContainerShape, ToggleChecked = ButtonLargeTokens.ContainerShapeSquare,
                            TogglePressed = -1,
                        };
                    case ButtonSize.ExtraLarge:
                        return new SizeSpec
                        {
                            Height = ButtonXLargeTokens.ContainerHeight, PaddingStart = ButtonXLargeTokens.LeadingSpace, PaddingEnd = ButtonXLargeTokens.TrailingSpace,
                            IconSize = ButtonXLargeTokens.IconSize, IconSpacing = ButtonXLargeTokens.IconLabelSpace, OutlineWidth = ButtonXLargeTokens.OutlinedOutlineWidth,
                            Text = TypeRole.HeadlineLarge, Square = ButtonXLargeTokens.ContainerShapeSquare,
                            Pressed = ButtonXLargeTokens.PressedContainerShape, ToggleChecked = ButtonXLargeTokens.ContainerShapeSquare,
                            TogglePressed = -1,
                        };
                    default:
                        // Small: SmallContentPadding (ButtonSmallTokens Leading/Trailing), ToggleButtonDefaults.pressedShape = 6 dp
                        return new SizeSpec
                        {
                            Height = ButtonSmallTokens.ContainerHeight, PaddingStart = ButtonSmallTokens.LeadingSpace, PaddingEnd = ButtonSmallTokens.TrailingSpace,
                            IconSize = ButtonSmallTokens.IconSize, IconSpacing = ButtonSmallTokens.IconLabelSpace, OutlineWidth = ButtonSmallTokens.OutlinedOutlineWidth,
                            Text = TypeRole.LabelLarge, Square = ButtonSmallTokens.ContainerShapeSquare,
                            Pressed = ButtonSmallTokens.PressedContainerShape, ToggleChecked = ButtonSmallTokens.SelectedContainerShapeSquare,
                            TogglePressed = 6f,
                        };
                }
            }
        }

        /// <summary>Colors of a style (ButtonDefaults.*ButtonColors and ToggleButtonDefaults.*ToggleButtonColors).</summary>
        struct StyleColors
        {
            public ColorRole? Container, ContainerChecked;
            public ColorRole Content, ContentChecked;
            public ColorRole? Outline;
            public int[] Elevation; // rest, hover, pressed, disabled
        }

        static StyleColors Colors(ButtonStyle style, bool toggle)
        {
            switch (style)
            {
                case ButtonStyle.Tonal:
                    return new StyleColors
                    {
                        Container = toggle ? TonalButtonTokens.UnselectedContainerColor : FilledTonalButtonTokens.ContainerColor,
                        Content = toggle ? TonalButtonTokens.UnselectedLabelTextColor : FilledTonalButtonTokens.LabelTextColor,
                        ContainerChecked = TonalButtonTokens.SelectedContainerColor,
                        ContentChecked = TonalButtonTokens.SelectedLabelTextColor,
                        Elevation = Levels(FilledTonalButtonTokens.ContainerElevation, FilledTonalButtonTokens.HoverContainerElevation,
                            FilledTonalButtonTokens.PressedContainerElevation, 0f),
                    };
                case ButtonStyle.Elevated:
                    return new StyleColors
                    {
                        Container = toggle ? ElevatedButtonTokens.UnselectedContainerColor : ElevatedButtonTokens.ContainerColor,
                        Content = toggle ? ElevatedButtonTokens.UnselectedPressedLabelTextColor : ElevatedButtonTokens.LabelTextColor,
                        ContainerChecked = ElevatedButtonTokens.SelectedContainerColor,
                        ContentChecked = ElevatedButtonTokens.SelectedPressedLabelTextColor,
                        Elevation = Levels(ElevatedButtonTokens.ContainerElevation, ElevatedButtonTokens.HoveredContainerElevation,
                            ElevatedButtonTokens.PressedContainerElevation, ElevatedButtonTokens.DisabledContainerElevation),
                    };
                case ButtonStyle.Outlined:
                    return new StyleColors
                    {
                        Container = null,
                        Content = toggle ? OutlinedButtonTokens.UnselectedLabelTextColor : OutlinedButtonTokens.LabelTextColor,
                        ContainerChecked = OutlinedButtonTokens.SelectedContainerColor,
                        ContentChecked = OutlinedButtonTokens.SelectedLabelTextColor,
                        Outline = OutlinedButtonTokens.OutlineColor,
                    };
                case ButtonStyle.Text:
                    // defaultTextButtonColors: content = ColorSchemeKeyTokens.Primary ("TODO replace with the token value")
                    return new StyleColors { Container = null, Content = ColorRole.Primary, ContentChecked = ColorRole.Primary };
                default:
                    return new StyleColors
                    {
                        Container = toggle ? FilledButtonTokens.UnselectedContainerColor : FilledButtonTokens.ContainerColor,
                        Content = toggle ? FilledButtonTokens.UnselectedPressedLabelTextColor : FilledButtonTokens.LabelTextColor,
                        ContainerChecked = FilledButtonTokens.SelectedContainerColor,
                        ContentChecked = FilledButtonTokens.SelectedPressedLabelTextColor,
                        Elevation = Levels(FilledButtonTokens.ContainerElevation, FilledButtonTokens.HoveredContainerElevation,
                            FilledButtonTokens.PressedContainerElevation, FilledButtonTokens.DisabledContainerElevation),
                    };
            }
        }

        static int[] Levels(float rest, float hover, float pressed, float disabled) =>
            new[] { Elevation.LevelOf(rest), Elevation.LevelOf(hover), Elevation.LevelOf(pressed), Elevation.LevelOf(disabled) };

        /// <summary>Creates a button. Returns its root RectTransform.</summary>
        /// <param name="segment">Position in a connected button group or split button (asymmetric shapes).</param>
        public static RectTransform Create(Transform parent, M3Context ctx, string label,
            ButtonStyle style = ButtonStyle.Filled, ButtonSize size = ButtonSize.Small, ButtonShape shape = ButtonShape.Round,
            string leadingIcon = null, string trailingIcon = null, bool toggle = false, bool selected = false, bool enabled = true,
            ButtonSegment segment = ButtonSegment.None, ColorRole? contentColor = null)
        {
            var s = SizeSpec.For(size);
            var c = Colors(style, toggle);
            // ButtonDefaults.xxxButtonColors(contentColor = ...)
            if (contentColor.HasValue) { c.Content = contentColor.Value; c.ContentChecked = contentColor.Value; }
            float full = s.Height / 2f;

            // Shapes: rest = round (full) or square token; pressed token; checked = square token (toggle)
            float rest = shape == ButtonShape.Round ? full : M3Pressable.Radius(s.Square, s.Height);
            float pressed = toggle && s.TogglePressed > 0 ? s.TogglePressed : M3Pressable.Radius(s.Pressed, s.Height);
            float checkedShape = shape == ButtonShape.Round ? M3Pressable.Radius(s.ToggleChecked, s.Height) : full;
            System.Func<float, CornerShape> shapeFor = null;
            float padStart, padEnd;
            bool textSmall = style == ButtonStyle.Text && size == ButtonSize.Small;
            // TextButtonContentPadding: 12 dp, and 16 dp at the end when there is an icon
            padStart = textSmall ? 12f : s.PaddingStart;
            padEnd = textSmall ? (leadingIcon != null ? 16f : 12f) : s.PaddingEnd;
            float opticalSign = 0f;
            float checkedOverlay = 0f;
            var spring = MotionScheme.Get(ctx.Motion, toggle ? MotionRole.FastSpatial : MotionRole.DefaultEffects);
            switch (segment)
            {
                case ButtonSegment.ConnectedLeading:
                case ButtonSegment.ConnectedMiddle:
                case ButtonSegment.ConnectedTrailing:
                    // ButtonGroupDefaults.connected*ButtonShapes: inner corners InnerCornerCornerSize, pressed
                    // PressedInnerCornerCornerSize, checked connectedButtonCheckedShape (CornerFull)
                    rest = ConnectedButtonGroupSmallTokens.InnerCornerCornerSize;
                    pressed = ConnectedButtonGroupSmallTokens.PressedInnerCornerCornerSize;
                    checkedShape = full;
                    if (segment == ButtonSegment.ConnectedLeading) shapeFor = r => new CornerShape(full, r, r, full);
                    else if (segment == ButtonSegment.ConnectedTrailing) shapeFor = r => new CornerShape(r, full, full, r);
                    else shapeFor = r => new CornerShape(r);
                    break;
                case ButtonSegment.SplitLeading:
                case ButtonSegment.SplitTrailing:
                    var sp = SplitSpec.For(size);
                    rest = sp.Inner;
                    pressed = sp.InnerPressed;
                    checkedShape = full; // TrailingCheckedShape = CircleShape
                    // SplitButtonDefaults: DefaultEffects for both buttons
                    spring = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects);
                    if (segment == ButtonSegment.SplitLeading)
                    {
                        shapeFor = r => new CornerShape(full, r, r, full);
                        padStart = sp.LeadingStart;
                        padEnd = sp.LeadingEnd;
                        opticalSign = 1f;
                    }
                    else
                    {
                        shapeFor = r => new CornerShape(r, full, full, r);
                        padStart = sp.TrailingStart;
                        padEnd = sp.TrailingEnd;
                        opticalSign = -1f;
                        checkedOverlay = StateLayer.Pressed; // TrailingButtonStateLayerAlpha
                        toggle = true;
                    }
                    break;
            }

            var root = M3Build.Rect($"Button ({style} {size}{(segment != ButtonSegment.None ? " " + segment : "")})", parent);
            root.sizeDelta = new Vector2(ButtonDefaultsMinWidth, s.Height);
            var rootLe = root.gameObject.AddComponent<LayoutElement>();
            rootLe.minHeight = rootLe.preferredHeight = s.Height;
            // Segments keep their content in a child so it can be shifted by horizontalCenterOptically.
            RectTransform contentRow = root;
            if (segment != ButtonSegment.None)
            {
                contentRow = M3Build.Rect("Content", root);
                M3Build.Fill(contentRow);
            }
            else rootLe.minWidth = ButtonDefaultsMinWidth;
            var rowLayout = M3Build.Row(contentRow, s.IconSpacing);
            rowLayout.padding = new RectOffset(Mathf.RoundToInt(padStart), Mathf.RoundToInt(padEnd), 0, 0);

            var p = new M3Pressable(root, ctx, new PressableStyle
            {
                Container = c.Container,
                ContainerChecked = toggle ? (checkedOverlay > 0 ? c.Container : c.ContainerChecked) : null,
                // outlined toggles: disabledContainerColor = DisabledOutlineColor at DisabledContainerOpacity, checked or not
                DisabledContainer = style == ButtonStyle.Outlined ? OutlinedButtonTokens.DisabledOutlineColor : FilledButtonTokens.DisabledContainerColor,
                DisabledUncheckedHasContainer = toggle && style == ButtonStyle.Outlined,
                DisabledContainerOpacity = FilledButtonTokens.DisabledContainerOpacity,
                Content = c.Content,
                ContentChecked = toggle ? (checkedOverlay > 0 ? c.Content : c.ContentChecked) : (ColorRole?)null,
                DisabledContent = FilledButtonTokens.DisabledLabelTextColor,
                DisabledContentOpacity = FilledButtonTokens.DisabledLabelTextOpacity,
                Outline = c.Outline,
                OutlineWidth = s.OutlineWidth,
                // outlinedButtonBorder(enabled = false): OutlineColor at DisabledContainerOpacity
                DisabledOutlineOpacity = OutlinedButtonTokens.DisabledContainerOpacity,
                Elevation = c.Elevation,
                Height = s.Height,
                RestShape = rest,
                PressedShape = pressed,
                CheckedShape = checkedShape,
                MaxShape = full,
                Spring = spring,
                Toggle = toggle,
                Selected = selected,
                Enabled = enabled,
                ShapeFor = shapeFor,
                CheckedOverlay = checkedOverlay,
            });

            // content above the container layers
            if (contentRow != root) contentRow.SetAsLastSibling();
            if (leadingIcon != null) p.Content(M3Build.Icon("LeadingIcon", contentRow, leadingIcon, s.IconSize, ColorRef.Role(c.Content), ctx));
            if (label != null) p.Content(M3Build.Text("Label", contentRow, label, s.Text, ColorRef.Role(c.Content), ctx));
            if (trailingIcon != null)
            {
                float iconSize = segment == ButtonSegment.SplitTrailing ? SplitSpec.For(size).TrailingIcon : s.IconSize;
                p.Content(M3Build.Icon("TrailingIcon", contentRow, trailingIcon, iconSize, ColorRef.Role(c.Content), ctx));
            }

            // Size to content (no ContentSizeFitter: it fights parent layout groups).
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRow);
            float width = LayoutUtility.GetPreferredWidth(contentRow);
            if (segment == ButtonSegment.SplitLeading || segment == ButtonSegment.SplitTrailing) width = Mathf.Max(48f, width); // Leading/TrailingButtonMinWidth
            else width = Mathf.Max(ButtonDefaultsMinWidth, width);
            root.sizeDelta = new Vector2(width, s.Height);
            if (segment != ButtonSegment.None) rootLe.preferredWidth = width;

            if (opticalSign != 0f)
            {
                // horizontalCenterOptically: 0.11 * (avgStart - avgEnd), clamped to [-start padding, end padding]
                p.Spec.OpticalContent = contentRow;
                p.Spec.OpticalA = opticalSign * 0.11f * full;
                p.Spec.OpticalB = -opticalSign * 0.11f;
                p.Spec.OpticalMin = -padStart;
                p.Spec.OpticalMax = padEnd;
            }
            p.Finish();
            return root;
        }

        /// <summary>Per-size split button values (SplitButtonDefaults / SplitButton*Tokens).</summary>
        public struct SplitSpec
        {
            public float Inner, InnerPressed, LeadingStart, LeadingEnd, TrailingStart, TrailingEnd, TrailingIcon;

            public static SplitSpec For(ButtonSize size)
            {
                switch (size)
                {
                    case ButtonSize.ExtraSmall:
                        return new SplitSpec { Inner = SplitButtonXSmallTokens.InnerCornerCornerSize, InnerPressed = SplitButtonXSmallTokens.InnerPressedCornerCornerSize,
                            LeadingStart = SplitButtonXSmallTokens.LeadingButtonLeadingSpace, LeadingEnd = SplitButtonXSmallTokens.LeadingButtonTrailingSpace,
                            TrailingStart = SplitButtonXSmallTokens.TrailingButtonLeadingSpace, TrailingEnd = SplitButtonXSmallTokens.TrailingButtonTrailingSpace,
                            TrailingIcon = SplitButtonXSmallTokens.TrailingIconSize };
                    case ButtonSize.Medium:
                        return new SplitSpec { Inner = SplitButtonMediumTokens.InnerCornerCornerSize, InnerPressed = SplitButtonMediumTokens.InnerPressedCornerCornerSize,
                            LeadingStart = SplitButtonMediumTokens.LeadingButtonLeadingSpace, LeadingEnd = SplitButtonMediumTokens.LeadingButtonTrailingSpace,
                            TrailingStart = SplitButtonMediumTokens.TrailingButtonLeadingSpace, TrailingEnd = SplitButtonMediumTokens.TrailingButtonTrailingSpace,
                            TrailingIcon = SplitButtonMediumTokens.TrailingIconSize };
                    case ButtonSize.Large:
                        return new SplitSpec { Inner = SplitButtonLargeTokens.InnerCornerCornerSize, InnerPressed = SplitButtonLargeTokens.InnerPressedCornerCornerSize,
                            LeadingStart = SplitButtonLargeTokens.LeadingButtonLeadingSpace, LeadingEnd = SplitButtonLargeTokens.LeadingButtonTrailingSpace,
                            TrailingStart = SplitButtonLargeTokens.TrailingButtonLeadingSpace, TrailingEnd = SplitButtonLargeTokens.TrailingButtonTrailingSpace,
                            TrailingIcon = SplitButtonLargeTokens.TrailingIconSize };
                    case ButtonSize.ExtraLarge:
                        return new SplitSpec { Inner = SplitButtonXLargeTokens.InnerCornerCornerSize, InnerPressed = SplitButtonXLargeTokens.InnerPressedCornerCornerSize,
                            LeadingStart = SplitButtonXLargeTokens.LeadingButtonLeadingSpace, LeadingEnd = SplitButtonXLargeTokens.LeadingButtonTrailingSpace,
                            TrailingStart = SplitButtonXLargeTokens.TrailingButtonLeadingSpace, TrailingEnd = SplitButtonXLargeTokens.TrailingButtonTrailingSpace,
                            TrailingIcon = SplitButtonXLargeTokens.TrailingIconSize };
                    default:
                        return new SplitSpec { Inner = SplitButtonSmallTokens.InnerCornerCornerSize, InnerPressed = SplitButtonSmallTokens.InnerPressedCornerCornerSize,
                            LeadingStart = SplitButtonSmallTokens.LeadingButtonLeadingSpace, LeadingEnd = SplitButtonSmallTokens.LeadingButtonTrailingSpace,
                            TrailingStart = SplitButtonSmallTokens.TrailingButtonLeadingSpace, TrailingEnd = SplitButtonSmallTokens.TrailingButtonTrailingSpace,
                            TrailingIcon = SplitButtonSmallTokens.TrailingIconSize };
                }
            }
        }
        /// <summary>ButtonDefaults.MinWidth.</summary>
        public const float ButtonDefaultsMinWidth = 58f;

    }
}
