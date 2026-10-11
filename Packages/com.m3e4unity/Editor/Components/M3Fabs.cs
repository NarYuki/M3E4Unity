using M3E4Unity.Udon;
using M3E4Unity.Tokens;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum FabSize { Small, Baseline, Medium, Large }
    public enum ExtendedFabSize { Baseline, Small, Medium, Large }

    /// <summary>FABs and extended FABs (FloatingActionButton.kt, FloatingActionButtonDefaults).</summary>
    public static class M3Fabs
    {
        /// <summary>FloatingActionButtonDefaults.elevation(): FabPrimaryContainerTokens; lowered = levels 1/2/1.</summary>
        static int[] Elevation(bool lowered) => lowered
            ? new[] { 1, 2, 1, 1 }
            : new[]
            {
                Tokens.Elevation.LevelOf(FabPrimaryContainerTokens.ContainerElevation),
                Tokens.Elevation.LevelOf(FabPrimaryContainerTokens.HoveredContainerElevation),
                Tokens.Elevation.LevelOf(FabPrimaryContainerTokens.PressedContainerElevation),
                Tokens.Elevation.LevelOf(FabPrimaryContainerTokens.ContainerElevation),
            };

        static PressableStyle Style(M3Context ctx, ColorRole container, float height, float radius, bool lowered, int[] elevation = null) => new PressableStyle
        {
            Container = container,
            // contentColorFor(containerColor)
            Content = M3Colors.ContentFor(container),
            Elevation = elevation ?? Elevation(lowered),
            Height = height,
            RestShape = radius,
            PressedShape = radius,
            CheckedShape = radius,
            MaxShape = radius,
            Spring = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects),
        };

        /// <param name="container">FloatingActionButtonDefaults.containerColor is PrimaryContainer; M3 also uses Secondary/TertiaryContainer, Primary, Secondary, Tertiary.</param>
        public static RectTransform Create(Transform parent, M3Context ctx, string icon, FabSize size = FabSize.Baseline,
            ColorRole container = ColorRole.PrimaryContainer, bool lowered = false, int[] elevation = null, float? sideOverride = null)
        {
            float side, iconSize, radius;
            switch (size)
            {
                case FabSize.Small: side = FabSmallTokens.ContainerWidth; iconSize = FabSmallTokens.IconSize; radius = Radius(FabSmallTokens.ContainerShape); break;
                // mediumShape = ShapeDefaults.LargeIncreased ("TODO: update to use token")
                case FabSize.Medium: side = FabMediumTokens.ContainerWidth; iconSize = FabMediumTokens.IconSize; radius = Radius(ShapeRole.CornerLargeIncreased); break;
                // LargeIconSize = 36.dp ("TODO: FabLargeTokens.IconSize is incorrect")
                case FabSize.Large: side = FabLargeTokens.ContainerWidth; iconSize = 36f; radius = Radius(FabLargeTokens.ContainerShape); break;
                default: side = FabBaselineTokens.ContainerWidth; iconSize = FabBaselineTokens.IconSize; radius = Radius(FabBaselineTokens.ContainerShape); break;
            }
            if (sideOverride.HasValue) side = sideOverride.Value; // fillMaxSize inside a sized parent (floating toolbar FAB)
            var root = M3Build.Rect($"FAB ({size})", parent);
            M3Build.Size(root, side, side);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = side;
            le.minHeight = le.preferredHeight = side;
            var p = new M3Pressable(root, ctx, Style(ctx, container, side, radius, lowered, elevation));
            var iconText = M3Build.Icon("Icon", root, icon, iconSize, ColorRef.Role(M3Colors.ContentFor(container)), ctx);
            var irt = (RectTransform)iconText.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            M3Pressable.Ignore(iconText);
            p.Content(iconText);
            p.Finish();
            return root;
        }

        public static RectTransform Extended(Transform parent, M3Context ctx, string label, string icon = null,
            ExtendedFabSize size = ExtendedFabSize.Baseline, ColorRole container = ColorRole.PrimaryContainer, bool lowered = false,
            bool expanded = true)
        {
            float height, padStart, padEnd, iconPad, iconSize, radius;
            TypeRole text;
            switch (size)
            {
                case ExtendedFabSize.Small:
                    height = ExtendedFabSmallTokens.ContainerHeight; padStart = ExtendedFabSmallTokens.LeadingSpace; padEnd = ExtendedFabSmallTokens.TrailingSpace;
                    iconPad = ExtendedFabSmallTokens.IconLabelSpace; iconSize = ExtendedFabSmallTokens.IconSize; radius = Radius(ExtendedFabSmallTokens.ContainerShape);
                    text = TypeRole.TitleMedium;
                    break;
                case ExtendedFabSize.Medium:
                    // MediumExtendedFabIconPadding = 12.dp ("ExtendedFabMediumTokens.IconLabelSpace is incorrect"); shape LargeIncreased
                    height = ExtendedFabMediumTokens.ContainerHeight; padStart = ExtendedFabMediumTokens.LeadingSpace; padEnd = ExtendedFabMediumTokens.TrailingSpace;
                    iconPad = 12f; iconSize = ExtendedFabMediumTokens.IconSize; radius = Radius(ShapeRole.CornerLargeIncreased);
                    text = TypeRole.TitleLarge;
                    break;
                case ExtendedFabSize.Large:
                    // LargeExtendedFabIconPadding = 16.dp ("ExtendedFabLargeTokens.IconLabelSpace is incorrect")
                    height = ExtendedFabLargeTokens.ContainerHeight; padStart = ExtendedFabLargeTokens.LeadingSpace; padEnd = ExtendedFabLargeTokens.TrailingSpace;
                    iconPad = 16f; iconSize = ExtendedFabLargeTokens.IconSize; radius = Radius(ExtendedFabLargeTokens.ContainerShape);
                    text = TypeRole.HeadlineSmall;
                    break;
                default:
                    // ExtendedFloatingActionButton: start 16 (with icon), end 20 (ExtendedFabTextPadding), icon padding 12, min width 80
                    height = ExtendedFabPrimaryTokens.ContainerHeight; padStart = icon != null ? 16f : 20f; padEnd = 20f;
                    iconPad = 12f; iconSize = ExtendedFabPrimaryTokens.IconSize; radius = Radius(ExtendedFabPrimaryTokens.ContainerShape);
                    text = ExtendedFabPrimaryTokens.LabelTextFont;
                    break;
            }
            bool baseline = size == ExtendedFabSize.Baseline;
            float minWidth = baseline ? 80f : height;
            var root = M3Build.Rect($"ExtendedFAB ({size})", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            var p = new M3Pressable(root, ctx, Style(ctx, container, height, radius, lowered));
            var content = M3Colors.ContentFor(container);

            // the content row is placed by M3ExtendedFab and clipped to the container while it collapses
            var clip = M3Build.Rect("Clip", root);
            M3Build.Fill(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            M3Pressable.Ignore(clip.gameObject.AddComponent<Image>());
            clip.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            clip.GetComponent<Image>().raycastTarget = false;
            var row = M3Build.Rect("Row", clip);
            row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
            row.pivot = new Vector2(0f, 0.5f);
            float iconW = icon != null ? iconSize : 0f;
            if (icon != null)
            {
                var ic = M3Build.Icon("Icon", row, icon, iconSize, ColorRef.Role(content), ctx);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(iconSize, iconSize);
                p.Content(ic);
            }
            // the label with the spacer before it (the AnimatedVisibility content)
            var slot = M3Build.Rect("LabelSlot", row);
            slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(iconW, 0f);
            if (baseline) slot.gameObject.AddComponent<RectMask2D>();
            var group = slot.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var t = M3Build.Text("Label", slot, label, text, ColorRef.Role(content), ctx);
            float spacer = icon != null ? iconPad : 0f;
            float textW = Mathf.Ceil(t.GetPreferredValues(label).x);
            var trt = t.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(spacer, 0f);
            trt.sizeDelta = new Vector2(textW, height);
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.enableWordWrapping = false;
            p.Content(t);
            float labelW = spacer + textW;
            slot.sizeDelta = new Vector2(labelW, height);
            row.sizeDelta = new Vector2(iconW + labelW, height);
            p.Finish();

            var fab = M3UdonBridge.Add<M3ExtendedFab>(root.gameObject);
            fab.expanded = expanded;
            fab.mode = baseline ? 1 : 0;
            fab.root = root;
            fab.rootLayout = le;
            fab.row = row;
            fab.labelGroup = group;
            fab.labelSlot = slot;
            fab.height = height;
            fab.minWidthExpanded = minWidth;
            // collapsed: FabBaselineTokens.ContainerWidth for the baseline FAB, minWidth = height for the sized ones
            fab.minWidthCollapsed = baseline ? FabBaselineTokens.ContainerWidth : height;
            fab.padStart = padStart;
            fab.padEnd = padEnd;
            fab.iconWidth = iconW;
            fab.labelWidth = labelW;
            fab.iconPadding = spacer;
            var fastSpatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            var defaultSpatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            float fastEffects = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            float defaultEffects = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
            fab.expandDamping = fastSpatial.DampingRatio; fab.expandStiffness = fastSpatial.Stiffness;
            var shrink = baseline ? defaultSpatial : fastSpatial;
            fab.shrinkDamping = shrink.DampingRatio; fab.shrinkStiffness = shrink.Stiffness;
            fab.fadeInStiffness = baseline ? defaultEffects : fastEffects;
            fab.fadeOutStiffness = fastEffects;
            M3UdonBridge.Sync(fab);

            // initial layout (as M3ExtendedFab.Apply)
            float width;
            float rowX;
            if (!baseline)
            {
                float full = Mathf.Max(height, padStart + iconW + labelW + padEnd);
                width = expanded ? full : height;
                rowX = padStart;
            }
            else
            {
                width = expanded ? Mathf.Max(minWidth, padStart + iconW + labelW + padEnd) : Mathf.Max(fab.minWidthCollapsed, iconW);
                rowX = expanded ? padStart : (width - iconW) / 2f;
                if (!expanded) slot.sizeDelta = new Vector2(0f, height);
            }
            group.alpha = expanded ? 1f : 0f;
            if (!expanded) slot.gameObject.SetActive(false);
            row.anchoredPosition = new Vector2(rowX, 0f);
            le.minWidth = le.preferredWidth = width;
            root.sizeDelta = new Vector2(width, height);
            return root;
        }

        static float Radius(ShapeRole role)
        {
            var s = ShapeScale.Get(role);
            return s.TopStart;
        }
    }
}
