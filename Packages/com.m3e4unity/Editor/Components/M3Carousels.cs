using System.Collections.Generic;
using System.Linq;
using M3E4Unity.Carousel;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum CarouselKind { MultiBrowse, Uncontained, CenteredHero }

    /// <summary>
    /// Carousels (androidx carousel: HorizontalMultiBrowseCarousel, HorizontalUncontainedCarousel,
    /// HorizontalCenteredHeroCarousel). Items are clipped with Modifier.maskClip(shapes.extraLarge) as in
    /// the samples. The strategy is computed here with the ported keyline code; M3Carousel runs it.
    /// </summary>
    public static class M3Carousels
    {
        public static RectTransform Create(Transform parent, M3Context ctx, CarouselKind kind, float width, float height, int itemCount,
            float preferredItemWidth = 186f, float itemSpacing = 8f, string[] labels = null)
        {
            KeylineList keylines;
            switch (kind)
            {
                case CarouselKind.Uncontained: keylines = CarouselKeylines.Uncontained(width, preferredItemWidth, itemSpacing); break;
                case CarouselKind.CenteredHero: keylines = CarouselKeylines.Hero(width, preferredItemWidth, itemSpacing, itemCount, isCentered: true); break;
                default: keylines = CarouselKeylines.MultiBrowse(width, preferredItemWidth, itemSpacing, itemCount); break;
            }
            var strategy = new CarouselStrategy(keylines, width, itemSpacing, 0f, 0f);
            // the page (content) size is itemMainAxisSize.roundToInt(); carouselItem works with the float size
            float itemSize = strategy.ItemMainAxisSize;
            float pageSize = Mathf.Floor(itemSize + 0.5f);
            float maxScroll = Mathf.Max(0f, itemSize * itemCount + itemSpacing * (itemCount - 1) - width);

            var root = M3Build.Rect("Carousel (" + kind + ")", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;
            root.gameObject.AddComponent<RectMask2D>();
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var content = M3Build.Rect("ScrollContent", root);
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(width + maxScroll, height);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = root;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;

            var roles = new[] { ColorRole.PrimaryContainer, ColorRole.SecondaryContainer, ColorRole.TertiaryContainer, ColorRole.SurfaceContainerHighest };
            var items = new List<RectTransform>();
            var contents = new List<RectTransform>();
            var shape = ShapeScale.Get(ShapeRole.CornerExtraLarge);
            for (int i = 0; i < itemCount; i++)
            {
                var mask = M3Build.Shape("Item", root, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(ColorRole.Surface), false);
                var mrt = (RectTransform)mask.transform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 0.5f);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.sizeDelta = new Vector2(pageSize, height);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var role = roles[i % roles.Length];
                var inner = M3Build.Shape("Content", mrt, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(role), false);
                var crt = (RectTransform)inner.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(pageSize, height);
                var label = M3Build.Text("Label", crt, labels != null && i < labels.Length ? labels[i] : "Item " + (i + 1), TypeRole.TitleMedium,
                    ColorRef.Role(M3Colors.ContentFor(role)), ctx.On(role));
                label.alignment = TextAlignmentOptions.BottomLeft;
                M3Build.Fill(label.rectTransform, 16f, 16f, 16f, 16f);
                items.Add(mrt);
                contents.Add(crt);
            }

            var c = M3UdonBridge.Add<M3Carousel>(root.gameObject);
            c.scrollRect = scroll;
            c.content = content;
            c.items = items.ToArray();
            c.itemContents = contents.ToArray();
            c.itemSize = itemSize;
            c.pageSize = pageSize;
            c.itemSpacing = itemSpacing;
            c.availableSpace = width;
            c.maxScroll = maxScroll;
            int K = strategy.DefaultKeylines.Count;
            c.keylineCount = K;
            c.defaultSize = strategy.DefaultKeylines.Keylines.Select(k => k.Size).ToArray();
            c.defaultOffset = strategy.DefaultKeylines.Keylines.Select(k => k.Offset).ToArray();
            c.defaultUnadjusted = strategy.DefaultKeylines.Keylines.Select(k => k.UnadjustedOffset).ToArray();
            c.startStepCount = strategy.StartKeylineSteps.Count;
            c.startSize = strategy.StartKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.Size)).ToArray();
            c.startOffset = strategy.StartKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.Offset)).ToArray();
            c.startUnadjusted = strategy.StartKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.UnadjustedOffset)).ToArray();
            c.startPoints = strategy.StartShiftPoints.ToArray();
            c.endStepCount = strategy.EndKeylineSteps.Count;
            c.endSize = strategy.EndKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.Size)).ToArray();
            c.endOffset = strategy.EndKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.Offset)).ToArray();
            c.endUnadjusted = strategy.EndKeylineSteps.SelectMany(s => s.Keylines.Select(k => k.UnadjustedOffset)).ToArray();
            c.endPoints = strategy.EndShiftPoints.ToArray();
            c.startShiftDistance = strategy.StartShiftDistance;
            c.endShiftDistance = strategy.EndShiftDistance;
            c.snapScroll = Enumerable.Range(0, itemCount)
                .Select(i => Mathf.Clamp(i * (itemSize + itemSpacing) - SnapOffset(strategy, itemSize, i, itemCount), 0f, maxScroll)).ToArray();
            // uncontained carousels use noSnapFlingBehavior
            c.snap = kind != CarouselKind.Uncontained;

            var trigger = root.gameObject.AddComponent<EventTrigger>();
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.BeginDrag, c, nameof(M3Carousel._BeginDrag));
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.EndDrag, c, nameof(M3Carousel._EndDrag));
            M3UdonBridge.Wire(scroll.onValueChanged, c, nameof(M3Carousel._Scrolled));
            c.Layout();
            M3UdonBridge.Sync(c);
            return root;
        }

        static float Floor05(float x) => Mathf.Floor(x + 0.5f); // Kotlin roundToInt

        /// <summary>getSnapPositionOffset (KeylineSnapPosition.kt)</summary>
        static float SnapOffset(CarouselStrategy s, float itemSize, int itemIndex, int itemCount)
        {
            if (!s.IsValid) return 0f;
            float offset = Floor05(s.DefaultKeylines.FirstFocal.UnadjustedOffset - itemSize / 2f);
            int startLast = s.StartKeylineSteps.Count - 1;
            if (itemIndex <= startLast)
            {
                int stepIndex = Mathf.Clamp(startLast - itemIndex, 0, startLast);
                offset = Floor05(s.StartKeylineSteps[stepIndex].FirstFocal.UnadjustedOffset - itemSize / 2f);
            }
            int lastItem = itemCount - 1;
            int endLast = s.EndKeylineSteps.Count - 1;
            int focalCount = s.DefaultKeylines.LastFocalIndex - s.DefaultKeylines.FirstFocalIndex + 1;
            if (itemIndex >= lastItem - endLast && itemCount > focalCount)
            {
                int stepIndex = Mathf.Clamp(endLast - (lastItem - itemIndex), 0, endLast);
                offset = Floor05(s.EndKeylineSteps[stepIndex].LastFocal.UnadjustedOffset - itemSize / 2f);
            }
            return offset;
        }
    }
}
