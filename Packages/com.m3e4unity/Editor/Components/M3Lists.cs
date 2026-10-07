using M3E4Unity.Tokens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Content of one list item (the slots of the expressive ListItem in ListItem.kt).</summary>
    public sealed class ListItemSpec
    {
        public string Headline;
        public string Overline;
        public string Supporting;
        /// <summary>Leading Material Symbol (Icon, default 24 dp).</summary>
        public string LeadingIcon;
        /// <summary>Leading avatar letter(s): a 40 dp PrimaryContainer circle (ItemLeadingAvatar* tokens).</summary>
        public string LeadingAvatar;
        public string TrailingIcon;
        /// <summary>Trailing supporting text (ItemTrailingSupportingText* tokens).</summary>
        public string TrailingText;
        public bool Selected;
        /// <summary>Clicking toggles the selection (otherwise the item is a plain clickable item).</summary>
        public bool Selectable;
        public bool Enabled = true;
        /// <summary>ListItemDefaults.colors(containerColor = …); null = the default ItemContainerColor.</summary>
        public ColorRole? Container;
    }

    /// <summary>
    /// Expressive list items (ListItem.kt InteractiveListItem, ListItemDefaults): the container
    /// morphs from CornerExtraSmall (rest) to CornerMedium (hovered) and CornerLarge (pressed /
    /// selected) with the FastSpatial spring. Segmented lists (SegmentedListItem) round the
    /// outer corners of the first and last items with ListTokens.ContainerShape and are
    /// separated by ListItemDefaults.SegmentedGap.
    /// </summary>
    public static class M3Lists
    {
        const float IconSize = 24f; // the slot takes the caller's Icon(), which defaults to 24 dp

        /// <summary>A column of list items; segmented = SegmentedListItem with segmentedShapes(index, count).</summary>
        public static RectTransform List(Transform parent, M3Context ctx, ListItemSpec[] items, float width, bool segmented = false)
        {
            var root = M3Build.Rect(segmented ? "SegmentedList" : "List", parent);
            var col = M3Build.Column(root, segmented ? ListTokens.SegmentedGap : 0f);
            col.childForceExpandWidth = true;
            for (int i = 0; i < items.Length; i++)
                Item(root, ctx, items[i], width, segmented, i, items.Length);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            float h = LayoutUtility.GetPreferredHeight(root);
            root.sizeDelta = new Vector2(width, h);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = h;
            return root;
        }

        public static RectTransform Item(Transform parent, M3Context ctx, ListItemSpec spec, float width,
            bool segmented = false, int index = 0, int count = 1)
        {
            float rest = ShapeScale.Get(ListTokens.ItemContainerExpressiveShape).TopStart;
            float hovered = ShapeScale.Get(ListTokens.ItemHoveredContainerExpressiveShape).TopStart;
            float pressed = ShapeScale.Get(ListTokens.ItemPressedContainerExpressiveShape).TopStart;
            float selected = ShapeScale.Get(ListTokens.ItemSelectedContainerExpressiveShape).TopStart;
            float outer = ShapeScale.Get(ListTokens.ContainerShape).TopStart;
            bool roundTop = segmented && index == 0;
            bool roundBottom = segmented && index == count - 1;

            ColorRole container = spec.Container ?? (segmented ? ListTokens.ItemSegmentedContainerColor : ListTokens.ItemContainerColor);
            var style = new PressableStyle
            {
                Container = container,
                ContainerChecked = ListTokens.ItemSelectedContainerColor,
                // disabledContainerColor = ItemContainerColor (selected or not)
                DisabledContainer = container,
                DisabledContainerOpacity = 1f,
                Content = ListTokens.ItemLabelTextColor,
                ContentChecked = ListTokens.ItemSelectedLabelTextColor,
                DisabledContent = ListTokens.ItemDisabledLabelTextColor,
                DisabledContentOpacity = ListTokens.ItemDisabledLabelTextOpacity,
                Toggle = spec.Selectable || spec.Selected,
                Selected = spec.Selected,
                Enabled = spec.Enabled,
                RestShape = rest,
                HoveredShape = hovered,
                PressedShape = pressed,
                CheckedShape = selected,
                CheckedPressedShape = pressed,
                MaxShape = Mathf.Max(pressed, selected),
                Spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial),
            };
            if (roundTop || roundBottom)
            {
                // segmentedShapes only replaces the rest shape's outer corners; the other states keep
                // their uniform shapes. The animated value is the inner radius (rest → hovered →
                // pressed); the outer corners follow rest(outer) → hovered → pressed linearly.
                style.ShapeFor = r =>
                {
                    float o = r <= hovered ? Mathf.Lerp(outer, hovered, Mathf.InverseLerp(rest, hovered, r)) : r;
                    return new CornerShape(roundTop ? o : r, roundTop ? o : r, roundBottom ? o : r, roundBottom ? o : r);
                };
            }

            var root = M3Build.Rect("ListItem", parent);
            var row = M3Build.Row(root, 0f, TextAnchor.MiddleLeft);
            row.padding = new RectOffset((int)ListTokens.ItemLeadingSpace, (int)ListTokens.ItemTrailingSpace,
                (int)ListTokens.ItemTopSpace, (int)ListTokens.ItemBottomSpace);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            var p = new M3Pressable(root, ctx, style);
            float between = ListTokens.ItemBetweenSpace;

            // LeadingDecorator: Box(padding(end = InteractiveListInternalSpacing))
            if (spec.LeadingIcon != null || spec.LeadingAvatar != null)
            {
                var slot = Slot(root, "Leading", 0, between);
                if (spec.LeadingAvatar != null)
                {
                    float size = ListTokens.ItemLeadingAvatarSize;
                    var avatar = M3Build.Rect("Avatar", slot);
                    M3Build.Size(avatar, size, size);
                    var ale = avatar.gameObject.AddComponent<LayoutElement>();
                    ale.minWidth = ale.preferredWidth = ale.minHeight = ale.preferredHeight = size;
                    var circle = M3Build.Shape("Container", avatar, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(ListTokens.ItemLeadingAvatarShape)),
                        ColorRef.Role(ListTokens.ItemLeadingAvatarColor));
                    circle.raycastTarget = false;
                    var letter = M3Build.Text("Label", avatar, spec.LeadingAvatar, ListTokens.ItemLeadingAvatarLabelFont,
                        ColorRef.Role(ListTokens.ItemLeadingAvatarLabelColor), ctx);
                    letter.alignment = TextAlignmentOptions.Center;
                    M3Build.Fill((RectTransform)letter.transform);
                }
                else
                {
                    var icon = M3Build.Icon("LeadingIcon", slot, spec.LeadingIcon, IconSize, ColorRef.Role(ListTokens.ItemLeadingIconColor), ctx);
                    p.Content(icon, ListTokens.ItemLeadingIconColor, ListTokens.ItemSelectedLeadingIconColor);
                }
            }

            var main = M3Build.Rect("Content", root);
            var mainCol = M3Build.Column(main, 0f, TextAnchor.MiddleLeft);
            mainCol.childForceExpandWidth = true;
            var mle = main.gameObject.AddComponent<LayoutElement>();
            mle.flexibleWidth = 1f;
            if (spec.Overline != null)
                p.Content(Line(main, "Overline", spec.Overline, ListTokens.ItemOverlineFont, ListTokens.ItemOverlineColor, ctx),
                    ListTokens.ItemOverlineColor, ListTokens.ItemSelectedOverlineColor);
            p.Content(Line(main, "Headline", spec.Headline ?? "", ListTokens.ItemLabelTextFont, ListTokens.ItemLabelTextColor, ctx),
                ListTokens.ItemLabelTextColor, ListTokens.ItemSelectedLabelTextColor);
            TextMeshProUGUI supporting = null;
            if (spec.Supporting != null)
            {
                supporting = Line(main, "Supporting", spec.Supporting, ListTokens.ItemSupportingTextFont, ListTokens.ItemSupportingTextColor, ctx);
                supporting.enableWordWrapping = true;
                p.Content(supporting, ListTokens.ItemSupportingTextColor, ListTokens.ItemSelectedSupportingTextColor);
            }

            // TrailingDecorator: Box(padding(start = InteractiveListInternalSpacing))
            if (spec.TrailingText != null)
            {
                var slot = Slot(root, "Trailing", between, 0);
                p.Content(Line(slot, "TrailingText", spec.TrailingText, ListTokens.ItemTrailingSupportingTextFont, ListTokens.ItemTrailingSupportingTextColor, ctx),
                    ListTokens.ItemTrailingSupportingTextColor, ListTokens.ItemSelectedTrailingSupportingTextColor);
            }
            else if (spec.TrailingIcon != null)
            {
                var slot = Slot(root, "Trailing", between, 0);
                var icon = M3Build.Icon("TrailingIcon", slot, spec.TrailingIcon, IconSize, ColorRef.Role(ListTokens.ItemTrailingIconColor), ctx);
                p.Content(icon, ListTokens.ItemTrailingIconColor, ListTokens.ItemSelectedTrailingIconColor);
            }

            p.Finish();

            // ListItemType: three-line = overline + supporting, or multi-line supporting text
            root.sizeDelta = new Vector2(width, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            bool multiline = false;
            if (supporting != null)
            {
                supporting.ForceMeshUpdate();
                multiline = supporting.textInfo.lineCount > 1;
            }
            bool threeLine = (spec.Overline != null && spec.Supporting != null) || multiline;
            bool twoLine = !threeLine && (spec.Overline != null || spec.Supporting != null);
            float minHeight = threeLine ? ListTokens.ItemThreeLineContainerHeight
                : twoLine ? ListTokens.ItemTwoLineContainerHeight : ListTokens.ItemOneLineContainerHeight;
            le.minHeight = minHeight;

            // verticalAlignment(): top-aligned once the space inside the padding reaches the breakpoint
            float breakpoint = (ListTokens.ItemThreeLineContainerHeight + ListTokens.ItemTwoLineContainerHeight) / 2f
                - ListTokens.ItemTopSpace - ListTokens.ItemBottomSpace;
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            float height = Mathf.Max(minHeight, LayoutUtility.GetPreferredHeight(root));
            bool top = height - ListTokens.ItemTopSpace - ListTokens.ItemBottomSpace >= breakpoint;
            var align = top ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            row.childAlignment = align;
            mainCol.childAlignment = align;
            le.preferredHeight = height;
            root.sizeDelta = new Vector2(width, height);
            return root;
        }

        static RectTransform Slot(RectTransform parent, string name, float padStart, float padEnd)
        {
            var slot = M3Build.Rect(name, parent);
            var h = M3Build.Row(slot, 0f);
            h.padding = new RectOffset((int)padStart, (int)padEnd, 0, 0);
            return slot;
        }

        static TextMeshProUGUI Line(RectTransform parent, string name, string text, TypeRole role, ColorRole color, M3Context ctx)
        {
            var t = M3Build.Text(name, parent, text, role, ColorRef.Role(color), ctx);
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            return t;
        }
    }
}
