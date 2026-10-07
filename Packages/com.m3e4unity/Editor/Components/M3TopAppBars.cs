using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum TopAppBarStyle { Small, CenterAligned, MediumFlexible, LargeFlexible }

    /// <summary>
    /// Top app bars (AppBar.kt): TopAppBar / CenterAlignedTopAppBar (SingleRowTopAppBar) and the
    /// expressive MediumFlexibleTopAppBar / LargeFlexibleTopAppBar (TwoRowsTopAppBar), laid out
    /// like TopAppBarMeasurePolicy.placeTopAppBar. Pass a ScrollRect to collapse / tint on scroll.
    /// </summary>
    public static class M3TopAppBars
    {
        const float HorizontalPadding = 4f;                       // TopAppBarHorizontalPadding
        const float TitleInset = 16f - HorizontalPadding;          // TopAppBarTitleInset
        const float MediumTitleBottomPadding = 24f;
        const float LargeTitleBottomPadding = 28f;
        const float IconButtonSlot = 48f;                          // IconButton with minimumInteractiveComponentSize

        public static RectTransform Create(Transform parent, M3Context ctx, TopAppBarStyle style, string title, float width,
            string subtitle = null, string navigationIcon = "arrow_back", string[] actions = null,
            bool centerTitle = false, ScrollRect scroll = null)
        {
            bool twoRows = style == TopAppBarStyle.MediumFlexible || style == TopAppBarStyle.LargeFlexible;
            bool center = centerTitle || style == TopAppBarStyle.CenterAligned;
            float collapsed = AppBarSmallTokens.ContainerHeight;
            float expanded = style == TopAppBarStyle.MediumFlexible
                ? (subtitle != null ? AppBarMediumFlexibleTokens.LargeContainerHeight : AppBarMediumFlexibleTokens.ContainerHeight)
                : style == TopAppBarStyle.LargeFlexible
                    ? (subtitle != null ? AppBarLargeFlexibleTokens.LargeContainerHeight : AppBarLargeFlexibleTokens.ContainerHeight)
                    : AppBarSmallTokens.ContainerHeight;
            var bg = AppBarTokens.ContainerColor;
            var inner = ctx.On(bg);

            var root = M3Build.Rect(style + "TopAppBar", parent);
            root.sizeDelta = new Vector2(width, expanded);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = expanded;
            var container = M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(bg));

            // top row: navigation icon, (small) title, actions
            var top = M3Build.Rect("TopRow", root);
            Place(top, 0f, 0f, width, collapsed);
            top.gameObject.AddComponent<RectMask2D>();
            float navW = 0f;
            if (navigationIcon != null)
            {
                var nav = M3IconButtons.Create(top, inner, navigationIcon, contentColor: AppBarTokens.LeadingIconColor);
                Center(nav, HorizontalPadding + IconButtonSlot / 2f, collapsed / 2f);
                navW = HorizontalPadding + IconButtonSlot;
            }
            float actW = 0f;
            if (actions != null && actions.Length > 0)
            {
                actW = actions.Length * IconButtonSlot + HorizontalPadding;
                for (int i = 0; i < actions.Length; i++)
                {
                    var a = M3IconButtons.Create(top, inner, actions[i], contentColor: AppBarTokens.TrailingIconColor);
                    Center(a, width - actW + i * IconButtonSlot + IconButtonSlot / 2f, collapsed / 2f);
                }
            }

            TypeRole smallTitleFont = AppBarSmallTokens.TitleFont;
            TypeRole smallSubFont = AppBarSmallTokens.SubtitleFont;
            var topTitle = TitleBlock(top, inner, title, subtitle, smallTitleFont, smallSubFont, center, width, navW, actW, out float tw, out float th, out _);
            var topRt = (RectTransform)topTitle.transform;
            Place(topRt, TitleX(center, width, tw, navW, actW), (collapsed - th) / 2f, tw, th);

            M3TopAppBar behaviour = null;
            CanvasGroup bottomGroup = null;
            RectTransform bottom = null;
            if (twoRows)
            {
                // bottom row: big title arranged at the bottom, its baseline titleBottomPadding above the bottom
                bottom = M3Build.Rect("BottomRow", root);
                Place(bottom, 0f, collapsed, width, expanded - collapsed);
                bottom.gameObject.AddComponent<RectMask2D>();
                TypeRole bigTitle = style == TopAppBarStyle.MediumFlexible ? AppBarMediumFlexibleTokens.TitleFont : AppBarLargeFlexibleTokens.TitleFont;
                TypeRole bigSub = style == TopAppBarStyle.MediumFlexible ? AppBarMediumFlexibleTokens.SubtitleFont : AppBarLargeFlexibleTokens.SubtitleFont;
                float bottomPadding = style == TopAppBarStyle.MediumFlexible ? MediumTitleBottomPadding : LargeTitleBottomPadding;
                var block = TitleBlock(bottom, inner, title, subtitle, bigTitle, bigSub, center, width, 0f, 0f, out float bw, out float bh, out float lastBaseline);
                var brt = (RectTransform)block.transform;
                float fromBottom = Mathf.Max(0f, bottomPadding - (bh - lastBaseline));
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f);
                brt.pivot = new Vector2(0f, 0f);
                brt.anchoredPosition = new Vector2(TitleX(center, width, bw, 0f, 0f), fromBottom);
                brt.sizeDelta = new Vector2(bw, bh);
                bottomGroup = block.gameObject.AddComponent<CanvasGroup>();
                // expanded: the small title is hidden (TopTitleAlphaEasing(0) = 0)
                topTitle.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            }

            if (twoRows || scroll != null)
            {
                behaviour = M3UdonBridge.Add<M3TopAppBar>(root.gameObject);
                behaviour.twoRows = twoRows;
                behaviour.bar = root;
                behaviour.bottomRow = bottom;
                behaviour.collapsedHeight = collapsed;
                behaviour.expandedHeight = expanded;
                behaviour.topTitle = twoRows ? topTitle.GetComponent<CanvasGroup>() : null;
                behaviour.bottomTitle = bottomGroup;
                behaviour.scrollRect = scroll;
                behaviour.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
                var sc = root.gameObject.AddComponent<M3SelectionColors>();
                var c0 = ColorRef.Role(bg);
                var c1 = ColorRef.Role(AppBarTokens.OnScrollContainerColor);
                sc.entries = new List<SelectionColorEntry>
                {
                    new SelectionColorEntry { graphic = container, off = c0, on = c1, indeterminate = c0, disabledOff = c0, disabledOn = c1, disabledIndeterminate = c0 },
                };
                if (scroll != null) M3UdonBridge.Wire(scroll.onValueChanged, behaviour, nameof(M3TopAppBar._Scrolled));
                M3UdonBridge.Sync(behaviour);
            }
            return root;
        }

        /// <summary>Title (+ subtitle) column; the Box / Column has 4 dp horizontal padding.</summary>
        static RectTransform TitleBlock(RectTransform parent, M3Context ctx, string title, string subtitle, TypeRole titleFont, TypeRole subFont,
            bool center, float barWidth, float navW, float actW, out float width, out float height, out float lastBaseline)
        {
            var block = M3Build.Rect("Title", parent);
            float maxW = Mathf.Max(0f, barWidth - Mathf.Max(TitleInset, navW) - actW) - 2f * HorizontalPadding;
            var t = M3Build.Text("Text", block, title, titleFont, ColorRef.Role(AppBarTokens.TitleColor), ctx);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            float tw = Mathf.Min(Mathf.Ceil(t.GetPreferredValues(title).x), maxW);
            var (th, _, tLast) = M3Build.MeasureText(t, title, tw);
            float sw = 0f, sh = 0f, sLast = 0f;
            TextMeshProUGUI s = null;
            if (subtitle != null)
            {
                s = M3Build.Text("Subtitle", block, subtitle, subFont, ColorRef.Role(AppBarTokens.SubtitleColor), ctx);
                s.enableWordWrapping = false;
                s.overflowMode = TextOverflowModes.Ellipsis;
                s.raycastTarget = false;
                sw = Mathf.Min(Mathf.Ceil(s.GetPreferredValues(subtitle).x), maxW);
                (sh, _, sLast) = M3Build.MeasureText(s, subtitle, sw);
            }
            float inner = Mathf.Max(tw, sw);
            width = inner + 2f * HorizontalPadding;
            height = th + sh;
            // Column(horizontalAlignment = titleHorizontalAlignment)
            float tx = HorizontalPadding + (center ? (inner - tw) / 2f : 0f);
            Place(t.rectTransform, tx, 0f, tw, th);
            t.alignment = TextAlignmentOptions.TopLeft;
            if (s != null)
            {
                float sx = HorizontalPadding + (center ? (inner - sw) / 2f : 0f);
                Place(s.rectTransform, sx, th, sw, sh);
                s.alignment = TextAlignmentOptions.TopLeft;
                lastBaseline = th + sLast;
            }
            else lastBaseline = tLast;
            return block;
        }

        /// <summary>placeTopAppBar: align in the full width, then push away from the navigation icon / actions.</summary>
        static float TitleX(bool center, float barWidth, float titleW, float navW, float actW)
        {
            float start = Mathf.Max(TitleInset, navW);
            float x = center ? (barWidth - titleW) / 2f : 0f;
            if (x < start) x += start - x;
            else if (x + titleW > barWidth - actW) x += (barWidth - actW) - (x + titleW);
            return x;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Center(RectTransform rt, float cx, float cy)
        {
            var l = rt.GetComponent<LayoutElement>();
            if (l != null) l.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }
    }
}
