using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Plain and rich tooltips (Tooltip.kt, internal/BasicTooltip.kt) shown above an anchor
    /// (TooltipAnchorPosition.Above, SpacingBetweenTooltipAndAnchor). Hovering the anchor shows the
    /// tooltip (PointerEventType.Enter → show, Exit → dismiss) with scale 0.8 ↔ 1 (FastSpatial)
    /// and alpha (FastEffects). The tooltip is a child of the anchor on its own sorting canvas so
    /// it draws above the following siblings.
    /// </summary>
    public static class M3Tooltips
    {
        const float Spacing = 4f;               // SpacingBetweenTooltipAndAnchor
        const float MinHeight = 24f;            // TooltipMinHeight
        const float MinWidth = 40f;             // TooltipMinWidth
        const float PlainPadH = 8f, PlainPadV = 4f; // PlainTooltipContentPadding
        const float PlainMaxWidth = 200f;       // TooltipDefaults.plainTooltipMaxWidth
        const float RichMaxWidth = 320f;        // TooltipDefaults.richTooltipMaxWidth
        const float RichPadH = 16f;             // RichTooltipHorizontalPadding
        const float HeightToSubheadFirstLine = 28f;
        const float HeightFromSubheadToTextFirstLine = 24f;
        const float TextBottomPadding = 16f;
        const float ActionLabelMinHeight = 36f;
        const float ActionLabelBottomPadding = 8f;
        const float TouchTarget = 48f;
        static readonly Vector2 CaretSize = new Vector2(16f, 8f); // TooltipDefaults.caretSize

        public static RectTransform Plain(RectTransform anchor, M3Context ctx, string text, bool caret = false, bool expanded = false)
        {
            var inner = ctx.On(PlainTooltipTokens.ContainerColor);
            var root = Root(anchor, "PlainTooltip");
            var surface = M3Build.Rect("Surface", root);
            var shape = ShapeScale.Get(PlainTooltipTokens.ContainerShape);
            if (caret) Caret(surface, PlainTooltipTokens.ContainerColor);
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(PlainTooltipTokens.ContainerColor));
            var t = M3Build.Text("Text", surface, text, PlainTooltipTokens.SupportingTextFont, ColorRef.Role(PlainTooltipTokens.SupportingTextColor), inner);
            t.enableWordWrapping = true;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            float maxText = PlainMaxWidth - 2f * PlainPadH;
            float tw = Mathf.Min(Mathf.Ceil(t.GetPreferredValues(text).x), maxText);
            var (th, _, _) = M3Build.MeasureText(t, text, tw);
            float w = Mathf.Max(MinWidth, tw + 2f * PlainPadH);
            float h = Mathf.Max(MinHeight, th + 2f * PlainPadV);
            Place(t.rectTransform, PlainPadH, (h - th) / 2f, tw, th);
            return Finish(root, surface, ctx, w, h, expanded, anchor);
        }

        public static RectTransform Rich(RectTransform anchor, M3Context ctx, string title, string text, string action = null,
            bool caret = false, bool expanded = false)
        {
            var bg = RichTooltipTokens.ContainerColor;
            var inner = ctx.On(bg);
            var root = Root(anchor, "RichTooltip");
            var surface = M3Build.Rect("Surface", root);
            var shape = ShapeScale.Get(RichTooltipTokens.ContainerShape);
            int level = Elevation.LevelOf(RichTooltipTokens.ContainerElevation);
            var shadows = M3Build.Shadows(surface, shape, level, 0);
            for (int i = 0; i < shadows.Length; i++) M3Build.SetShape(shadows[i], M3Build.ShadowCell(shape, level, i, out _));
            if (caret) Caret(surface, bg);
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(bg));

            TextMeshProUGUI titleT = null, textT;
            if (title != null)
            {
                titleT = M3Build.Text("Title", surface, title, RichTooltipTokens.SubheadFont, ColorRef.Role(RichTooltipTokens.SubheadColor), inner);
                titleT.enableWordWrapping = true; titleT.alignment = TextAlignmentOptions.TopLeft; titleT.raycastTarget = false;
            }
            textT = M3Build.Text("Text", surface, text, RichTooltipTokens.SupportingTextFont, ColorRef.Role(RichTooltipTokens.SupportingTextColor), inner);
            textT.enableWordWrapping = true; textT.alignment = TextAlignmentOptions.TopLeft; textT.raycastTarget = false;
            RectTransform actionB = action != null
                ? M3Buttons.Create(surface, inner, action, ButtonStyle.Text, contentColor: RichTooltipTokens.ActionLabelTextColor) : null;

            // sizeIn(40..320): the widest child, wrapped at the max width
            float maxInner = RichMaxWidth - 2f * RichPadH;
            float want = Mathf.Ceil(textT.GetPreferredValues(text).x);
            if (titleT != null) want = Mathf.Max(want, Mathf.Ceil(titleT.GetPreferredValues(title).x));
            if (actionB != null) want = Mathf.Max(want, actionB.sizeDelta.x);
            float iw = Mathf.Min(want, maxInner);
            float w = Mathf.Max(MinWidth, iw + 2f * RichPadH);

            float y = 0f;
            if (titleT != null)
            {
                // paddingFromBaseline(top = HeightToSubheadFirstLine)
                var (th, first, _) = M3Build.MeasureText(titleT, title, iw);
                Place(titleT.rectTransform, RichPadH, HeightToSubheadFirstLine - first, iw, th);
                y = HeightToSubheadFirstLine - first + th;
            }
            var (xh, xfirst, _) = M3Build.MeasureText(textT, text, iw);
            if (titleT == null && actionB == null)
            {
                Place(textT.rectTransform, RichPadH, PlainPadV, iw, xh);
                y = PlainPadV + xh + PlainPadV;
            }
            else
            {
                // paddingFromBaseline(top = 24) + padding(bottom = 16)
                float top = y + HeightFromSubheadToTextFirstLine - xfirst;
                Place(textT.rectTransform, RichPadH, top, iw, xh);
                y = top + xh + TextBottomPadding;
            }
            if (actionB != null)
            {
                // requiredHeightIn(min = 36) + padding(bottom = 8); the TextButton measures 48 (touch target)
                float slot = Mathf.Max(ActionLabelMinHeight, TouchTarget);
                var le = actionB.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;
                actionB.anchorMin = actionB.anchorMax = new Vector2(0f, 1f);
                actionB.pivot = new Vector2(0f, 0.5f);
                actionB.anchoredPosition = new Vector2(RichPadH, -(y + slot / 2f));
                y += slot + ActionLabelBottomPadding;
            }
            float h = Mathf.Max(MinHeight, y);
            var r = Finish(root, surface, ctx, w, h, expanded, anchor);
            // the action is clickable: the tooltip canvas needs its own raycaster
            if (actionB != null) root.gameObject.AddComponent<GraphicRaycaster>();
            return r;
        }

        static RectTransform Root(RectTransform anchor, string name)
        {
            var root = M3Build.Rect(name, anchor);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
            return root;
        }

        /// <summary>DefaultTooltipCaretShape (16 × 8 triangle) at the bottom center: the lower half of a 45° square.</summary>
        static void Caret(RectTransform surface, ColorRole color)
        {
            float side = CaretSize.x / Mathf.Sqrt(2f); // the diagonal equals the caret width; half the height is CaretSize.y
            var img = M3Build.Shape("Caret", surface, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(color), false);
            img.raycastTarget = false;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(side, side);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        static RectTransform Finish(RectTransform root, RectTransform surface, M3Context ctx, float w, float h, bool expanded, RectTransform anchor)
        {
            // abovePositioning: centered over the anchor, bottom edge Spacing above its top
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, Spacing);
            root.sizeDelta = new Vector2(w, h);
            M3Build.Fill(surface);
            surface.pivot = new Vector2(0.5f, 0.5f);

            var popup = M3UdonBridge.Add<M3Popup>(root.gameObject);
            popup.target = surface;
            popup.group = surface.gameObject.AddComponent<CanvasGroup>();
            popup.group.blocksRaycasts = expanded;
            popup.expanded = expanded;
            popup.closedScale = 0.8f;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            popup.spatialDamping = spatial.DampingRatio;
            popup.spatialStiffness = spatial.Stiffness;
            popup.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            if (!expanded) surface.gameObject.SetActive(false);

            // hover on the anchor shows / dismisses (BasicTooltip: PointerEventType.Enter / Exit)
            M3UdonBridge.WirePointer(anchor.gameObject, EventTriggerType.PointerEnter, popup, nameof(M3Popup._Open));
            M3UdonBridge.WirePointer(anchor.gameObject, EventTriggerType.PointerExit, popup, nameof(M3Popup._Close));
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
    }
}
