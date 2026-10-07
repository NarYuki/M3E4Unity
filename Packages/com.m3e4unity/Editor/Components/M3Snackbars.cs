using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum SnackbarDuration { Short, Long, Indefinite }

    /// <summary>
    /// Snackbars (Snackbar.kt). ComposeMaterial3Flags.isSnackbarStylingFixEnabled is false by
    /// default, so these are LegacyOneRowSnackbar / LegacyNewLineButtonSnackbar. Shown and hidden
    /// like SnackbarHost's FadeInFadeOutWithScale (scale 0.8 ↔ 1 FastSpatial, alpha FastEffects),
    /// closing itself after the SnackbarDuration.
    /// </summary>
    public static class M3Snackbars
    {
        const float MaxWidth = 600f;                 // ContainerMaxWidth
        const float HeightToFirstLine = 30f;
        const float HorizontalSpacing = 16f;
        const float HorizontalSpacingButtonSide = 8f;
        const float SeparateButtonExtraY = 2f;
        const float LegacyVerticalPadding = 6f;      // LegacySnackbarVerticalPadding
        const float TextEndExtraSpacing = 8f;
        const float LongButtonVerticalOffset = 12f;
        const float TouchTarget = 48f;               // minimumInteractiveComponentSize of the buttons

        public static RectTransform Create(Transform parent, M3Context ctx, string message, string action = null,
            bool withDismissAction = false, bool actionOnNewLine = false, float width = 344f,
            SnackbarDuration duration = SnackbarDuration.Short, bool expanded = true)
        {
            width = Mathf.Min(width, MaxWidth + HorizontalSpacing + HorizontalSpacingButtonSide);
            var shape = ShapeScale.Get(SnackbarTokens.ContainerShape);
            var inner = ctx.On(SnackbarTokens.ContainerColor);

            var root = M3Build.Rect("Snackbar", parent);
            var surface = M3Build.Rect("Surface", root);
            int level = Elevation.LevelOf(SnackbarTokens.ContainerElevation);
            var shadows = M3Build.Shadows(surface, shape, level, 0);
            for (int i = 0; i < shadows.Length; i++) M3Build.SetShape(shadows[i], M3Build.ShadowCell(shape, level, i, out _));
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(SnackbarTokens.ContainerColor));

            // buttons (TextButton with actionColor, IconButton(close) with dismissActionContentColor)
            RectTransform actionB = action != null
                ? M3Buttons.Create(surface, inner, action, ButtonStyle.Text, contentColor: SnackbarTokens.ActionLabelTextColor) : null;
            RectTransform dismissB = withDismissAction
                ? M3IconButtons.Create(surface, inner, "close", contentColor: SnackbarTokens.IconColor) : null;
            float aw = actionB != null ? actionB.sizeDelta.x : 0f;
            float dw = dismissB != null ? Mathf.Max(TouchTarget, dismissB.sizeDelta.x) : 0f;

            var text = M3Build.Text("Message", surface, message, SnackbarTokens.SupportingTextFont, ColorRef.Role(SnackbarTokens.SupportingTextColor), inner);
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;

            float height;
            if (actionOnNewLine && actionB != null)
            {
                // LegacyNewLineButtonSnackbar: Column(start 16, bottom 2) { text paddingFromBaseline(30, 12) + end 8; action row at the end }
                float textW = width - HorizontalSpacing - HorizontalSpacingButtonSide;
                var (th, firstBase, lastBase) = M3Build.MeasureText(text, message, textW);
                float textTop = HeightToFirstLine - firstBase;
                float textBlock = HeightToFirstLine + (lastBase - firstBase) + LongButtonVerticalOffset;
                Place(text.rectTransform, HorizontalSpacing, textTop, textW, th);
                float endPad = dismissB == null ? HorizontalSpacingButtonSide : 0f;
                float rowY = textBlock;
                float x = width - endPad;
                if (dismissB != null) { x -= dw; PlaceCentered(dismissB, x, rowY, dw, TouchTarget); }
                x -= aw;
                PlaceCentered(actionB, x, rowY, aw, TouchTarget);
                height = textBlock + TouchTarget + SeparateButtonExtraY;
            }
            else
            {
                // LegacyOneRowSnackbar
                float endPad = dismissB == null ? HorizontalSpacingButtonSide : 0f;
                float containerW = width - HorizontalSpacing - endPad;
                float extra = dismissB == null ? TextEndExtraSpacing : 0f;
                float textW = Mathf.Max(0f, containerW - aw - dw - extra);
                var (th, firstBase, lastBase) = M3Build.MeasureText(text, message, textW);
                float boxH = th + 2f * LegacyVerticalPadding;
                bool oneLine = Mathf.Approximately(firstBase, lastBase);
                float buttonsH = actionB != null || dismissB != null ? TouchTarget : 0f;
                float textY;
                if (oneLine)
                {
                    height = Mathf.Max(SnackbarTokens.SingleLineContainerHeight, buttonsH);
                    textY = (height - boxH) / 2f;
                }
                else
                {
                    textY = HeightToFirstLine - (LegacyVerticalPadding + firstBase);
                    height = Mathf.Max(SnackbarTokens.TwoLinesContainerHeight, textY + boxH);
                }
                Place(text.rectTransform, HorizontalSpacing, textY + LegacyVerticalPadding, textW, th);
                float dismissX = HorizontalSpacing + containerW - dw;
                if (dismissB != null) PlaceCentered(dismissB, dismissX, (height - TouchTarget) / 2f, dw, TouchTarget);
                if (actionB != null) PlaceCentered(actionB, dismissX - aw, (height - TouchTarget) / 2f, aw, TouchTarget);
            }

            surface.anchorMin = surface.anchorMax = new Vector2(0.5f, 0.5f);
            surface.pivot = new Vector2(0.5f, 0.5f); // graphicsLayer scale around the center
            surface.sizeDelta = new Vector2(width, height);
            surface.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;

            var popup = M3UdonBridge.Add<M3Popup>(root.gameObject);
            popup.target = surface;
            popup.group = surface.gameObject.AddComponent<CanvasGroup>();
            popup.expanded = expanded;
            popup.closedScale = 0.8f;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            popup.spatialDamping = spatial.DampingRatio;
            popup.spatialStiffness = spatial.Stiffness;
            popup.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            // SnackbarDuration.toMillis: Short 4000, Long 10000, Indefinite never
            popup.autoCloseSeconds = duration == SnackbarDuration.Short ? 4f : duration == SnackbarDuration.Long ? 10f : 0f;
            if (!expanded) surface.gameObject.SetActive(false);
            if (actionB != null) M3UdonBridge.Wire(actionB.GetComponent<Button>().onClick, popup, nameof(M3Popup._Close));
            if (dismissB != null) M3UdonBridge.Wire(dismissB.GetComponent<Button>().onClick, popup, nameof(M3Popup._Close));
            M3UdonBridge.Sync(popup);
            return root;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Places a button centered in a (touch target) slot.</summary>
        static void PlaceCentered(RectTransform rt, float x, float y, float w, float h)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w / 2f, -(y + h / 2f));
        }
    }
}
