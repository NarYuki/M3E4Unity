using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>One menu item (SelectableDropdownMenuItem in Menu.kt).</summary>
    public sealed class MenuItemSpec
    {
        public string Text;
        public string Supporting;
        public string LeadingIcon;
        /// <summary>Leading icon shown while selected (the samples use "check"); without LeadingIcon it appears only when selected.</summary>
        public string SelectedLeadingIcon;
        public string TrailingIcon;
        public bool Selected;
        public bool Enabled = true;
    }

    /// <summary>
    /// Expressive menus (Menu.kt DropdownMenuPopup / DropdownMenuGroup / SelectableDropdownMenuItem,
    /// MenuDefaults): groups of items, each group a SurfaceContainerLow (standard) or
    /// TertiaryContainer (vibrant) surface with level-2 shadow and position-dependent corners that
    /// morph to inactiveShape once hovered and left; items morph to ItemSelectedShape when selected.
    /// </summary>
    public static class M3Menus
    {
        const float ItemMinWidth = 112f;   // DropdownMenuItemDefaultMinWidth
        const float ItemMaxWidth = 280f;   // DropdownMenuItemDefaultMaxWidth
        const float ItemPadH = 12f;        // DropdownMenuItemHorizontalPadding
        const float ItemPadV = 12f;        // SelectableItemVerticalPadding
        const float ItemOuterPad = 4f;     // DropdownMenuSelectableItemPadding
        const float ItemOuterPadV = 2f;    // DropdownMenuSelectableItemWithSupportTexPadding (vertical)
        const float IconTextSpacing = 8f;  // dropdownMenuIconTextPadding
        const float GroupPadV = 2f;        // DropdownMenuGroupVerticalPadding

        /// <summary>
        /// A menu of groups (GroupedMenuSample). Returns the popup root; its M3Popup opens / closes it
        /// (scale 0.8 → 1 from the top-start corner).
        /// </summary>
        public static RectTransform Create(Transform parent, M3Context ctx, MenuItemSpec[][] groups, bool vibrant = false, bool expanded = true)
        {
            var root = M3Build.Rect("Menu", parent);
            var content = M3Build.Rect("MenuContent", root);
            var col = M3Build.Column(content, SegmentedMenuTokens.SegmentedGap); // MenuDefaults.GroupSpacing
            col.childForceExpandWidth = true;
            var groupCtx = ctx.On(vibrant ? VibrantMenuTokens.ContainerColor : StandardMenuTokens.ContainerColor);

            // width(IntrinsicSize.Max): every item is as wide as the widest one (112..280 dp)
            float width = ItemMinWidth;
            foreach (var g in groups)
                foreach (var it in g)
                    width = Mathf.Max(width, Mathf.Min(ItemMaxWidth, PreferredItemWidth(it, ctx)));

            for (int gi = 0; gi < groups.Length; gi++)
                Group(content, groupCtx, groups[gi], gi, groups.Length, width, vibrant);

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float h = LayoutUtility.GetPreferredHeight(content);
            float w = width + 2f * ItemOuterPad;
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f); // transform origin: top-start (MenuAnchorPosition.Below)
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(w, h);
            root.sizeDelta = new Vector2(w, h);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = h;

            var popup = M3UdonBridge.Add<M3Popup>(root.gameObject);
            popup.target = content;
            popup.group = content.gameObject.AddComponent<CanvasGroup>();
            popup.expanded = expanded;
            popup.closedScale = 0.8f; // ClosedScaleTarget
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            popup.spatialDamping = spatial.DampingRatio;
            popup.spatialStiffness = spatial.Stiffness;
            popup.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            if (!expanded) content.gameObject.SetActive(false);
            M3UdonBridge.Sync(popup);
            return root;
        }

        static void Group(RectTransform parent, M3Context ctx, MenuItemSpec[] items, int index, int count, float itemWidth, bool vibrant)
        {
            // MenuDefaults.groupShape(index, count): outer corners Large, inner corners Small; inactive = Small
            float large = ShapeScale.Get(SegmentedMenuTokens.ContainerShape).TopStart;
            float small = ShapeScale.Get(SegmentedMenuTokens.GroupShape).TopStart;
            float inactive = ShapeScale.Get(SegmentedMenuTokens.InactiveContainerShape).TopStart;
            bool roundTop = count == 1 || index == 0;
            bool roundBottom = count == 1 || index == count - 1;
            System.Func<float, CornerShape> shapeFor = v => new CornerShape(roundTop ? v : small, roundTop ? v : small, roundBottom ? v : small, roundBottom ? v : small);
            float rest = roundTop || roundBottom ? large : small;

            var root = M3Build.Rect("MenuGroup", parent);
            var col = M3Build.Column(root, 0f);
            col.padding = new RectOffset(0, 0, (int)GroupPadV, (int)GroupPadV);
            col.childForceExpandWidth = true;

            var spec = new InteractionSpec
            {
                Root = root,
                Context = ctx,
                RestShape = rest, PressedShape = rest, SelectedShape = rest, SelectedPressedShape = rest,
                InactiveShape = inactive,
                MaxShape = Mathf.Max(rest, inactive),
                Spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial),
            };
            // shadowElevation = MenuDefaults.ShadowElevation (MenuTokens.ContainerElevation)
            int level = Elevation.LevelOf(MenuTokens.ContainerElevation);
            var shadows = M3Build.Shadows(root, shapeFor(rest), level, 0);
            foreach (var s in shadows) M3Pressable.Ignore(s);
            spec.Shadows = shadows;
            spec.Elevation = new[] { level, level, level, level };
            spec.ShadowShape = shapeFor;
            var container = M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(rest)), ColorRef.Role(ctx.Background));
            M3Pressable.Ignore(container);
            spec.ShapeLayers.Add((container, r => ShapeCell.Create(ShapeKind.Fill).WithShape(shapeFor(r))));

            for (int i = 0; i < items.Length; i++)
                Item(root, ctx, items[i], i, items.Length, itemWidth, vibrant);
            M3Interaction.Apply(spec);
        }

        static void Item(RectTransform parent, M3Context ctx, MenuItemSpec s, int index, int count, float width, bool vibrant)
        {
            // MenuDefaults.itemShape(index, count): leading Medium/ExtraSmall, middle ExtraSmall, trailing ExtraSmall/Medium;
            // selected = ItemSelectedShape
            float medium = ShapeScale.Get(SegmentedMenuTokens.ItemFirstChildShape).TopStart;
            float rest = ShapeScale.Get(SegmentedMenuTokens.ItemShape).TopStart;
            float selected = ShapeScale.Get(SegmentedMenuTokens.ItemSelectedShape).TopStart;
            bool lead = count > 1 && index == 0;
            bool trail = count > 1 && index == count - 1;
            // the animated value is the inner corner radius; outer corners of the first / last item stay Medium
            System.Func<float, CornerShape> shapeFor = v => new CornerShape(lead ? medium : v, lead ? medium : v, trail ? medium : v, trail ? medium : v);

            ColorRole container = vibrant ? VibrantMenuTokens.ContainerColor : StandardMenuTokens.ContainerColor;
            var style = new PressableStyle
            {
                Container = container,
                ContainerChecked = vibrant ? VibrantMenuTokens.ItemSelectedContainerColor : StandardMenuTokens.ItemSelectedContainerColor,
                // disabledContainerColor = ContainerColor
                DisabledContainer = container,
                DisabledContainerOpacity = 1f,
                Content = vibrant ? VibrantMenuTokens.ItemLabelTextColor : StandardMenuTokens.ItemLabelTextColor,
                ContentChecked = vibrant ? VibrantMenuTokens.ItemSelectedLabelTextColor : StandardMenuTokens.ItemSelectedLabelTextColor,
                DisabledContent = vibrant ? VibrantMenuTokens.ItemDisabledLabelTextColor : StandardMenuTokens.ItemDisabledLabelTextColor,
                DisabledContentOpacity = StandardMenuTokens.ItemDisabledLabelTextOpacity,
                Toggle = true,
                Selected = s.Selected,
                Enabled = s.Enabled,
                RestShape = rest, PressedShape = rest, CheckedShape = selected, CheckedPressedShape = selected,
                MaxShape = Mathf.Max(rest, selected),
                Spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial),
                ShapeFor = shapeFor,
            };

            // Surface(modifier.fillMaxWidth().padding(horizontal 4 [, vertical 2])) around the item row
            var outer = M3Build.Rect("MenuItem", parent);
            var oc = M3Build.Column(outer, 0f);
            float padV = s.Supporting != null ? ItemOuterPadV : 0f;
            oc.padding = new RectOffset((int)ItemOuterPad, (int)ItemOuterPad, (int)padV, (int)padV);
            oc.childForceExpandWidth = true;

            var root = M3Build.Rect("Item", outer);
            var row = M3Build.Row(root, IconTextSpacing, TextAnchor.MiddleLeft);
            row.padding = new RectOffset((int)ItemPadH, (int)ItemPadH, (int)ItemPadV, (int)ItemPadV);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minHeight = SegmentedMenuTokens.Item;
            le.preferredWidth = width;
            var p = new M3Pressable(root, ctx, style);

            ColorRole leadColor = vibrant ? VibrantMenuTokens.ItemLeadingIconColor : StandardMenuTokens.ItemLeadingIconColor;
            ColorRole leadSel = vibrant ? VibrantMenuTokens.ItemSelectedLeadingIconColor : StandardMenuTokens.ItemSelectedLeadingIconColor;
            float iconSize = SegmentedMenuTokens.ItemLeadingIconSize;
            if (s.LeadingIcon != null)
            {
                var icon = M3Build.Icon("LeadingIcon", root, s.LeadingIcon, iconSize, ColorRef.Role(leadColor), ctx);
                p.Content(icon, leadColor, leadSel);
                if (s.SelectedLeadingIcon != null)
                {
                    var sel = M3Build.Icon("SelectedLeadingIcon", root, s.SelectedLeadingIcon, iconSize, ColorRef.Role(leadSel), ctx);
                    p.Content(sel, leadColor, leadSel);
                    p.Spec.ShowWhenSelected.Add(sel.gameObject);
                    p.Spec.HideWhenSelected.Add(icon.gameObject);
                }
            }
            else if (s.SelectedLeadingIcon != null)
            {
                // AnimatedVisibility(visible = selected)
                var sel = M3Build.Icon("SelectedLeadingIcon", root, s.SelectedLeadingIcon, iconSize, ColorRef.Role(leadSel), ctx);
                p.Content(sel, leadColor, leadSel);
                p.Spec.ShowWhenSelected.Add(sel.gameObject);
            }

            var textBox = M3Build.Rect("Text", root);
            var tc = M3Build.Column(textBox, 0f, TextAnchor.MiddleLeft);
            tc.childForceExpandWidth = true;
            textBox.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var label = M3Build.Text("Label", textBox, s.Text, TypeRole.LabelLarge, ColorRef.Role(style.Content), ctx);
            label.alignment = TMPro.TextAlignmentOptions.Left;
            p.Content(label);
            if (s.Supporting != null)
            {
                ColorRole sup = vibrant ? VibrantMenuTokens.ItemSupportingTextColor : StandardMenuTokens.ItemSupportingTextColor;
                ColorRole supSel = vibrant ? VibrantMenuTokens.ItemSelectedSupportingTextColor : StandardMenuTokens.ItemSelectedSupportingTextColor;
                var st = M3Build.Text("Supporting", textBox, s.Supporting, TypeRole.BodyMedium, ColorRef.Role(sup), ctx);
                st.alignment = TMPro.TextAlignmentOptions.Left;
                p.Content(st, sup, supSel);
            }

            if (s.TrailingIcon != null)
            {
                ColorRole tr = vibrant ? VibrantMenuTokens.ItemTrailingIconColor : StandardMenuTokens.ItemTrailingIconColor;
                ColorRole trSel = vibrant ? VibrantMenuTokens.ItemSelectedTrailingIconColor : StandardMenuTokens.ItemSelectedTrailingIconColor;
                var icon = M3Build.Icon("TrailingIcon", root, s.TrailingIcon, SegmentedMenuTokens.ItemTrailingIconSize, ColorRef.Role(tr), ctx);
                p.Content(icon, tr, trSel);
            }
            p.Finish();
        }

        static float PreferredItemWidth(MenuItemSpec s, M3Context ctx)
        {
            float w = 2f * ItemPadH;
            if (s.LeadingIcon != null || s.SelectedLeadingIcon != null) w += SegmentedMenuTokens.ItemLeadingIconSize + IconTextSpacing;
            if (s.TrailingIcon != null) w += IconTextSpacing + SegmentedMenuTokens.ItemTrailingIconSize;
            w += Mathf.Max(TextWidth(s.Text, TypeRole.LabelLarge, ctx), s.Supporting != null ? TextWidth(s.Supporting, TypeRole.BodyMedium, ctx) : 0f);
            return Mathf.Ceil(w);
        }

        static float TextWidth(string text, TypeRole role, M3Context ctx)
        {
            var go = new GameObject("Measure", typeof(RectTransform));
            var t = go.AddComponent<TMPro.TextMeshProUGUI>();
            M3Fonts.ApplyStyle(t, role, ctx.Theme);
            float w = t.GetPreferredValues(text).x;
            Object.DestroyImmediate(go);
            return w;
        }
    }
}
