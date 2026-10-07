using M3E4Unity.Tokens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum CardStyle { Filled, Elevated, Outlined }

    /// <summary>Badges (Badge.kt), dividers (Divider.kt) and cards (Card.kt).</summary>
    public static class M3Containment
    {
        /// <summary>
        /// A badge attached to the top-end corner of an anchor (BadgedBox). text = null for the small dot.
        /// </summary>
        public static RectTransform Badge(RectTransform anchor, M3Context ctx, string text = null)
        {
            bool large = text != null;
            float size = large ? BadgeTokens.LargeSize : BadgeTokens.Size;
            var root = M3Build.Rect("Badge", anchor);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0f, 0f);
            M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(large ? BadgeTokens.LargeShape : BadgeTokens.Shape)),
                ColorRef.Role(large ? BadgeTokens.LargeColor : BadgeTokens.Color));
            float width = size;
            if (large)
            {
                var row = M3Build.Row(root, 0);
                // BadgeWithContentHorizontalPadding = 4 dp
                row.padding = new RectOffset(4, 4, 0, 0);
                M3Pressable.Ignore(root.Find("Container").GetComponent<Image>());
                var t = M3Build.Text("Label", root, text, BadgeTokens.LargeLabelTextFont, ColorRef.Role(BadgeTokens.LargeLabelTextColor), ctx);
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                width = Mathf.Max(size, LayoutUtility.GetPreferredWidth(root));
            }
            root.sizeDelta = new Vector2(width, size);
            // BadgedBox: x = anchor width - offset, y = -height + vertical offset (overlap)
            float hOffset = large ? 12f : 6f, vOffset = large ? 14f : 6f;
            root.anchoredPosition = new Vector2(-hOffset, -vOffset);
            return root;
        }

        public static RectTransform Divider(Transform parent, M3Context ctx, bool vertical = false, float length = 0f)
        {
            float t = DividerTokens.Thickness;
            var img = M3Build.Shape(vertical ? "VerticalDivider" : "HorizontalDivider", parent, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DividerTokens.Color), false);
            var rt = (RectTransform)img.transform;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (vertical)
            {
                rt.sizeDelta = new Vector2(t, length);
                le.minWidth = le.preferredWidth = t;
                if (length > 0) le.preferredHeight = length; else le.flexibleHeight = 1;
            }
            else
            {
                rt.sizeDelta = new Vector2(length, t);
                le.minHeight = le.preferredHeight = t;
                if (length > 0) le.preferredWidth = length; else le.flexibleWidth = 1;
            }
            return rt;
        }

        /// <summary>
        /// A card container (Card / ElevatedCard / OutlinedCard). Put content inside the returned
        /// "Content" child. clickable = the onClick overloads (state layer, ripple, elevation per state).
        /// </summary>
        public static RectTransform Card(Transform parent, M3Context ctx, CardStyle style, Vector2 size, bool clickable = false, bool enabled = true)
        {
            var root = M3Build.Rect($"Card ({style})", parent);
            M3Build.Size(root, size.x, size.y);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            ColorRole container;
            CornerShape shape;
            int[] elevation;
            switch (style)
            {
                case CardStyle.Elevated:
                    container = ElevatedCardTokens.ContainerColor; shape = ShapeScale.Get(ElevatedCardTokens.ContainerShape);
                    elevation = new[] { Lv(ElevatedCardTokens.ContainerElevation), Lv(ElevatedCardTokens.HoverContainerElevation), Lv(ElevatedCardTokens.PressedContainerElevation), Lv(ElevatedCardTokens.DisabledContainerElevation) };
                    break;
                case CardStyle.Outlined:
                    container = OutlinedCardTokens.ContainerColor; shape = ShapeScale.Get(OutlinedCardTokens.ContainerShape);
                    elevation = new[] { Lv(OutlinedCardTokens.ContainerElevation), Lv(OutlinedCardTokens.HoverContainerElevation), Lv(OutlinedCardTokens.PressedContainerElevation), Lv(OutlinedCardTokens.DisabledContainerElevation) };
                    break;
                default:
                    container = FilledCardTokens.ContainerColor; shape = ShapeScale.Get(FilledCardTokens.ContainerShape);
                    elevation = new[] { Lv(FilledCardTokens.ContainerElevation), Lv(FilledCardTokens.HoverContainerElevation), Lv(FilledCardTokens.PressedContainerElevation), Lv(FilledCardTokens.DisabledContainerElevation) };
                    break;
            }
            float r = shape.TopStart;
            var inner = ctx.On(container);
            if (clickable)
            {
                var p = new M3Pressable(root, ctx, new PressableStyle
                {
                    Container = container,
                    // CardDefaults: disabled container = DisabledContainerColor at DisabledContainerOpacity over the surface
                    DisabledContainer = style == CardStyle.Elevated ? ElevatedCardTokens.DisabledContainerColor : style == CardStyle.Filled ? FilledCardTokens.DisabledContainerColor : OutlinedCardTokens.ContainerColor,
                    DisabledContainerOpacity = style == CardStyle.Outlined ? 1f : ElevatedCardTokens.DisabledContainerOpacity,
                    Content = M3Colors.ContentFor(container),
                    Outline = style == CardStyle.Outlined ? OutlinedCardTokens.OutlineColor : (ColorRole?)null,
                    OutlineWidth = OutlinedCardTokens.OutlineWidth,
                    DisabledOutlineOpacity = OutlinedCardTokens.DisabledOutlineOpacity,
                    Elevation = elevation,
                    RestShape = r, PressedShape = r, CheckedShape = r, MaxShape = r,
                    Enabled = enabled,
                });
                p.Finish();
            }
            else
            {
                if (elevation[0] > 0)
                {
                    var shadows = M3Build.Shadows(root, shape, elevation[0], 0);
                    for (int i = 0; i < 2; i++) M3Build.SetShape(shadows[i], M3Build.ShadowCell(shape, elevation[0], i, out _));
                }
                M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(container));
                if (style == CardStyle.Outlined)
                    M3Build.Shape("Outline", root, ShapeCell.Create(ShapeKind.Stroke).WithShape(shape).WithStroke(OutlinedCardTokens.OutlineWidth), ColorRef.Role(OutlinedCardTokens.OutlineColor));
            }
            var content = M3Build.Rect("Content", root);
            M3Build.Fill(content);
            return root;
        }

        static int Lv(float dp) => Elevation.LevelOf(dp);
    }
}
