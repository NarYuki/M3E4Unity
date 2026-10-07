using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// FAB menu (FloatingActionButtonMenu.kt FloatingActionButtonMenuSample): a ToggleFloatingActionButton
    /// at the bottom end and FloatingActionButtonMenuItems above it (PrimaryContainer pills, 56 dp,
    /// 24 dp paddings, TitleMedium labels, 4 dp apart). Driven by M3FabMenu.
    /// </summary>
    public static class M3FabMenus
    {
        const float PaddingHorizontal = 16f;   // FabMenuPaddingHorizontal
        const float ButtonPaddingBottom = 16f; // FabMenuButtonPaddingBottom
        const float InitialRadius = 16f;       // FabInitialCornerRadius

        public static RectTransform Create(Transform parent, M3Context ctx, (string icon, string label)[] items, bool expanded = true)
        {
            float itemH = FabMenuBaselineTokens.ListItemContainerHeight;
            float spacing = FabMenuBaselineTokens.ListItemBetweenSpace;
            float lead = FabMenuBaselineTokens.ListItemLeadingSpace, trail = FabMenuBaselineTokens.ListItemTrailingSpace;
            float iconSize = FabMenuBaselineTokens.ListItemIconSize, gap = FabMenuBaselineTokens.ListItemIconLabelSpace;
            float fab = FabBaselineTokens.ContainerHeight;                 // FabInitialSize
            float finalRadius = FabMenuBaselineTokens.CloseButtonContainerHeight / 2f; // FabFinalCornerRadius
            var itemContainer = ColorRole.PrimaryContainer;
            var itemContent = M3Colors.ContentFor(itemContainer);
            var itemCtx = ctx.On(itemContainer);

            // measure items
            var widths = new float[items.Length];
            float maxW = fab;
            for (int i = 0; i < items.Length; i++)
            {
                var probe = M3Build.Text("Probe", (RectTransform)parent, items[i].label, TypeRole.TitleMedium, ColorRef.Clear, itemCtx);
                float tw = Mathf.Ceil(probe.GetPreferredValues(items[i].label).x);
                Object.DestroyImmediate(probe.gameObject);
                widths[i] = Mathf.Max(itemH, lead + iconSize + gap + tw + trail); // sizeIn(minWidth = FabMenuItemMinWidth)
                maxW = Mathf.Max(maxW, widths[i]);
            }
            float bottomPadding = fab + ButtonPaddingBottom + FabMenuBaselineTokens.CloseButtonBetweenSpace;
            float columnH = items.Length * itemH + (items.Length - 1) * spacing + bottomPadding;
            float height = Mathf.Max(fab + ButtonPaddingBottom, columnH);
            float width = maxW + 2f * PaddingHorizontal;

            var root = M3Build.Rect("FloatingActionButtonMenu", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;
            var menu = M3UdonBridge.Add<M3FabMenu>(root.gameObject);
            menu.expanded = expanded;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            menu.spatialDamping = spatial.DampingRatio;
            menu.spatialStiffness = spatial.Stiffness;
            menu.fastEffectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            menu.slowEffectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.SlowEffects).Stiffness;

            // items: aligned to the end, revealed from the end (the content is placed at align(End))
            var rects = new List<RectTransform>();
            var groups = new List<CanvasGroup>();
            float endX = width - PaddingHorizontal;
            float y = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                var item = M3Build.Rect("FloatingActionButtonMenuItem", root);
                item.anchorMin = item.anchorMax = new Vector2(0f, 1f);
                item.pivot = new Vector2(1f, 1f);
                item.anchoredPosition = new Vector2(endX, -y);
                item.sizeDelta = new Vector2(widths[i], itemH);
                M3Build.Shape("Container", item, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(FabMenuBaselineTokens.ListItemContainerShape)), ColorRef.Role(itemContainer));
                item.gameObject.AddComponent<RectMask2D>();
                var content = M3Build.Rect("Content", item);
                content.anchorMin = content.anchorMax = new Vector2(1f, 0.5f);
                content.pivot = new Vector2(1f, 0.5f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(widths[i], itemH);
                var icon = M3Build.Icon("Icon", content, items[i].icon, iconSize, ColorRef.Role(itemContent), itemCtx);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(lead, 0f);
                var label = M3Build.Text("Label", content, items[i].label, TypeRole.TitleMedium, ColorRef.Role(itemContent), itemCtx);
                label.alignment = TextAlignmentOptions.Left;
                var lr = label.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f);
                lr.pivot = new Vector2(0f, 0.5f);
                lr.anchoredPosition = new Vector2(lead + iconSize + gap, 0f);
                lr.sizeDelta = new Vector2(widths[i] - lead - iconSize - gap - trail, itemH);
                var group = item.gameObject.AddComponent<CanvasGroup>();
                var button = item.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                // the sample's items collapse the menu when clicked
                M3UdonBridge.Wire(button.onClick, menu, nameof(M3FabMenu._Close));
                rects.Add(item); groups.Add(group);
                y += itemH + spacing;
            }
            menu.items = rects.ToArray();
            menu.itemWidths = widths;
            menu.itemGroups = groups.ToArray();

            // ToggleFloatingActionButton (contentAlignment TopEnd, size 56, radius 16 → 28)
            var fabRt = M3Build.Rect("ToggleFloatingActionButton", root);
            fabRt.anchorMin = fabRt.anchorMax = new Vector2(0f, 1f);
            fabRt.pivot = new Vector2(1f, 0f);
            fabRt.anchoredPosition = new Vector2(endX, -(height - ButtonPaddingBottom));
            fabRt.sizeDelta = new Vector2(fab, fab);
            int frames = Mathf.RoundToInt((finalRadius - InitialRadius) / 0.5f) + 1;
            int level = Elevation.LevelOf(FabPrimaryContainerTokens.ContainerElevation);
            var shadows = M3Build.Shadows(fabRt, new CornerShape(InitialRadius), level, 0);
            var containerSprites = new Sprite[frames];
            var shadowSprites = new Sprite[shadows.Length * frames];
            for (int f = 0; f < frames; f++)
            {
                var shape = new CornerShape(InitialRadius + f * 0.5f);
                containerSprites[f] = M3ShapeAtlas.Get(ShapeCell.Create(ShapeKind.Fill).WithShape(shape));
                for (int s = 0; s < shadows.Length; s++)
                    shadowSprites[s * frames + f] = M3ShapeAtlas.Get(M3Build.ShadowCell(shape, level, s, out _));
            }
            for (int s = 0; s < shadows.Length; s++) shadows[s].sprite = shadowSprites[s * frames + (expanded ? frames - 1 : 0)];
            var container = M3Build.Shape("Container", fabRt, ShapeCell.Create(ShapeKind.Fill).WithShape(new CornerShape(InitialRadius)), ColorRef.Role(ColorRole.PrimaryContainer));
            var openIcon = M3Build.Icon("IconAdd", fabRt, "add", 24f, ColorRef.Role(ColorRole.OnPrimaryContainer), ctx);
            var closeIcon = M3Build.Icon("IconClose", fabRt, "close", 24f, ColorRef.Role(ColorRole.OnPrimary), ctx);
            foreach (var ic in new[] { openIcon, closeIcon })
            {
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ic.rectTransform.anchoredPosition = Vector2.zero;
                var b = ic.GetComponent<M3ColorBinding>(); if (b != null) Object.DestroyImmediate(b);
            }
            var cb = container.GetComponent<M3ColorBinding>(); if (cb != null) Object.DestroyImmediate(cb);
            var fabButton = fabRt.gameObject.AddComponent<Button>();
            fabButton.targetGraphic = container;
            fabButton.transition = Selectable.Transition.None;
            M3UdonBridge.Wire(fabButton.onClick, menu, nameof(M3FabMenu._Toggle));
            menu.fabContainer = container;
            menu.fabShadows = shadows;
            menu.fabFrames = containerSprites;
            menu.fabShadowFrames = shadowSprites;
            menu.iconOpen = openIcon.rectTransform; menu.iconClose = closeIcon.rectTransform;
            menu.iconOpenText = openIcon; menu.iconCloseText = closeIcon;
            menu.iconSizeFrom = FabBaselineTokens.IconSize;                 // FabInitialIconSize
            menu.iconSizeTo = FabMenuBaselineTokens.CloseButtonIconSize;    // FabFinalIconSize
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(ColorRole.PrimaryContainer), ColorRef.Role(ColorRole.Primary),     // ToggleFloatingActionButtonDefaults.containerColor
                ColorRef.Role(ColorRole.OnPrimaryContainer), ColorRef.Role(ColorRole.OnPrimary), // iconColor
            };
            M3UdonBridge.Sync(menu);
            return root;
        }
    }
}
