using M3E4Unity.Tokens;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Expressive toolbars: the horizontal floating toolbar (FloatingToolbar.kt, standard / vibrant
    /// colors, optionally with a FAB) and the docked toolbar (FlexibleBottomAppBar in AppBar.kt).
    /// Toolbar actions are IconButtons tinted with the toolbar content color.
    /// </summary>
    public static class M3Toolbars
    {
        const float IconButtonSlot = 48f;   // IconButton with minimumInteractiveComponentSize
        const float ToolbarToFabGap = 8f;   // FloatingToolbarDefaults.ToolbarToFabGap

        public static RectTransform Floating(Transform parent, M3Context ctx, string[] icons, bool vibrant = false, string fabIcon = null)
        {
            // FloatingToolbarDefaults.standard/vibrantFloatingToolbarColors
            ColorRole container = vibrant ? FloatingToolbarTokens.VibrantContainerColor : FloatingToolbarTokens.StandardContainerColor;
            ColorRole content = M3Colors.ContentFor(container);
            ColorRole fabContainer = vibrant ? ColorRole.TertiaryContainer : ColorRole.PrimaryContainer;
            var inner = ctx.On(container);
            float h = FloatingToolbarTokens.ContainerHeight;
            float padStart = FloatingToolbarTokens.ContainerLeadingSpace, padEnd = FloatingToolbarTokens.ContainerTrailingSpace;
            float toolbarW = padStart + icons.Length * IconButtonSlot + padEnd;
            float fabSide = FabBaselineTokens.ContainerWidth; // FabSizeRange.start when expanded
            float totalW = toolbarW + (fabIcon != null ? ToolbarToFabGap + fabSide : 0f);
            float totalH = fabIcon != null ? Mathf.Max(h, FabMediumTokens.ContainerWidth) : h; // defaultMinSize(FabSizeRange.endInclusive)

            var root = M3Build.Rect(vibrant ? "FloatingToolbar (vibrant)" : "FloatingToolbar", parent);
            root.sizeDelta = new Vector2(totalW, totalH);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = totalW;
            le.preferredHeight = le.minHeight = totalH;

            var bar = M3Build.Rect("Toolbar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(toolbarW, h);
            // shadowElevation: ContainerExpandedElevation (Level0), or ContainerExpandedElevationWithFab (Level1)
            int level = fabIcon != null ? 1 : 0;
            if (level > 0)
            {
                var shadows = M3Build.Shadows(bar, CornerShape.Full, level, 0);
                for (int i = 0; i < shadows.Length; i++) M3Build.SetShape(shadows[i], M3Build.ShadowCell(CornerShape.Full, level, i, out _));
            }
            M3Build.Shape("Container", bar, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(FloatingToolbarTokens.ContainerShape)), ColorRef.Role(container));
            for (int i = 0; i < icons.Length; i++)
            {
                var b = M3IconButtons.Create(bar, inner, icons[i], contentColor: content);
                Center(b, padStart + i * IconButtonSlot + IconButtonSlot / 2f);
            }

            if (fabIcon != null)
            {
                // FloatingToolbarDefaults.VibrantFloatingActionButton / StandardFloatingActionButton: Level2 (hover Level3)
                var fab = M3Fabs.Create(root, ctx, fabIcon, FabSize.Baseline, fabContainer, elevation: new[] { 2, 3, 2, 2 }, sideOverride: fabSide);
                var fle = fab.GetComponent<LayoutElement>(); fle.ignoreLayout = true;
                fab.anchorMin = fab.anchorMax = new Vector2(0f, 0.5f);
                fab.pivot = new Vector2(0f, 0.5f);
                fab.anchoredPosition = new Vector2(toolbarW + ToolbarToFabGap, 0f);
            }
            return root;
        }

        /// <summary>FlexibleBottomAppBar: 64 dp, SurfaceContainer, 16 dp start / end, Arrangement.SpaceBetween.</summary>
        public static RectTransform Docked(Transform parent, M3Context ctx, string[] icons, float width)
        {
            ColorRole container = DockedToolbarTokens.ContainerColor;
            var inner = ctx.On(container);
            float h = DockedToolbarTokens.ContainerHeight;
            var root = M3Build.Rect("DockedToolbar", parent);
            root.sizeDelta = new Vector2(width, h);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = h;
            M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(DockedToolbarTokens.ContainerShape)), ColorRef.Role(container));
            float start = DockedToolbarTokens.ContainerLeadingSpace;
            float inner0 = width - start - DockedToolbarTokens.ContainerTrailingSpace;
            int n = icons.Length;
            float gap = n > 1 ? (inner0 - n * IconButtonSlot) / (n - 1) : 0f;
            for (int i = 0; i < n; i++)
            {
                var b = M3IconButtons.Create(root, inner, icons[i], contentColor: M3Colors.ContentFor(container));
                float x = n > 1 ? start + i * (IconButtonSlot + gap) : start + (inner0 - IconButtonSlot) / 2f;
                Center(b, x + IconButtonSlot / 2f);
            }
            return root;
        }

        static void Center(RectTransform rt, float cx)
        {
            var l = rt.GetComponent<LayoutElement>();
            if (l != null) l.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, 0f);
        }
    }
}
