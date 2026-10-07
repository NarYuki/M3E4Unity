using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum NavigationItemIconPosition { Top, Start }
    public enum ShortNavigationBarArrangement { EqualWeight, Centered }

    public sealed class NavigationItemSpec
    {
        public string Icon;
        /// <summary>Icon while selected; null = the filled variant of Icon (the samples swap Outlined / Filled).</summary>
        public string SelectedIcon;
        public string Label;
        public bool Enabled = true;
    }

    /// <summary>
    /// The expressive short navigation bar (ShortNavigationBar.kt + NavigationItem.kt): items with a
    /// pill indicator that grows from the center (DefaultSpatial) and a ripple confined to the pill.
    /// </summary>
    public static class M3NavigationBars
    {
        const float IndicatorToLabel = 4f;   // TopIconIndicatorToLabelPadding
        const float DisabledAlpha = 0.38f;

        public static RectTransform Create(Transform parent, M3Context ctx, NavigationItemSpec[] items, int selected, float width,
            NavigationItemIconPosition iconPosition = NavigationItemIconPosition.Top,
            ShortNavigationBarArrangement arrangement = ShortNavigationBarArrangement.EqualWeight)
        {
            ColorRole barColor = NavigationBarTokens.ContainerColor;
            var inner = ctx.On(barColor);
            float barHeight = NavigationBarTokens.ContainerHeight;
            bool top = iconPosition == NavigationItemIconPosition.Top;

            var root = M3Build.Rect("ShortNavigationBar", parent);
            M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(barColor));
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;

            // item geometry
            int n = items.Length;
            float iconSize = NavigationBarVerticalItemTokens.IconSize;
            float indH = top ? NavigationBarVerticalItemTokens.ActiveIndicatorHeight : NavigationBarHorizontalItemTokens.ActiveIndicatorHeight;
            float padH = top ? (NavigationBarVerticalItemTokens.ActiveIndicatorWidth - iconSize) / 2f : NavigationBarHorizontalItemTokens.ActiveIndicatorLeadingSpace;
            float iconToLabel = NavigationBarTokens.ItemActiveIndicatorIconLabelSpace; // StartIconToLabelPadding

            var labelWidths = new float[n];
            float labelHeight = 0f;
            for (int i = 0; i < n; i++)
            {
                if (items[i].Label == null) continue;
                var probe = M3Build.Text("Probe", root, items[i].Label, NavigationBarTokens.LabelTextFont, ColorRef.Clear, inner);
                labelWidths[i] = Mathf.Ceil(probe.GetPreferredValues(items[i].Label).x);
                labelHeight = Mathf.Max(labelHeight, Mathf.Ceil(probe.GetPreferredValues(items[i].Label).y));
                Object.DestroyImmediate(probe.gameObject);
            }
            float itemContentH = top ? NavigationBarVerticalItemTokens.ContainerBetweenSpace * 2f + indH + IndicatorToLabel + labelHeight : indH;
            float height = Mathf.Max(barHeight, itemContentH);
            root.sizeDelta = new Vector2(width, height);
            le.preferredHeight = le.minHeight = height;

            // arrangement (EqualWeight / CenteredContentMeasurePolicy)
            var widths = new float[n];
            float x = 0f;
            if (arrangement == ShortNavigationBarArrangement.EqualWeight)
            {
                for (int i = 0; i < n; i++) widths[i] = Mathf.Floor(width / n);
            }
            else
            {
                float maxW = Mathf.Floor(width / n);
                float pad = n > 6 ? 0f : Mathf.Round((100f - 10f * (n + 3)) / 2f / 100f * width);
                float minW = Mathf.Floor((width - pad * 2f) / n);
                for (int i = 0; i < n; i++)
                {
                    float intrinsic = top ? Mathf.Max(labelWidths[i], iconSize + 2f * padH)
                        : iconSize + labelWidths[i] + iconToLabel + 2f * padH;
                    float w = minW;
                    if (w < intrinsic) { w = Mathf.Min(intrinsic, maxW); pad -= (w - minW) / 2f; }
                    widths[i] = w;
                }
                x = pad;
            }

            var group = M3UdonBridge.Add<M3NavigationGroup>(root.gameObject);
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            group.springDamping = spatial.DampingRatio;
            group.springStiffness = spatial.Stiffness;
            group.selectedIndex = selected;
            var selectables = new List<Selectable>();
            var indicators = new List<RectTransform>();
            var indicatorWidths = new List<float>();
            var indicatorGraphics = new List<Graphic>();
            var show = new List<GameObject>(); var showItem = new List<int>();
            var hide = new List<GameObject>(); var hideItem = new List<int>();
            var entries = new List<SelectionColorEntry>();
            var coloredItem = new List<int>();
            var interactives = new List<M3Interactive>();

            for (int i = 0; i < n; i++)
            {
                var s = items[i];
                var item = M3Build.Rect("NavigationItem", root);
                item.anchorMin = item.anchorMax = new Vector2(0f, 0.5f);
                item.pivot = new Vector2(0f, 0.5f);
                item.anchoredPosition = new Vector2(x, 0f);
                item.sizeDelta = new Vector2(widths[i], height);
                x += widths[i];

                // indicator geometry inside the item (y measured from the item top)
                float indW = top ? NavigationBarVerticalItemTokens.ActiveIndicatorWidth : iconSize + iconToLabel + labelWidths[i] + 2f * padH;
                float indY = top ? (height - itemContentH) / 2f + NavigationBarVerticalItemTokens.ContainerBetweenSpace : (height - indH) / 2f;
                float indCenterY = indY + indH / 2f;
                var pill = ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full);

                // ripple / state layer: the indicator pill only (MappedInteractionSource)
                var stateLayer = M3Build.Shape("StateLayer", item, pill, ColorRef.Clear, false);
                Pill((RectTransform)stateLayer.transform, indW, indH, indCenterY);
                var indicator = M3Build.Shape("Indicator", item, pill, ColorRef.Role(NavigationBarTokens.ItemActiveIndicatorColor), false);
                indicator.raycastTarget = false;
                Pill((RectTransform)indicator.transform, indW, indH, indCenterY);
                indicator.transform.SetAsFirstSibling();
                var ripple = M3Build.Shape("Ripple", item, ShapeCell.Create(ShapeKind.Ripple).WithShape(CornerShape.Full), ColorRef.Clear, false);
                Pill((RectTransform)ripple.transform, indW, indH, indCenterY);
                indicators.Add((RectTransform)indicator.transform);
                indicatorWidths.Add(indW);
                indicatorGraphics.Add(indicator);
                var indColor = ColorRef.Role(NavigationBarTokens.ItemActiveIndicatorColor);
                entries.Add(Entry(indicator, indColor, indColor, indColor, indColor)); coloredItem.Add(i);

                // icon (outlined) and selected icon (filled)
                float iconX = top ? (widths[i] - iconSize) / 2f : (widths[i] - (iconSize + iconToLabel + labelWidths[i])) / 2f;
                float iconY = indY + (indH - iconSize) / 2f;
                var iconOff = M3Build.Icon("Icon", item, s.Icon, iconSize, ColorRef.Role(NavigationBarTokens.ItemInactiveIconColor), inner);
                var iconOn = M3Build.Icon("SelectedIcon", item, s.SelectedIcon ?? s.Icon, iconSize, ColorRef.Role(NavigationBarTokens.ItemActiveIconColor), inner, filled: s.SelectedIcon == null);
                foreach (var ic in new[] { iconOff, iconOn }) TopLeft(ic.rectTransform, iconX, iconY, iconSize, iconSize);
                show.Add(iconOn.gameObject); showItem.Add(i);
                hide.Add(iconOff.gameObject); hideItem.Add(i);
                var iconDis = ColorRef.Over(barColor, NavigationBarTokens.ItemInactiveIconColor, DisabledAlpha);
                foreach (var ic in new[] { iconOff, iconOn })
                {
                    entries.Add(Entry(ic, ColorRef.Role(NavigationBarTokens.ItemInactiveIconColor), ColorRef.Role(NavigationBarTokens.ItemActiveIconColor), iconDis, iconDis));
                    coloredItem.Add(i);
                }

                if (s.Label != null)
                {
                    var label = M3Build.Text("Label", item, s.Label, NavigationBarTokens.LabelTextFont, ColorRef.Role(NavigationBarTokens.ItemInactiveLabelTextColor), inner);
                    label.raycastTarget = false;
                    if (top)
                    {
                        label.alignment = TextAlignmentOptions.Top;
                        TopLeft(label.rectTransform, (widths[i] - labelWidths[i]) / 2f, indY + indH + IndicatorToLabel, labelWidths[i], labelHeight);
                    }
                    else
                    {
                        label.alignment = TextAlignmentOptions.Left;
                        TopLeft(label.rectTransform, iconX + iconSize + iconToLabel, (height - labelHeight) / 2f, labelWidths[i], labelHeight);
                    }
                    // selectedTextColorTopIconPosition = ItemActiveLabelTextColor; start icon: ItemActiveIconColor (TODO in Compose)
                    var on = top ? NavigationBarTokens.ItemActiveLabelTextColor : NavigationBarTokens.ItemActiveIconColor;
                    var dis = ColorRef.Over(barColor, NavigationBarTokens.ItemInactiveLabelTextColor, DisabledAlpha);
                    entries.Add(Entry(label, ColorRef.Role(NavigationBarTokens.ItemInactiveLabelTextColor), ColorRef.Role(on), dis, dis));
                    coloredItem.Add(i);
                }

                // interaction: the whole item is selectable, state layer / ripple on the pill
                var spec = new InteractionSpec
                {
                    Root = item,
                    Context = inner,
                    Enabled = s.Enabled,
                    Selected = i == selected,
                    RestShape = indH / 2f, PressedShape = indH / 2f, SelectedShape = indH / 2f, SelectedPressedShape = indH / 2f,
                    MaxShape = indH / 2f,
                    StateLayer = stateLayer,
                    Ripple = ripple,
                    RippleClipShape = CornerShape.Full,
                };
                spec.ShapeLayers.Add((stateLayer, r => pill));
                // the ripple color is the bar's LocalContentColor (contentColorFor(containerColor))
                var content = M3Colors.ContentFor(barColor);
                spec.Color(stateLayer, ColorRef.Over(barColor, content, StateLayer.Hover),
                    ColorRef.Over(NavigationBarTokens.ItemActiveIndicatorColor, content, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                spec.RippleColor = ColorRef.Over(barColor, content, StateLayer.Hover, content, StateLayer.Pressed);
                spec.RippleColorSelected = ColorRef.Over(NavigationBarTokens.ItemActiveIndicatorColor, content, StateLayer.Hover, content, StateLayer.Pressed);
                var it = M3Interaction.Apply(spec);
                interactives.Add(it);
                var button = item.GetComponent<Button>();
                selectables.Add(button);
                M3UdonBridge.Wire(button.onClick, group, "_Select" + i);
            }

            group.items = selectables.ToArray();
            group.indicators = indicators.ToArray();
            group.indicatorWidths = indicatorWidths.ToArray();
            group.indicatorGraphics = indicatorGraphics.ToArray();
            group.showWhenSelected = show.ToArray(); group.showItem = showItem.ToArray();
            group.hideWhenSelected = hide.ToArray(); group.hideItem = hideItem.ToArray();
            group.coloredItem = coloredItem.ToArray();
            group.interactives = interactives.ToArray();
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            for (int i = 0; i < n; i++)
            {
                bool sel = i == selected;
                show[i].SetActive(sel);
                hide[i].SetActive(!sel);
                indicators[i].sizeDelta = new Vector2(sel ? indicatorWidths[i] : 0f, indicators[i].sizeDelta.y);
            }
            M3UdonBridge.Sync(group);
            return root;
        }

        static SelectionColorEntry Entry(Graphic g, ColorRef off, ColorRef on, ColorRef dOff, ColorRef dOn) =>
            new SelectionColorEntry { graphic = g, off = off, on = on, indeterminate = off, disabledOff = dOff, disabledOn = dOn, disabledIndeterminate = dOff };

        static void Pill(RectTransform rt, float w, float h, float centerY)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -centerY);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
