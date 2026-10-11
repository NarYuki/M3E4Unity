using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Pull to refresh (PullToRefresh.kt PullToRefreshBox with PullToRefreshDefaults.Indicator or the
    /// expressive PullToRefreshDefaults.LoadingIndicator) around a list in a vertical ScrollRect.
    /// </summary>
    public static class M3PullToRefreshes
    {
        const float SpinnerContainerSize = 40f;   // IndicatorBox size
        const float SpinnerSize = 16f;            // (ArcRadius + StrokeWidth) * 2 + …
        const float StrokeWidth = 2.5f;
        const float ArcRadius = 5.5f;
        const float ArrowWidth = 10f, ArrowHeight = 5f;
        const float Threshold = 80f;              // PositionalThreshold = IndicatorMaxDistance

        /// <param name="loadingIndicator">PullToRefreshDefaults.LoadingIndicator instead of Indicator.</param>
        /// <param name="demoRefreshSeconds">End a refresh by itself after this time (0 = call _FinishRefresh).</param>
        public static RectTransform Box(Transform parent, M3Context ctx, float width, float height, string[] items,
            bool loadingIndicator = false, float demoRefreshSeconds = 0f)
        {
            var root = M3Build.Rect(loadingIndicator ? "PullToRefreshBox (LoadingIndicator)" : "PullToRefreshBox", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = width;
            le.minHeight = le.preferredHeight = height;
            var bg = ctx.Background;

            // content: a list in a vertical ScrollRect (clamped: the pull replaces the overscroll)
            var viewport = M3Build.Rect("Viewport", root);
            M3Build.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var content = M3Build.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            float itemH = ListTokens.ItemOneLineContainerHeight;
            content.sizeDelta = new Vector2(0f, items.Length * itemH);
            for (int i = 0; i < items.Length; i++)
            {
                var item = M3Lists.Item(content, ctx, new ListItemSpec { Headline = items[i] }, width);
                item.anchorMin = item.anchorMax = new Vector2(0f, 1f);
                item.pivot = new Vector2(0f, 1f);
                item.anchoredPosition = new Vector2(0f, -i * itemH);
                item.sizeDelta = new Vector2(width, itemH);
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            // the indicator: IndicatorBox aligned TopCenter, drawn only below the box's top edge
            var clip = M3Build.Rect("IndicatorClip", root);
            M3Build.Fill(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            float boxW = loadingIndicator ? LoadingIndicatorTokens.ContainerWidth : SpinnerContainerSize;
            float boxH = loadingIndicator ? LoadingIndicatorTokens.ContainerHeight : SpinnerContainerSize;
            var box = M3Build.Rect("IndicatorBox", clip);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(boxW, boxH);
            box.anchoredPosition = new Vector2(0f, boxH);

            var ptr = M3UdonBridge.Add<M3PullToRefresh>(root.gameObject);
            ptr.scrollRect = scroll;
            ptr.viewport = viewport;
            ptr.style = loadingIndicator ? 1 : 0;
            ptr.indicatorBox = box;
            ptr.boxHeight = boxH;
            ptr.maxDistance = Threshold;
            ptr.threshold = Threshold;
            ptr.demoRefreshSeconds = demoRefreshSeconds;
            ptr.stateStiffness = 1500f; // Animatable.animateTo default spring(): StiffnessMedium
            ptr.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;

            var pullGroupRt = M3Build.Rect("Pull", box);
            M3Build.Fill(pullGroupRt);
            var pullGroup = pullGroupRt.gameObject.AddComponent<CanvasGroup>();
            pullGroup.blocksRaycasts = false;
            var refreshGroupRt = M3Build.Rect("Refreshing", box);
            M3Build.Fill(refreshGroupRt);
            var refreshGroup = refreshGroupRt.gameObject.AddComponent<CanvasGroup>();
            refreshGroup.blocksRaycasts = false;
            refreshGroup.alpha = 0f;
            ptr.pullGroup = pullGroup;
            ptr.refreshGroup = refreshGroup;

            if (!loadingIndicator)
            {
                // Indicator: 40 dp circle, surfaceContainerHigh, Elevation (Level2) while visible
                ptr.shadows = M3Build.Shadows(box, CornerShape.Full, Elevation.LevelOf(ElevationTokens.Level2), 0);
                foreach (var s in ptr.shadows) s.enabled = false;
                M3Build.Shape("Container", box, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Role(ColorRole.SurfaceContainerHigh))
                    .transform.SetSiblingIndex(ptr.shadows.Length);

                // CircularArrowProgressIndicator (alpha 0.3 / 1)
                var alphaRt = M3Build.Rect("ArrowAlpha", pullGroupRt);
                M3Build.Fill(alphaRt);
                var alphaGroup = alphaRt.gameObject.AddComponent<CanvasGroup>();
                alphaGroup.alpha = 0.3f;
                ptr.arrowAlphaGroup = alphaGroup;
                var arrow = M3Build.Shape("Arrow", alphaRt, ShapeCell.Create(ShapeKind.PullArrow)
                    .WithExtra(StrokeWidth, ArcRadius + StrokeWidth / 2f, ArrowWidth, ArrowHeight, 0f), ColorRef.Role(ColorRole.OnSurfaceVariant), false);
                Center((RectTransform)arrow.transform, SpinnerSize);
                ptr.pullMaterial = M3ShapeAtlas.CreateDynamicMaterial("PullArrow", Vector4.zero);
                arrow.material = ptr.pullMaterial;
                ptr.pullImages = new[] { arrow };

                // CircularProgressIndicator(strokeWidth = StrokeWidth, color = indicatorColor), 16 dp, no track
                var spinner = M3Build.Shape("Spinner", refreshGroupRt, ShapeCell.Create(ShapeKind.CircularProgress)
                    .WithExtra(0f, CircularProgressIndicatorTokens.TrackActiveSpace, StrokeWidth, 0f, 0f, 0f, 0f, 1f).WithExtra2(0f),
                    ColorRef.Role(ColorRole.OnSurfaceVariant), false);
                Center((RectTransform)spinner.transform, SpinnerSize);
                ptr.refreshMaterial = M3ShapeAtlas.CreateDynamicMaterial("PullSpinner", Vector4.zero);
                spinner.material = ptr.refreshMaterial;
                ptr.refreshImages = new[] { spinner };
            }
            else
            {
                // LoadingIndicator: containedContainerColor circle, LoadingIndicatorElevation (Level0)
                M3Build.Shape("Container", box, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Role(LoadingIndicatorTokens.ContainedContainerColor))
                    .transform.SetAsFirstSibling();
                var rotate = M3Build.Rect("Rotate", pullGroupRt);
                M3Build.Fill(rotate);
                ptr.pullRotate = rotate;
                // ContainedLoadingIndicator(progress = distanceFraction): the determinate indicator without its own M3Progress
                var det = M3ProgressIndicators.Loading(rotate, ctx, contained: true, progress: 0f);
                var prog = det.GetComponent<M3Progress>();
                ptr.pullMaterial = prog.dynamicMaterial;
                ptr.pullImages = prog.parts;
                Object.DestroyImmediate(prog);
                Center(det, boxW);
                // ContainedLoadingIndicator(): indeterminate, started when it appears
                var ind = M3ProgressIndicators.Loading(refreshGroupRt, ctx, contained: true);
                Center(ind, boxW);
                var indImg = ind.Find("Indicator").GetComponent<Image>();
                ptr.refreshMaterial = M3ShapeAtlas.CreateDynamicMaterial("PullLoading", Vector4.zero);
                indImg.material = ptr.refreshMaterial;
                ptr.refreshImages = new[] { indImg };
            }
            M3UdonBridge.Sync(ptr);

            // the drag reaches the behaviour through an EventTrigger on the ScrollRect itself
            var trigger = viewport.gameObject.AddComponent<EventTrigger>();
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.BeginDrag, ptr, nameof(M3PullToRefresh._BeginDrag));
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.EndDrag, ptr, nameof(M3PullToRefresh._EndDrag));
            return root;
        }

        static void Center(RectTransform rt, float size)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
        }
    }
}
