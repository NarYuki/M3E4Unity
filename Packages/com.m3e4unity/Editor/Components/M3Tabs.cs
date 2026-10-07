using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum TabStyle { Primary, Secondary }

    public sealed class TabSpec
    {
        public string Text;
        public string Icon;
        /// <summary>LeadingIconTab: icon before the text in a 48 dp row.</summary>
        public bool LeadingIcon;
        public bool Enabled = true;
    }

    /// <summary>
    /// Fixed primary / secondary tab rows (TabRow.kt PrimaryTabRow / SecondaryTabRow, Tab.kt Tab /
    /// LeadingIconTab with TabBaselineLayout). Colors follow PrimaryNavigationTabTokens /
    /// SecondaryNavigationTabTokens (active / inactive label and icon).
    /// </summary>
    public static class M3Tabs
    {
        const float SmallTabHeight = 48f;              // PrimaryNavigationTabTokens.ContainerHeight
        const float LargeTabHeight = 72f;
        const float HorizontalTextPadding = 16f;
        const float SingleLineTextBaselineWithIcon = 14f;
        const float IconDistanceFromBaseline = 20f;
        const float TextDistanceFromLeadingIcon = 8f;
        const float DisabledAlpha = 0.38f;

        public static RectTransform Create(Transform parent, M3Context ctx, TabStyle style, TabSpec[] tabs, int selected, float width)
        {
            bool primary = style == TabStyle.Primary;
            ColorRole bg = primary ? PrimaryNavigationTabTokens.ContainerColor : SecondaryNavigationTabTokens.ContainerColor;
            var inner = ctx.On(bg);
            ColorRole activeText = primary ? PrimaryNavigationTabTokens.ActiveLabelTextColor : SecondaryNavigationTabTokens.ActiveLabelTextColor;
            ColorRole inactiveText = primary ? PrimaryNavigationTabTokens.InactiveLabelTextColor : SecondaryNavigationTabTokens.InactiveLabelTextColor;
            ColorRole activeIcon = primary ? PrimaryNavigationTabTokens.ActiveIconColor : SecondaryNavigationTabTokens.ActiveIconColor;
            ColorRole inactiveIcon = primary ? PrimaryNavigationTabTokens.InactiveIconColor : SecondaryNavigationTabTokens.InactiveIconColor;
            TypeRole font = primary ? PrimaryNavigationTabTokens.LabelTextFont : SecondaryNavigationTabTokens.LabelTextFont;
            float iconSize = PrimaryNavigationTabTokens.IconSize;

            int n = tabs.Length;
            float tabWidth = Mathf.Floor(width / n);
            bool anyLarge = false;
            foreach (var t in tabs) if (t.Text != null && t.Icon != null && !t.LeadingIcon) anyLarge = true;
            float height = anyLarge ? LargeTabHeight : SmallTabHeight;

            var root = M3Build.Rect(primary ? "PrimaryTabRow" : "SecondaryTabRow", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;
            M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(bg));

            var row = M3UdonBridge.Add<M3TabRow>(root.gameObject);
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            row.springDamping = spatial.DampingRatio;
            row.springStiffness = spatial.Stiffness;
            row.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
            row.fastEffectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            row.selectedIndex = selected;
            row.tabWidth = tabWidth;

            var selectables = new List<Selectable>();
            var widths = new List<float>();
            var entries = new List<SelectionColorEntry>();
            var coloredItem = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var s = tabs[i];
                var tab = M3Build.Rect("Tab", root);
                tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
                tab.pivot = new Vector2(0f, 1f);
                tab.anchoredPosition = new Vector2(i * tabWidth, 0f);
                tab.sizeDelta = new Vector2(tabWidth, height);

                // ripple(bounded, color = selectedContentColor) over the whole tab
                var stateLayer = M3Build.Shape("StateLayer", tab, ShapeCell.Create(ShapeKind.Fill), ColorRef.Clear);
                var ripple = M3Build.Shape("Ripple", tab, ShapeCell.Create(ShapeKind.Ripple), ColorRef.Clear);
                var spec = new InteractionSpec
                {
                    Root = tab, Context = inner, Enabled = s.Enabled, StateLayer = stateLayer, Ripple = ripple,
                    RippleClipRadius = 0f,
                };
                spec.ShapeLayers.Add((stateLayer, _ => ShapeCell.Create(ShapeKind.Fill)));
                spec.Color(stateLayer, ColorRef.Over(bg, activeText, StateLayer.Hover), ColorRef.Over(bg, activeText, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                spec.RippleColor = spec.RippleColorSelected = ColorRef.Over(bg, activeText, StateLayer.Hover, activeText, StateLayer.Pressed);

                TextMeshProUGUI text = null;
                float textW = 0f, textH = 0f, firstBase = 0f, lastBase = 0f;
                if (s.Text != null)
                {
                    text = M3Build.Text("Text", tab, s.Text, font, ColorRef.Role(inactiveText), inner);
                    text.alignment = TextAlignmentOptions.Top;
                    text.enableWordWrapping = true;
                    text.raycastTarget = false;
                    float maxW = tabWidth - 2f * HorizontalTextPadding;
                    textW = Mathf.Min(Mathf.Ceil(text.GetPreferredValues(s.Text).x), maxW);
                    (textH, firstBase, lastBase) = M3Build.MeasureText(text, s.Text, textW);
                    entries.Add(Entry(text, ColorRef.Role(inactiveText), ColorRef.Role(activeText), ColorRef.Over(bg, inactiveText, DisabledAlpha), ColorRef.Over(bg, activeText, DisabledAlpha)));
                    coloredItem.Add(i);
                }
                TextMeshProUGUI icon = null;
                if (s.Icon != null)
                {
                    icon = M3Build.Icon("Icon", tab, s.Icon, iconSize, ColorRef.Role(inactiveIcon), inner);
                    entries.Add(Entry(icon, ColorRef.Role(inactiveIcon), ColorRef.Role(activeIcon), ColorRef.Over(bg, inactiveIcon, DisabledAlpha), ColorRef.Over(bg, activeIcon, DisabledAlpha)));
                    coloredItem.Add(i);
                }

                float contentW;
                if (text != null && icon != null && s.LeadingIcon)
                {
                    // LeadingIconTab: Row(Center) { icon, 8 dp, text } in 48 dp, padding 16
                    float rowW = iconSize + TextDistanceFromLeadingIcon + textW;
                    float x0 = (tabWidth - rowW) / 2f;
                    TopLeft(icon.rectTransform, x0, (height - iconSize) / 2f, iconSize, iconSize);
                    TopLeft(text.rectTransform, x0 + iconSize + TextDistanceFromLeadingIcon, (height - textH) / 2f, textW, textH);
                    contentW = rowW;
                }
                else if (text != null && icon != null)
                {
                    // placeTextAndIcon: last baseline 14 dp above the indicator, icon 20 sp above the first baseline
                    float textY = height - lastBase - (SingleLineTextBaselineWithIcon + PrimaryNavigationTabTokens.ActiveIndicatorHeight);
                    float iconY = textY - (iconSize + IconDistanceFromBaseline - firstBase);
                    TopLeft(text.rectTransform, (tabWidth - textW) / 2f, textY, textW, textH);
                    TopLeft(icon.rectTransform, (tabWidth - iconSize) / 2f, iconY, iconSize, iconSize);
                    contentW = Mathf.Max(textW, iconSize);
                }
                else if (text != null)
                {
                    TopLeft(text.rectTransform, (tabWidth - textW) / 2f, (height - textH) / 2f, textW, textH);
                    contentW = textW;
                }
                else
                {
                    TopLeft(icon.rectTransform, (tabWidth - iconSize) / 2f, (height - iconSize) / 2f, iconSize, iconSize);
                    contentW = iconSize;
                }

                var it = M3Interaction.Apply(spec);
                var button = tab.GetComponent<Button>();
                M3UdonBridge.Wire(button.onClick, row, "_Select" + i);
                selectables.Add(button);
                // TabPosition.contentWidth = min(intrinsic, tabWidth) - 2 * HorizontalTextPadding, at least 24 dp
                // (the intrinsic width of a text tab includes its 16 dp paddings)
                float intrinsic = s.LeadingIcon || text == null ? contentW + 2f * HorizontalTextPadding : textW + 2f * HorizontalTextPadding;
                widths.Add(primary ? Mathf.Max(Mathf.Min(intrinsic, tabWidth) - 2f * HorizontalTextPadding, 24f) : tabWidth);
            }

            // divider (HorizontalDivider) then the indicator on top of it
            var divider = M3Build.Shape("Divider", root, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DividerTokens.Color), false);
            var drt = (RectTransform)divider.transform;
            drt.anchorMin = new Vector2(0f, 0f); drt.anchorMax = new Vector2(1f, 0f); drt.pivot = new Vector2(0.5f, 0f);
            drt.anchoredPosition = Vector2.zero; drt.sizeDelta = new Vector2(0f, DividerTokens.Thickness);
            divider.raycastTarget = false;
            var indicatorCell = primary ? ShapeCell.Create(ShapeKind.Fill).WithShape(PrimaryNavigationTabTokens.ActiveIndicatorShape) : ShapeCell.Create(ShapeKind.Fill);
            var ind = M3Build.Shape("Indicator", root, indicatorCell, ColorRef.Role(PrimaryNavigationTabTokens.ActiveIndicatorColor), false);
            ind.raycastTarget = false;
            var irt = (RectTransform)ind.transform;
            irt.sizeDelta = new Vector2(widths[selected], PrimaryNavigationTabTokens.ActiveIndicatorHeight);
            row.indicator = irt;
            row.indicatorWidths = widths.ToArray();
            row.tabs = selectables.ToArray();
            row.coloredItem = coloredItem.ToArray();
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            irt.anchorMin = irt.anchorMax = Vector2.zero;
            irt.pivot = Vector2.zero;
            irt.anchoredPosition = new Vector2(Mathf.Round(selected * tabWidth + (tabWidth - widths[selected]) / 2f), 0f);
            M3UdonBridge.Sync(row);
            return root;
        }

        static SelectionColorEntry Entry(Graphic g, ColorRef off, ColorRef on, ColorRef dOff, ColorRef dOn) =>
            new SelectionColorEntry { graphic = g, off = off, on = on, indeterminate = off, disabledOff = dOff, disabledOn = dOn, disabledIndeterminate = dOff };

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
