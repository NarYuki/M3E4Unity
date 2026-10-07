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
            ExtendedFabSize size = ExtendedFabSize.Baseline, ColorRole container = ColorRole.PrimaryContainer, bool lowered = false)
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
            float minWidth = size == ExtendedFabSize.Baseline ? 80f : height;
            var root = M3Build.Rect($"ExtendedFAB ({size})", parent);
            var row = M3Build.Row(root, iconPad);
            row.padding = new RectOffset(Mathf.RoundToInt(padStart), Mathf.RoundToInt(padEnd), 0, 0);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minWidth;
            le.minHeight = le.preferredHeight = height;
            var p = new M3Pressable(root, ctx, Style(ctx, container, height, radius, lowered));
            var content = M3Colors.ContentFor(container);
            if (icon != null) p.Content(M3Build.Icon("Icon", root, icon, iconSize, ColorRef.Role(content), ctx));
            p.Content(M3Build.Text("Label", root, label, text, ColorRef.Role(content), ctx));
            p.Finish();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            root.sizeDelta = new Vector2(Mathf.Max(minWidth, LayoutUtility.GetPreferredWidth(root)), height);
            return root;
        }

        static float Radius(ShapeRole role)
        {
            var s = ShapeScale.Get(role);
            return s.TopStart;
        }
    }
}
