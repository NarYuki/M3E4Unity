using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Expressive (wide) navigation rail (WideNavigationRail.kt): collapsed 96 dp with top-icon
    /// items, expanded 220..360 dp with start-icon items; a menu button header toggles it.
    /// The layout is computed at runtime by M3NavigationRail.
    /// </summary>
    public static class M3NavigationRails
    {
        const float DisabledAlpha = 0.38f;

        public static RectTransform Create(Transform parent, M3Context ctx, NavigationItemSpec[] items, int selected, float height,
            bool expanded = false, bool menuHeader = true, ColorRole? containerColor = null, ShapeRole? shape = null,
            Component menuTarget = null, string menuEvent = null, bool modal = false)
        {
            ColorRole railColor = containerColor ?? NavigationRailCollapsedTokens.ContainerColor;
            var inner = ctx.On(railColor);
            var root = M3Build.Rect("WideNavigationRail", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            le.preferredWidth = le.minWidth = NavigationRailExpandedTokens.ContainerWidthMaximum;
            root.sizeDelta = new Vector2(NavigationRailExpandedTokens.ContainerWidthMaximum, height);

            // the rail surface (its width is animated); the root reserves the maximum width
            var rail = M3Build.Rect("Rail", root);
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 1f);
            rail.anchoredPosition = Vector2.zero;
            rail.sizeDelta = new Vector2(NavigationRailCollapsedTokens.ContainerWidth, 0f);
            M3Build.Shape("Container", rail, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(shape ?? NavigationRailCollapsedTokens.ContainerShape)), ColorRef.Role(railColor));

            var r = M3UdonBridge.Add<M3NavigationRail>(root.gameObject);
            r.rail = rail;
            r.expanded = expanded;
            r.selectedIndex = selected;
            r.collapsedWidth = NavigationRailCollapsedTokens.ContainerWidth;
            r.expandedMinWidth = NavigationRailExpandedTokens.ContainerWidthMinimum;
            r.expandedMaxWidth = NavigationRailExpandedTokens.ContainerWidthMaximum;
            r.topPadding = NavigationRailCollapsedTokens.TopSpace;
            r.headerPadding = NavigationRailBaselineItemTokens.HeaderSpaceMinimum;
            r.collapsedItemSpacing = NavigationRailCollapsedTokens.ItemVerticalSpace;
            r.collapsedItemMinHeight = NavigationRailBaselineItemTokens.ContainerHeight;
            r.expandedItemMinHeight = 48f; // LocalMinimumInteractiveComponentSize
            r.iconSize = NavigationRailBaselineItemTokens.IconSize;
            r.itemHorizontalPadding = 20f; // WNRItemHorizontalPadding
            r.indicatorPadH = (NavigationRailVerticalItemTokens.ActiveIndicatorWidth - NavigationRailBaselineItemTokens.IconSize) / 2f;
            r.indicatorPadVCollapsed = (NavigationRailVerticalItemTokens.ActiveIndicatorHeight - NavigationRailBaselineItemTokens.IconSize) / 2f;
            r.indicatorPadVExpanded = (NavigationRailHorizontalItemTokens.ActiveIndicatorHeight - NavigationRailBaselineItemTokens.IconSize) / 2f;
            r.topIconToLabel = NavigationRailVerticalItemTokens.IconLabelSpace;
            r.startIconToLabel = NavigationRailHorizontalItemTokens.IconLabelSpace;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            r.springDamping = spatial.DampingRatio;
            r.springStiffness = spatial.Stiffness;
            if (modal)
            {
                // WideNavigationRailLayout(isModal = true): the widths animate with FastSpatial
                var fast = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
                r.widthDamping = fast.DampingRatio;
                r.widthStiffness = fast.Stiffness;
            }

            if (menuHeader)
            {
                // the samples' header: an IconButton(menu / menu_open) that toggles the rail, aligned with the item icons
                var header = M3Build.Rect("Header", rail);
                header.sizeDelta = new Vector2(NavigationRailCollapsedTokens.ContainerWidth, 48f);
                var btn = M3IconButtons.Create(header, inner, "menu");
                var bl = btn.GetComponent<LayoutElement>(); if (bl != null) bl.ignoreLayout = true;
                btn.anchorMin = btn.anchorMax = new Vector2(0f, 0.5f);
                btn.pivot = new Vector2(0.5f, 0.5f);
                btn.anchoredPosition = new Vector2(NavigationRailCollapsedTokens.ContainerWidth / 2f, 0f);
                M3UdonBridge.Wire(btn.GetComponent<Button>().onClick, menuTarget != null ? menuTarget : r, menuEvent ?? nameof(M3NavigationRail._Toggle));
                r.header = header;
                r.headerHeight = 48f;
            }

            int n = items.Length;
            var L = new Lists(n);
            var entries = new List<SelectionColorEntry>();
            var coloredItem = new List<int>();
            var content = M3Colors.ContentFor(railColor);
            for (int i = 0; i < n; i++)
            {
                var s = items[i];
                var item = M3Build.Rect("NavigationItem", rail);
                var pill = ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full);
                var indicator = M3Build.Shape("Indicator", item, pill, ColorRef.Role(NavigationRailColorTokens.ItemActiveIndicator), false);
                indicator.raycastTarget = false;
                var stateLayer = M3Build.Shape("StateLayer", item, pill, ColorRef.Clear, false);
                var ripple = M3Build.Shape("Ripple", item, ShapeCell.Create(ShapeKind.Ripple).WithShape(CornerShape.Full), ColorRef.Clear, false);
                var ind = ColorRef.Role(NavigationRailColorTokens.ItemActiveIndicator);
                entries.Add(Entry(indicator, ind, ind, ind, ind)); coloredItem.Add(i);

                var iconOff = M3Build.Icon("Icon", item, s.Icon, r.iconSize, ColorRef.Role(NavigationRailColorTokens.ItemInactiveIcon), inner);
                var iconOn = M3Build.Icon("SelectedIcon", item, s.SelectedIcon ?? s.Icon, r.iconSize, ColorRef.Role(NavigationRailColorTokens.ItemActiveIcon), inner, filled: s.SelectedIcon == null);
                var iconDis = ColorRef.Over(railColor, NavigationRailColorTokens.ItemInactiveIcon, DisabledAlpha);
                foreach (var ic in new[] { iconOff, iconOn })
                {
                    entries.Add(Entry(ic, ColorRef.Role(NavigationRailColorTokens.ItemInactiveIcon), ColorRef.Role(NavigationRailColorTokens.ItemActiveIcon), iconDis, iconDis));
                    coloredItem.Add(i);
                }

                // labels: LabelMedium (top icon, selected = ItemActiveLabelText) and LabelLarge (start icon, selected = ItemActiveIcon)
                var labels = M3Build.Rect("Labels", item);
                M3Build.Fill(labels);
                var group = labels.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                var lt = M3Build.Text("LabelTop", labels, s.Label, NavigationRailVerticalItemTokens.LabelTextFont, ColorRef.Role(NavigationRailColorTokens.ItemInactiveLabelText), inner);
                var ls = M3Build.Text("LabelStart", labels, s.Label, NavigationRailHorizontalItemTokens.LabelTextFont, ColorRef.Role(NavigationRailColorTokens.ItemInactiveLabelText), inner);
                var labDis = ColorRef.Over(railColor, NavigationRailColorTokens.ItemInactiveLabelText, DisabledAlpha);
                entries.Add(Entry(lt, ColorRef.Role(NavigationRailColorTokens.ItemInactiveLabelText), ColorRef.Role(NavigationRailColorTokens.ItemActiveLabelText), labDis, labDis)); coloredItem.Add(i);
                entries.Add(Entry(ls, ColorRef.Role(NavigationRailColorTokens.ItemInactiveLabelText), ColorRef.Role(NavigationRailColorTokens.ItemActiveIcon), labDis, labDis)); coloredItem.Add(i);
                foreach (var t in new[] { lt, ls }) { t.alignment = TextAlignmentOptions.TopLeft; t.raycastTarget = false; }

                var spec = new InteractionSpec
                {
                    Root = item,
                    Context = inner,
                    Enabled = s.Enabled,
                    Selected = i == selected,
                    RestShape = 16f, PressedShape = 16f, SelectedShape = 16f, SelectedPressedShape = 16f, MaxShape = 16f,
                    StateLayer = stateLayer,
                    Ripple = ripple,
                    RippleClipShape = CornerShape.Full,
                };
                spec.ShapeLayers.Add((stateLayer, _ => pill));
                spec.Color(stateLayer, ColorRef.Over(railColor, content, StateLayer.Hover),
                    ColorRef.Over(NavigationRailColorTokens.ItemActiveIndicator, content, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                spec.RippleColor = ColorRef.Over(railColor, content, StateLayer.Hover, content, StateLayer.Pressed);
                spec.RippleColorSelected = ColorRef.Over(NavigationRailColorTokens.ItemActiveIndicator, content, StateLayer.Hover, content, StateLayer.Pressed);
                var it = M3Interaction.Apply(spec);
                var button = item.GetComponent<Button>();
                M3UdonBridge.Wire(button.onClick, r, "_Select" + i);

                L.items.Add(button); L.interactives.Add(it); L.itemRects.Add(item);
                L.indicators.Add((RectTransform)indicator.transform); L.indicatorGraphics.Add(indicator);
                L.stateLayers.Add((RectTransform)stateLayer.transform); L.ripples.Add((RectTransform)ripple.transform);
                L.iconsOff.Add(iconOff.rectTransform); L.iconsOn.Add(iconOn.rectTransform);
                L.labelsTop.Add(lt.rectTransform); L.labelsStart.Add(ls.rectTransform); L.labelGroups.Add(group);
                L.topW.Add(Mathf.Ceil(lt.GetPreferredValues(s.Label).x)); L.topH.Add(Mathf.Ceil(lt.GetPreferredValues(s.Label).y));
                L.startW.Add(Mathf.Ceil(ls.GetPreferredValues(s.Label).x)); L.startH.Add(Mathf.Ceil(ls.GetPreferredValues(s.Label).y));
            }

            r.items = L.items.ToArray();
            r.interactives = L.interactives.ToArray();
            r.itemRects = L.itemRects.ToArray();
            r.indicators = L.indicators.ToArray();
            r.indicatorGraphics = L.indicatorGraphics.ToArray();
            r.stateLayers = L.stateLayers.ToArray();
            r.ripples = L.ripples.ToArray();
            r.iconsOff = L.iconsOff.ToArray();
            r.iconsOn = L.iconsOn.ToArray();
            r.labelsTop = L.labelsTop.ToArray();
            r.labelsStart = L.labelsStart.ToArray();
            r.labelGroups = L.labelGroups.ToArray();
            r.topLabelW = L.topW.ToArray(); r.topLabelH = L.topH.ToArray();
            r.startLabelW = L.startW.ToArray(); r.startLabelH = L.startH.ToArray();
            r.coloredItem = coloredItem.ToArray();
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            M3UdonBridge.Sync(r);
            return root;
        }

        /// <summary>
        /// ModalWideNavigationRail: the collapsed rail in the layout (unless hideOnCollapse) and, over a
        /// scrim spanning screenWidth, a modal rail (ModalContainerColor, ModalContainerShape) that expands
        /// when opened. M3ModalNavigationRail drives it.
        /// </summary>
        public static RectTransform Modal(Transform parent, M3Context ctx, NavigationItemSpec[] items, int selected, float height,
            float screenWidth, bool hideOnCollapse = false, bool open = false)
        {
            float collapsedW = NavigationRailCollapsedTokens.ContainerWidth;
            var root = M3Build.Rect("ModalWideNavigationRail", parent);
            float reserve = hideOnCollapse ? 0f : collapsedW;
            root.sizeDelta = new Vector2(Mathf.Max(reserve, 1f), height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = reserve;
            le.preferredHeight = le.minHeight = height;
            var ctrl = M3UdonBridge.Add<M3ModalNavigationRail>(root.gameObject);
            ctrl.open = open;
            ctrl.hideOnCollapse = hideOnCollapse;
            ctrl.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            ctrl.spatialDamping = spatial.DampingRatio;
            ctrl.spatialStiffness = spatial.Stiffness;

            if (!hideOnCollapse)
            {
                var baseRoot = Create(root, ctx, items, selected, height, false, true, menuTarget: ctrl, menuEvent: nameof(M3ModalNavigationRail._Open));
                Place(baseRoot, collapsedW, height);
                var baseRail = baseRoot.GetComponent<M3NavigationRail>();
                baseRail.changeListener = ctrl;
                ctrl.baseRail = baseRail;
                var gos = new GameObject[baseRail.itemRects.Length];
                for (int i = 0; i < gos.Length; i++) gos[i] = baseRail.itemRects[i].gameObject;
                ctrl.baseItems = gos;
                M3UdonBridge.Sync(baseRail);
            }

            // the modal window: scrim + the modal rail
            var overlay = M3Build.Rect("Modal", root);
            overlay.anchorMin = overlay.anchorMax = new Vector2(0f, 1f);
            overlay.pivot = new Vector2(0f, 1f);
            overlay.anchoredPosition = Vector2.zero;
            overlay.sizeDelta = new Vector2(screenWidth, height);
            var scrimImg = M3Build.Shape("Scrim", overlay, ShapeCell.Create(ShapeKind.Fill), ColorRef.Translucent(ScrimTokens.ContainerColor, ScrimTokens.ContainerOpacity));
            var scrimGroup = scrimImg.gameObject.AddComponent<CanvasGroup>();
            var scrimButton = scrimImg.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            var nav = scrimButton.navigation; nav.mode = Navigation.Mode.None; scrimButton.navigation = nav;
            M3UdonBridge.Wire(scrimButton.onClick, ctrl, nameof(M3ModalNavigationRail._Close));
            var modalRoot = Create(overlay, ctx, items, selected, height, open || hideOnCollapse, true,
                containerColor: NavigationRailExpandedTokens.ModalContainerColor, shape: NavigationRailExpandedTokens.ModalContainerShape,
                menuTarget: ctrl, menuEvent: nameof(M3ModalNavigationRail._Close), modal: true);
            Place(modalRoot, NavigationRailExpandedTokens.ContainerWidthMaximum, height);
            var modalRail = modalRoot.GetComponent<M3NavigationRail>();
            modalRail.changeListener = ctrl;
            M3UdonBridge.Sync(modalRail);
            ctrl.modalRail = modalRail;
            ctrl.overlay = overlay.gameObject;
            ctrl.scrim = scrimGroup;
            ctrl.modalRect = modalRoot;
            ctrl.modalWidth = NavigationRailExpandedTokens.ContainerWidthMaximum;
            overlay.gameObject.SetActive(open);
            scrimGroup.alpha = open ? 1f : 0f;
            M3UdonBridge.Sync(ctrl);
            return root;
        }

        static void Place(RectTransform rt, float w, float h)
        {
            var l = rt.GetComponent<LayoutElement>(); if (l != null) l.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        sealed class Lists
        {
            public readonly List<Selectable> items = new List<Selectable>();
            public readonly List<M3Interactive> interactives = new List<M3Interactive>();
            public readonly List<RectTransform> itemRects = new List<RectTransform>(), indicators = new List<RectTransform>(),
                stateLayers = new List<RectTransform>(), ripples = new List<RectTransform>(), iconsOff = new List<RectTransform>(),
                iconsOn = new List<RectTransform>(), labelsTop = new List<RectTransform>(), labelsStart = new List<RectTransform>();
            public readonly List<Graphic> indicatorGraphics = new List<Graphic>();
            public readonly List<CanvasGroup> labelGroups = new List<CanvasGroup>();
            public readonly List<float> topW = new List<float>(), topH = new List<float>(), startW = new List<float>(), startH = new List<float>();
            public Lists(int n) { }
        }

        static SelectionColorEntry Entry(Graphic g, ColorRef off, ColorRef on, ColorRef dOff, ColorRef dOn) =>
            new SelectionColorEntry { graphic = g, off = off, on = on, indeterminate = off, disabledOff = dOff, disabledOn = dOn, disabledIndeterminate = dOff };
    }
}
