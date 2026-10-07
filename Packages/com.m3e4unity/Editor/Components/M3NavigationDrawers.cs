using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public sealed class DrawerItemSpec
    {
        public string Label;
        public string Icon;
        /// <summary>Badge text at the end (the samples show a count).</summary>
        public string Badge;
    }

    /// <summary>
    /// Modal navigation drawer (NavigationDrawer.kt ModalNavigationDrawer + ModalDrawerSheet +
    /// NavigationDrawerItem, as in ModalNavigationDrawerSample): a 360 dp SurfaceContainerLow sheet
    /// with LargeEnd corners over a scrim; it opens with DefaultSpatial and closes with FastEffects.
    /// </summary>
    public static class M3NavigationDrawers
    {
        const float ItemPaddingH = 12f;   // NavigationDrawerItemDefaults.ItemPadding
        const float TopSpacer = 12f;      // the sample's Spacer(Modifier.height(12.dp))
        const float ItemStart = 16f, ItemEnd = 24f, IconGap = 12f;

        public static RectTransform Modal(Transform parent, M3Context ctx, DrawerItemSpec[] items, int selected, bool open = true)
        {
            var host = M3Build.Rect("ModalNavigationDrawer", parent);
            M3Build.Fill(host);
            host.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var root = M3Build.Rect("DrawerRoot", host);
            M3Build.Fill(root);
            var scrimRt = M3Build.Rect("ScrimLayer", root);
            M3Build.Fill(scrimRt);
            var scrim = M3Build.Shape("Scrim", scrimRt, ShapeCell.Create(ShapeKind.Fill), ColorRef.Translucent(ScrimTokens.ContainerColor, ScrimTokens.ContainerOpacity));
            var scrimGroup = scrimRt.gameObject.AddComponent<CanvasGroup>();

            var bg = NavigationDrawerTokens.ModalContainerColor;
            var inner = ctx.On(bg);
            float width = NavigationDrawerTokens.ContainerWidth;
            var sheet = M3Build.Rect("ModalDrawerSheet", root);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(0f, 1f);
            sheet.pivot = new Vector2(0f, 0.5f);
            sheet.anchoredPosition = Vector2.zero;
            sheet.sizeDelta = new Vector2(width, 0f);
            M3Build.Shape("Container", sheet, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(NavigationDrawerTokens.ContainerShape)), ColorRef.Role(bg))
                .raycastTarget = true;

            var col = M3Build.Rect("Content", sheet);
            M3Build.Fill(col);
            var v = M3Build.Column(col, 0f);
            v.padding = new RectOffset((int)ItemPaddingH, (int)ItemPaddingH, (int)TopSpacer, 0);
            v.childForceExpandWidth = true;

            var interactives = new List<M3Interactive>();
            for (int i = 0; i < items.Length; i++)
                interactives.Add(Item(col, inner, items[i], i == selected, width - 2f * ItemPaddingH));
            foreach (var it in interactives) { it.exclusiveWith = interactives.ToArray(); M3UdonBridge.Sync(it); }

            var motion = M3UdonBridge.Add<M3Sheet>(host.gameObject);
            motion.sheet = sheet;
            motion.scrim = scrimGroup;
            motion.root = root.gameObject;
            motion.open = open;
            motion.hiddenOffset = new Vector2(-width, 0f);
            motion.useSpring = true;
            var openSpring = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            var closeSpring = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects);
            motion.openDamping = openSpring.DampingRatio; motion.openStiffness = openSpring.Stiffness;
            motion.closeDamping = closeSpring.DampingRatio; motion.closeStiffness = closeSpring.Stiffness;
            motion.sheetWidth = width;
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            M3UdonBridge.Wire(scrimButton.onClick, motion, nameof(M3Sheet._Close));
            if (!open) root.gameObject.SetActive(false);
            M3UdonBridge.Sync(motion);
            return host;
        }

        static M3Interactive Item(RectTransform parent, M3Context ctx, DrawerItemSpec s, bool selected, float width)
        {
            float h = NavigationDrawerTokens.ActiveIndicatorHeight;
            var item = M3Build.Rect("NavigationDrawerItem", parent);
            var le = item.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            item.sizeDelta = new Vector2(width, h);
            var style = new PressableStyle
            {
                Container = null,
                ContainerChecked = NavigationDrawerTokens.ActiveIndicatorColor,
                Content = NavigationDrawerTokens.InactiveLabelTextColor,
                ContentChecked = NavigationDrawerTokens.ActiveLabelTextColor,
                Toggle = true,
                Selected = selected,
                RestShape = h / 2f, PressedShape = h / 2f, CheckedShape = h / 2f, MaxShape = h / 2f,
            };
            var p = new M3Pressable(item, ctx, style);
            var row = M3Build.Row(item, 0f, TextAnchor.MiddleLeft);
            row.padding = new RectOffset((int)ItemStart, (int)ItemEnd, 0, 0);
            if (s.Icon != null)
            {
                var icon = M3Build.Icon("Icon", item, s.Icon, NavigationDrawerTokens.IconSize, ColorRef.Role(NavigationDrawerTokens.InactiveIconColor), ctx);
                p.Content(icon, NavigationDrawerTokens.InactiveIconColor, NavigationDrawerTokens.ActiveIconColor);
                M3Build.Rect("Spacer", item).gameObject.AddComponent<LayoutElement>().minWidth = IconGap;
            }
            var label = M3Build.Text("Label", item, s.Label, NavigationDrawerTokens.LabelTextFont, ColorRef.Role(style.Content), ctx);
            label.alignment = TextAlignmentOptions.Left;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            p.Content(label);
            if (s.Badge != null)
            {
                M3Build.Rect("Spacer", item).gameObject.AddComponent<LayoutElement>().minWidth = IconGap;
                // badgeColor = text color (NavigationDrawerItemDefaults.colors: selectedBadgeColor = selectedTextColor)
                var badge = M3Build.Text("Badge", item, s.Badge, NavigationDrawerTokens.LargeBadgeLabelFont, ColorRef.Role(style.Content), ctx);
                badge.alignment = TextAlignmentOptions.Right;
                p.Content(badge);
            }
            return p.Finish();
        }
    }
}
