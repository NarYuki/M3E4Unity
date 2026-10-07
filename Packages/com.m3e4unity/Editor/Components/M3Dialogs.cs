using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Basic dialogs (AlertDialog.kt: AlertDialogContent, AlertDialogFlowRow). The dialog sits
    /// centered on a scrim (ScrimTokens) that fills the parent; M3Popup on the returned root
    /// shows / hides both (alpha with FastEffects).
    /// </summary>
    public static class M3Dialogs
    {
        const float MinWidth = 280f;          // DialogMinWidth
        const float MaxWidth = 560f;          // DialogMaxWidth
        const float Padding = 24f;            // AlertDialogDefaults.dialogPadding
        const float IconBottom = 16f;         // IconPadding
        const float TitleBottom = 16f;        // TitlePadding
        const float TextBottom = 24f;         // AlertDialogDefaults.textPadding
        const float ButtonsSpacing = 8f;      // ButtonsMainAxisSpacing / ButtonsCrossAxisSpacing

        public static RectTransform Create(Transform parent, M3Context ctx, string title, string text,
            string confirm, string dismiss = null, string icon = null, bool expanded = true, bool scrim = true)
        {
            var root = M3Build.Rect("Dialog", parent);
            M3Build.Fill(root);
            var le0 = root.gameObject.AddComponent<LayoutElement>();
            le0.ignoreLayout = true;
            var content = M3Build.Rect("DialogContent", root);
            M3Build.Fill(content);
            if (scrim)
            {
                var s = M3Build.Shape("Scrim", content, ShapeCell.Create(ShapeKind.Fill),
                    ColorRef.Translucent(ScrimTokens.ContainerColor, ScrimTokens.ContainerOpacity));
                s.raycastTarget = true; // modal: blocks the UI behind
            }

            var surfaceRole = DialogTokens.ContainerColor;
            var inner = ctx.On(surfaceRole);
            var shape = ShapeScale.Get(DialogTokens.ContainerShape);

            // intrinsic width: the widest of title, unwrapped text and the button row, within 280..560
            float buttonsW = 0f;
            var surface = M3Build.Rect("Surface", content);
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(surfaceRole));
            var body = M3Build.Rect("Body", surface);
            var col = M3Build.Column(body, 0f);
            col.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            col.childForceExpandWidth = false;

            if (icon != null)
            {
                var box = M3Build.Rect("IconBox", body);
                var bc = M3Build.Column(box, 0f, TextAnchor.UpperCenter);
                bc.padding = new RectOffset(0, 0, 0, (int)IconBottom);
                M3Build.Icon("Icon", box, icon, DialogTokens.IconSize, ColorRef.Role(DialogTokens.IconColor), inner);
                box.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                bc.childAlignment = TextAnchor.UpperCenter;
            }
            TextMeshProUGUI titleT = null;
            if (title != null)
            {
                var box = M3Build.Rect("TitleBox", body);
                var bc = M3Build.Column(box, 0f, icon != null ? TextAnchor.UpperCenter : TextAnchor.UpperLeft);
                bc.padding = new RectOffset(0, 0, 0, (int)TitleBottom);
                box.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                titleT = M3Build.Text("Title", box, title, DialogTokens.HeadlineFont, ColorRef.Role(DialogTokens.HeadlineColor), inner);
                titleT.enableWordWrapping = true;
                titleT.alignment = icon != null ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
            }
            TextMeshProUGUI textT = null;
            if (text != null)
            {
                var box = M3Build.Rect("TextBox", body);
                var bc = M3Build.Column(box, 0f);
                bc.padding = new RectOffset(0, 0, 0, (int)TextBottom);
                bc.childForceExpandWidth = true;
                box.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                textT = M3Build.Text("Text", box, text, DialogTokens.SupportingTextFont, ColorRef.Role(DialogTokens.SupportingTextColor), inner);
                textT.enableWordWrapping = true;
                textT.alignment = TextAlignmentOptions.TopLeft;
            }

            // buttons: confirm first in code, but laid out after dismiss horizontally (flipped FlowRow), end-aligned
            var buttons = M3Build.Rect("Buttons", body);
            buttons.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform confirmB = M3Buttons.Create(buttons, inner, confirm, ButtonStyle.Text);
            RectTransform dismissB = dismiss != null ? M3Buttons.Create(buttons, inner, dismiss, ButtonStyle.Text) : null;
            buttonsW = confirmB.sizeDelta.x + (dismissB != null ? ButtonsSpacing + dismissB.sizeDelta.x : 0f);

            float contentW = buttonsW;
            if (titleT != null) contentW = Mathf.Max(contentW, titleT.GetPreferredValues(title).x);
            if (textT != null) contentW = Mathf.Max(contentW, textT.GetPreferredValues(text).x);
            float width = Mathf.Clamp(Mathf.Ceil(contentW) + 2f * Padding, MinWidth, MaxWidth);
            float innerW = width - 2f * Padding;

            bool stacked = dismissB != null && buttonsW > innerW;
            if (stacked)
            {
                // FlowRow wraps: confirm on top, both aligned to the end
                var v = M3Build.Column(buttons, ButtonsSpacing, TextAnchor.UpperRight);
                v.childControlWidth = false;
                v.childControlHeight = false;
            }
            else
            {
                var h = M3Build.Row(buttons, ButtonsSpacing, TextAnchor.MiddleRight);
                h.childControlWidth = false;
                h.childControlHeight = false;
                if (dismissB != null) dismissB.SetAsFirstSibling();
            }

            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.preferredWidth = width;
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(width, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
            // the column children are full width
            foreach (Transform c in body)
            {
                var rt = (RectTransform)c;
                var le = rt.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = innerW;
            }
            col.childControlWidth = true;
            col.childForceExpandWidth = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
            float height = LayoutUtility.GetPreferredHeight(body);
            body.sizeDelta = new Vector2(width, height);
            M3Build.Fill(body);
            surface.anchorMin = surface.anchorMax = new Vector2(0.5f, 0.5f);
            surface.pivot = new Vector2(0.5f, 0.5f);
            surface.anchoredPosition = Vector2.zero;
            surface.sizeDelta = new Vector2(width, height);

            var popup = M3UdonBridge.Add<M3Popup>(root.gameObject);
            popup.target = content;
            popup.group = content.gameObject.AddComponent<CanvasGroup>();
            popup.expanded = expanded;
            popup.closedScale = 1f;
            popup.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            if (!expanded) content.gameObject.SetActive(false);
            // both actions close the dialog (onDismissRequest / confirm in the samples)
            M3UdonBridge.Wire(confirmB.GetComponent<Button>().onClick, popup, nameof(M3Popup._Close));
            if (dismissB != null) M3UdonBridge.Wire(dismissB.GetComponent<Button>().onClick, popup, nameof(M3Popup._Close));
            M3UdonBridge.Sync(popup);
            return root;
        }
    }
}
