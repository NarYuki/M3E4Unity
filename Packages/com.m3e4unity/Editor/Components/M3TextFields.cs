using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum TextFieldStyle { Filled, Outlined }

    /// <summary>
    /// Single-line filled and outlined text fields (TextField.kt / OutlinedTextField.kt with the
    /// TextFieldLayout / OutlinedTextFieldLayout measure policies of internal/TextFieldImpl.kt).
    /// Input is a TMP_InputField (VRChat shows its keyboard for it).
    /// </summary>
    public static class M3TextFields
    {
        const float MinWidth = 280f;      // TextFieldDefaults.MinWidth
        const float MinHeight = 56f;      // TextFieldDefaults.MinHeight
        const float Padding = 16f;        // TextFieldPadding
        const float LabelVertical = 8f;   // TextFieldWithLabelVerticalPadding
        const float LineHeight = 24f;     // MinTextLineHeight
        const float MinLabelLine = 16f;   // MinFocusedLabelLineHeight
        const float SupportingTop = 4f;   // SupportingTopPadding (supportingTextPadding top)
        const float InteractiveSize = 48f; // minimumInteractiveComponentSize of the icon boxes
        const float CursorThickness = 2f; // TextFieldImpl cursor brush width
        const float SelectionOpacity = 0.4f; // TextSelectionColors background alpha

        public static RectTransform Create(Transform parent, M3Context ctx, TextFieldStyle style, string label,
            string text = "", string placeholder = null, string supporting = null, string leadingIcon = null,
            string trailingIcon = null, bool error = false, bool enabled = true, float width = MinWidth)
        {
            bool outlined = style == TextFieldStyle.Outlined;
            var bg = ctx.Background;
            ColorRole container = FilledTextFieldTokens.ContainerColor;
            // colors are composited over what is behind the text (filled: the container)
            var under = outlined ? bg : container;
            ColorRef Dis(ColorRole role, float a) => ColorRef.Over(under, role, a);
            float iconPad = (InteractiveSize - SmallIconButtonTokens.IconSize) / 2f; // textFieldHorizontalIconPadding
            float top = outlined ? MinLabelLine / 2f : 0f; // topPaddingForLabelCutout (bodySmall line height / 2)

            var root = M3Build.Rect(outlined ? "OutlinedTextField" : "TextField", parent);
            var field = M3Build.Rect("Field", root);
            TopLeft(field, 0f, top, width, MinHeight);

            var entries = new List<SelectionColorEntry>();
            void Colors(Graphic g, ColorRef unfocused, ColorRef focused, ColorRef err, ColorRef disabled) =>
                entries.Add(new SelectionColorEntry { graphic = g, off = unfocused, on = focused, indeterminate = err, disabledOff = disabled, disabledOn = disabled, disabledIndeterminate = disabled });

            Image hit;
            Image outline = null;
            RectTransform indicator = null;
            RectTransform notch = null;
            Sprite[] outlineFrames = null;
            float outlineStep = 0.25f;
            if (!outlined)
            {
                // filled: container (disabledContainerColor = ContainerColor in TextFieldDefaults.colors) + indicator line
                hit = M3Build.Shape("Container", field, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(FilledTextFieldTokens.ContainerShape)),
                    ColorRef.Role(container));
                var line = M3Build.Shape("Indicator", field, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(FilledTextFieldTokens.ActiveIndicatorColor), false);
                line.raycastTarget = false;
                indicator = (RectTransform)line.transform;
                indicator.anchorMin = new Vector2(0f, 0f);
                indicator.anchorMax = new Vector2(1f, 0f);
                indicator.pivot = new Vector2(0.5f, 0f);
                indicator.anchoredPosition = Vector2.zero;
                indicator.sizeDelta = new Vector2(0f, TextFieldDefaults_Unfocused);
                Colors(line, ColorRef.Role(FilledTextFieldTokens.ActiveIndicatorColor), ColorRef.Role(FilledTextFieldTokens.FocusActiveIndicatorColor),
                    ColorRef.Role(FilledTextFieldTokens.ErrorActiveIndicatorColor),
                    Dis(FilledTextFieldTokens.DisabledActiveIndicatorColor, FilledTextFieldTokens.DisabledActiveIndicatorOpacity));
            }
            else
            {
                hit = M3Build.Shape("HitArea", field, ShapeCell.Create(ShapeKind.Fill), ColorRef.Clear);
                var shape = ShapeScale.Get(OutlinedTextFieldTokens.ContainerShape);
                float w0 = OutlinedTextFieldTokens.OutlineWidth, w1 = OutlinedTextFieldTokens.FocusOutlineWidth;
                int n = Mathf.RoundToInt((w1 - w0) / outlineStep) + 1;
                outlineFrames = new Sprite[n];
                for (int i = 0; i < n; i++)
                    outlineFrames[i] = M3ShapeAtlas.Get(ShapeCell.Create(ShapeKind.Stroke).WithShape(shape).WithStroke(w0 + i * outlineStep));
                outline = M3Build.Shape("Outline", field, ShapeCell.Create(ShapeKind.Stroke).WithShape(shape).WithStroke(w0), ColorRef.Role(OutlinedTextFieldTokens.OutlineColor));
                outline.raycastTarget = false;
                Colors(outline, ColorRef.Role(OutlinedTextFieldTokens.OutlineColor), ColorRef.Role(OutlinedTextFieldTokens.FocusOutlineColor),
                    ColorRef.Role(OutlinedTextFieldTokens.ErrorOutlineColor),
                    Dis(OutlinedTextFieldTokens.DisabledOutlineColor, OutlinedTextFieldTokens.DisabledOutlineOpacity));
                // outlineCutout: the border is clipped around the minimized label
                var cut = M3Build.Shape("Cutout", field, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(bg), false);
                cut.raycastTarget = false;
                notch = (RectTransform)cut.transform;
                notch.anchorMin = notch.anchorMax = new Vector2(0f, 1f);
                notch.pivot = new Vector2(0f, 0.5f);
                notch.anchoredPosition = new Vector2(Padding - 4f, 0f);
                notch.sizeDelta = new Vector2(0f, MinLabelLine);
            }

            // icon boxes (minimumInteractiveComponentSize), centered vertically
            float leadingW = 0f, trailingW = 0f;
            Graphic leadingG = null, trailingG = null;
            if (leadingIcon != null)
            {
                leadingW = InteractiveSize;
                leadingG = M3Build.Icon("LeadingIcon", field, leadingIcon, FilledTextFieldTokens.LeadingIconSize, ColorRef.Role(FilledTextFieldTokens.LeadingIconColor), ctx);
                TopLeft((RectTransform)leadingG.transform, (InteractiveSize - FilledTextFieldTokens.LeadingIconSize) / 2f, (MinHeight - FilledTextFieldTokens.LeadingIconSize) / 2f,
                    FilledTextFieldTokens.LeadingIconSize, FilledTextFieldTokens.LeadingIconSize);
            }
            if (trailingIcon != null)
            {
                trailingW = InteractiveSize;
                trailingG = M3Build.Icon("TrailingIcon", field, trailingIcon, FilledTextFieldTokens.TrailingIconSize, ColorRef.Role(FilledTextFieldTokens.TrailingIconColor), ctx);
                TopLeft((RectTransform)trailingG.transform, width - InteractiveSize + (InteractiveSize - FilledTextFieldTokens.TrailingIconSize) / 2f,
                    (MinHeight - FilledTextFieldTokens.TrailingIconSize) / 2f, FilledTextFieldTokens.TrailingIconSize, FilledTextFieldTokens.TrailingIconSize);
            }
            float startPad = leadingIcon != null ? Mathf.Max(0f, Padding - iconPad) : Padding;
            float endPad = trailingIcon != null ? Mathf.Max(0f, Padding - iconPad) : Padding;
            float textX = leadingW + startPad;
            float textW = width - textX - trailingW - endPad;
            bool hasLabel = !string.IsNullOrEmpty(label);

            // single line: the input sits below the minimized label (filled) or centered (outlined / no label)
            float textY = !outlined && hasLabel ? LabelVertical + MinLabelLine : (MinHeight - LineHeight) / 2f;

            // input field: viewport (RectMask2D) + text
            var viewport = M3Build.Rect("TextArea", field);
            TopLeft(viewport, textX, textY, textW, LineHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var inputText = M3Build.Text("Text", viewport, text ?? "", FilledTextFieldTokens.InputFont, ColorRef.Role(FilledTextFieldTokens.InputColor), ctx);
            M3Build.Fill((RectTransform)inputText.transform);
            inputText.alignment = TextAlignmentOptions.Left;
            inputText.raycastTarget = false;
            Colors(inputText, ColorRef.Role(FilledTextFieldTokens.InputColor), ColorRef.Role(FilledTextFieldTokens.FocusInputColor),
                ColorRef.Role(FilledTextFieldTokens.ErrorInputColor), Dis(FilledTextFieldTokens.DisabledInputColor, FilledTextFieldTokens.DisabledInputOpacity));

            TextMeshProUGUI ph = null;
            if (placeholder != null)
            {
                ph = M3Build.Text("Placeholder", field, placeholder, FilledTextFieldTokens.InputFont, ColorRef.Role(FilledTextFieldTokens.InputPlaceholderColor), ctx);
                TopLeft(ph.rectTransform, textX, textY, textW, LineHeight);
                ph.alignment = TextAlignmentOptions.Left;
                ph.raycastTarget = false;
                var phc = ColorRef.Role(FilledTextFieldTokens.InputPlaceholderColor);
                Colors(ph, phc, phc, phc, Dis(FilledTextFieldTokens.DisabledInputColor, FilledTextFieldTokens.DisabledInputOpacity));
            }

            // label: BodyLarge (expanded) ↔ BodySmall (minimized)
            TextMeshProUGUI lab = null;
            float smallSize = 0f, smallSpacing = 0f, smallWidth = 0f, largeSize = 0f, largeSpacing = 0f, largeWidth = 0f;
            if (hasLabel)
            {
                lab = M3Build.Text("Label", field, label, TypeRole.BodySmall, ColorRef.Role(FilledTextFieldTokens.LabelColor), ctx);
                smallSize = lab.fontSize; smallSpacing = lab.characterSpacing;
                smallWidth = Mathf.Ceil(lab.GetPreferredValues(label).x);
                M3Fonts.ApplyStyle(lab, FilledTextFieldTokens.LabelFont, ctx.Theme);
                largeSize = lab.fontSize; largeSpacing = lab.characterSpacing;
                largeWidth = Mathf.Ceil(lab.GetPreferredValues(label).x);
                lab.alignment = TextAlignmentOptions.Left;
                lab.raycastTarget = false;
                lab.rectTransform.sizeDelta = new Vector2(Mathf.Max(largeWidth, smallWidth), LineHeight);
                Colors(lab, ColorRef.Role(FilledTextFieldTokens.LabelColor), ColorRef.Role(FilledTextFieldTokens.FocusLabelColor),
                    ColorRef.Role(FilledTextFieldTokens.ErrorLabelColor), Dis(FilledTextFieldTokens.DisabledLabelColor, FilledTextFieldTokens.DisabledLabelOpacity));
            }

            if (leadingG != null)
                Colors(leadingG, ColorRef.Role(FilledTextFieldTokens.LeadingIconColor), ColorRef.Role(FilledTextFieldTokens.FocusLeadingIconColor),
                    ColorRef.Role(FilledTextFieldTokens.ErrorLeadingIconColor), Dis(FilledTextFieldTokens.DisabledLeadingIconColor, FilledTextFieldTokens.DisabledLeadingIconOpacity));
            if (trailingG != null)
                Colors(trailingG, ColorRef.Role(FilledTextFieldTokens.TrailingIconColor), ColorRef.Role(FilledTextFieldTokens.FocusTrailingIconColor),
                    ColorRef.Role(FilledTextFieldTokens.ErrorTrailingIconColor), Dis(FilledTextFieldTokens.DisabledTrailingIconColor, FilledTextFieldTokens.DisabledTrailingIconOpacity));

            // supporting text: supportingTextPadding(start 16, top 4, end 16)
            float height = top + MinHeight;
            if (supporting != null)
            {
                var sup = M3Build.Text("SupportingText", root, supporting, FilledTextFieldTokens.SupportingFont, ColorRef.Role(FilledTextFieldTokens.SupportingColor), ctx);
                sup.alignment = TextAlignmentOptions.TopLeft;
                sup.enableWordWrapping = true;
                sup.raycastTarget = false;
                float sw = width - 2f * Padding;
                float sh = Mathf.Max(MinLabelLine, Mathf.Ceil(sup.GetPreferredValues(supporting, sw, 0f).y));
                TopLeft(sup.rectTransform, Padding, height + SupportingTop, sw, sh);
                height += SupportingTop + sh;
                // background = the surface behind the field, not the container
                Colors(sup, ColorRef.Role(FilledTextFieldTokens.SupportingColor), ColorRef.Role(FilledTextFieldTokens.FocusSupportingColor),
                    ColorRef.Role(FilledTextFieldTokens.ErrorSupportingColor), ColorRef.Over(bg, FilledTextFieldTokens.DisabledSupportingColor, FilledTextFieldTokens.DisabledSupportingOpacity));
            }

            // the TMP_InputField on the field (its target graphic is the container / hit area)
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = inputText;
            input.targetGraphic = hit;
            input.transition = Selectable.Transition.None;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = inputText.font;
            input.pointSize = inputText.fontSize;
            input.caretWidth = Mathf.RoundToInt(CursorThickness);
            input.customCaretColor = true;
            input.richText = false;
            input.text = text ?? "";
            input.interactable = enabled;
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            var tf = M3UdonBridge.Add<M3TextField>(root.gameObject);
            tf.input = input;
            tf.isError = error;
            tf.label = lab;
            tf.labelRect = lab != null ? lab.rectTransform : null;
            tf.placeholder = ph;
            tf.indicator = indicator;
            tf.outline = outline;
            tf.outlineFrames = outlineFrames;
            tf.outlineStep = outlineStep;
            tf.notch = notch;
            tf.widthUnfocused = outlined ? OutlinedTextFieldTokens.OutlineWidth : FilledTextFieldTokens.ActiveIndicatorHeight;
            tf.widthFocused = outlined ? OutlinedTextFieldTokens.FocusOutlineWidth : FilledTextFieldTokens.FocusActiveIndicatorHeight;
            tf.alwaysMinimized = !hasLabel;
            if (hasLabel)
            {
                tf.minimizedScale = smallSize / largeSize;
                tf.labelExpandedWidth = largeWidth; tf.labelMinimizedWidth = smallWidth;
                tf.notchPadding = 4f; // OutlinedTextFieldInnerPadding
                // expanded: centered vertically after the leading icon; minimized: filled at the top padding,
                // outlined centered on the border at the start padding
                // (positions are box centers: expanded 24 dp box centered in the field)
                tf.labelExpanded = new Vector2(textX, MinHeight / 2f);
                tf.labelMinimized = outlined ? new Vector2(Padding, 0f) : new Vector2(textX, LabelVertical + MinLabelLine / 2f);
            }
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            var effects = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects);
            tf.spatialDamping = spatial.DampingRatio;
            tf.spatialStiffness = spatial.Stiffness;
            tf.effectsStiffness = effects.Stiffness;
            M3UdonBridge.Wire(input.onSelect, tf, nameof(M3TextField._Focus));
            M3UdonBridge.Wire(input.onDeselect, tf, nameof(M3TextField._Blur));
            M3UdonBridge.Wire(input.onValueChanged, tf, nameof(M3TextField._Changed));

            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.entries = entries;
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(FilledTextFieldTokens.CaretColor),
                ColorRef.Role(FilledTextFieldTokens.ErrorFocusCaretColor),
                ColorRef.Translucent(ColorRole.Primary, SelectionOpacity),
            };

            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = width;
            le.minHeight = le.preferredHeight = height;
            M3UdonBridge.Sync(tf);
            return root;
        }

        const float TextFieldDefaults_Unfocused = 1f; // TextFieldDefaults.UnfocusedIndicatorThickness

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
