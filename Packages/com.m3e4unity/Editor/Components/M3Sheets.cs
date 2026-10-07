using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Modal bottom sheet (ModalBottomSheet.kt, SheetDefaults.kt): a SurfaceContainerLow sheet with
    /// ExtraLarge top corners, at most 640 dp wide, centered at the bottom of the parent over a
    /// scrim, with the drag handle (32 × 4, 22 dp vertical padding). The sheet slides with
    /// BottomSheetAnimationSpec (300 ms FastOutSlowIn); the scrim fades with DefaultEffects.
    /// Put the sheet content in the returned "Content" RectTransform (via the out parameter).
    /// </summary>
    public static class M3Sheets
    {
        const float SheetMaxWidth = 640f;            // BottomSheetDefaults.SheetMaxWidth
        const float DragHandleVerticalPadding = 22f;

        public static RectTransform ModalBottomSheet(Transform parent, M3Context ctx, float sheetHeight, out RectTransform content,
            bool open = true, bool dragHandle = true)
        {
            var host = M3Build.Rect("ModalBottomSheet", parent);
            M3Build.Fill(host);
            host.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var root = M3Build.Rect("SheetRoot", host);
            M3Build.Fill(root);
            var scrimRt = M3Build.Rect("ScrimLayer", root);
            M3Build.Fill(scrimRt);
            var scrim = M3Build.Shape("Scrim", scrimRt, ShapeCell.Create(ShapeKind.Fill),
                ColorRef.Translucent(ScrimTokens.ContainerColor, ScrimTokens.ContainerOpacity));
            scrim.raycastTarget = true;
            var scrimGroup = scrimRt.gameObject.AddComponent<CanvasGroup>();

            var bg = SheetBottomTokens.DockedContainerColor;
            var inner = ctx.On(bg);
            var sheet = M3Build.Rect("Sheet", root);
            float parentW = ((RectTransform)parent).rect.width;
            float w = parentW > 0f ? Mathf.Min(parentW, SheetMaxWidth) : SheetMaxWidth;
            sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.anchoredPosition = Vector2.zero;
            sheet.sizeDelta = new Vector2(w, sheetHeight);
            M3Build.Shape("Container", sheet, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(SheetBottomTokens.DockedContainerShape)), ColorRef.Role(bg))
                .raycastTarget = true;

            float top = 0f;
            if (dragHandle)
            {
                // BottomSheetDefaults.DragHandle: Surface(shape = extraLarge, color) of 32 × 4 with vertical padding 22
                float hw = SheetBottomTokens.DockedDragHandleWidth, hh = SheetBottomTokens.DockedDragHandleHeight;
                var handle = M3Build.Shape("DragHandle", sheet, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(ShapeRole.CornerExtraLarge)),
                    ColorRef.Role(SheetBottomTokens.DockedDragHandleColor), false);
                var hrt = (RectTransform)handle.transform;
                hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = new Vector2(0f, -DragHandleVerticalPadding);
                hrt.sizeDelta = new Vector2(hw, hh);
                top = DragHandleVerticalPadding * 2f + hh;
            }
            content = M3Build.Rect("Content", sheet);
            M3Build.Fill(content, 0f, 0f, 0f, top);

            var motion = M3UdonBridge.Add<M3Sheet>(host.gameObject);
            motion.sheet = sheet;
            motion.scrim = scrimGroup;
            motion.root = root.gameObject;
            motion.open = open;
            motion.hiddenOffset = new Vector2(0f, -sheetHeight);
            motion.durationSeconds = 0.3f;
            motion.easing = new Vector4(0.4f, 0f, 0.2f, 1f); // FastOutSlowInEasing
            motion.scrimStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
            // tapping the scrim dismisses (onDismissRequest)
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            M3UdonBridge.Wire(scrimButton.onClick, motion, nameof(M3Sheet._Close));
            if (!open) root.gameObject.SetActive(false);
            M3UdonBridge.Sync(motion);
            return host;
        }
    }
}
