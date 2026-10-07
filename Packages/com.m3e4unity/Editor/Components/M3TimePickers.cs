using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Which contents a time picker dialog switches between (the samples' mode toggles).</summary>
    public enum TimePickerDialogModes
    {
        /// <summary>The clock picker only.</summary>
        Picker,
        /// <summary>Clock picker ↔ time input (DisplayModeToggle).</summary>
        PickerAndInput,
        /// <summary>Time scroll ↔ time input (ScrollDisplayModeToggle); vibrant only.</summary>
        ScrollAndInput,
    }

    /// <summary>
    /// Time pickers (TimePicker.kt, 12-hour, portrait): the clock picker (VerticalTimePicker:
    /// VerticalClockDisplay + ClockFace), the time input (TimeInputImpl) and the time scroll
    /// (TimeScrollImpl with ScrollField), each standard or vibrant (TimePickerShapes given:
    /// TimePickerDefaults.vibrantColors, the Vibrant* sizes), and the dialogs (TimePickerDialog /
    /// VibrantTimePickerDialog, TimePickerCustomLayout / VibrantTimePickerCustomLayout) with a mode
    /// toggle. The period toggle is the updated toggle (ComposeMaterial3Flags.isUpdatedTimepickerToggleEnabled).
    /// </summary>
    public static class M3TimePickers
    {
        const float DisplaySeparatorWidth = 24f, VibrantSeparatorWidth = 16f;
        const float PeriodTogglePaddingSmall = 4f, VibrantPeriodTogglePadding = 8f, VibrantPeriodToggleLargePadding = 16f;
        const float ClockDisplayBottomMargin = 36f, ClockFaceBottomMargin = 24f, VibrantVerticalTimePickerGap = 12f;
        const float VibrantTimeFieldWidth = 100f, VibrantTimeFieldHeight = 120f;
        const float VibrantPeriodToggleWidth = 56f, VibrantPeriodToggleHeight = 120f;
        const float SupportLabelTop = 7f;
        const float NumberBox = 48f;                // MinimumInteractiveSize
        const float ScrollFieldHeight = 200f;       // ScrollFieldDefaults.ScrollFieldHeight
        static readonly int[] Hours = { 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        static readonly int[] Minutes = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 };

        // ---- dialog ------------------------------------------------------------------------------

        public static RectTransform Dialog(Transform parent, M3Context ctx, int hour = 9, int minute = 30, bool vibrant = false,
            TimePickerDialogModes modes = TimePickerDialogModes.PickerAndInput, int initialMode = -1)
        {
            if (modes == TimePickerDialogModes.ScrollAndInput) vibrant = true; // TimeScroll is vibrant only
            var bg = vibrant ? ColorRole.SurfaceContainer : DialogTokens.ContainerColor;
            var shape = vibrant ? ShapeScale.Get(ShapeRole.CornerExtraLarge) : ShapeScale.Get(DialogTokens.ContainerShape);
            var inner = ctx.On(bg);
            int otherMode = modes == TimePickerDialogModes.ScrollAndInput ? 2 : 0;
            int mode = initialMode >= 0 ? initialMode : otherMode;

            var root = M3Build.Rect("TimePickerDialog", parent);
            var surface = M3Build.Rect("Surface", root);
            surface.anchorMin = surface.anchorMax = new Vector2(0.5f, 0.5f);
            surface.pivot = new Vector2(0.5f, 0.5f);
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(shape), ColorRef.Role(bg));
            var dialog = M3UdonBridge.Add<M3TimePickerDialog>(root.gameObject);
            dialog.hour = hour; dialog.minute = minute;
            dialog.mode = mode; dialog.otherMode = otherMode;

            // TimePickerCustomLayout: title at 24 / 24, content, actions, bottom 24;
            // VibrantTimePickerCustomLayout: content at top 12, 12 to the actions, bottom 12, side padding 12
            float pad = vibrant ? 12f : 24f;
            float top = vibrant ? 12f : 24f;
            float titleBlock = 0f;
            TextMeshProUGUI title = null;
            if (!vibrant)
            {
                title = M3Build.Text("Title", surface, "Select Time", TypeRole.LabelMedium, ColorRef.Role(M3Colors.ContentFor(bg)), inner);
                title.alignment = TextAlignmentOptions.TopLeft;
                var (th, _, _) = M3Build.MeasureText(title, "Select Time", 200f);
                TopLeft(title.rectTransform, 24f, 24f, 240f, th);
                titleBlock = th + 20f; // Title(modifier = padding(bottom = 20))
            }
            float contentTop = top + titleBlock;
            float actionsH = 48f;
            float contentActions = vibrant ? 12f : 0f;
            float bottom = vibrant ? 12f : 24f;
            dialog.chromeHeight = contentTop + contentActions + actionsH + bottom;
            dialog.horizontalPadding = pad;
            dialog.title = title;
            dialog.titles = new[] { "Select Time", "Enter Time", "Select Time" }; // m3c_time_picker / input / scroll _dialog_title

            var widths = new float[3];
            var heights = new float[3];
            var pages = new GameObject[3];
            if (modes != TimePickerDialogModes.ScrollAndInput)
            {
                var page = Page(surface, "Picker", contentTop);
                var picker = ClockContent(page, inner, vibrant, hour, minute, out widths[0], out heights[0]);
                picker.changeListener = dialog;
                dialog.picker = picker;
                pages[0] = page.gameObject;
                M3UdonBridge.Sync(picker);
            }
            if (modes != TimePickerDialogModes.Picker)
            {
                var page = Page(surface, "Input", contentTop);
                var input = InputContent(page, inner, vibrant, hour, minute, out widths[1], out heights[1]);
                input.changeListener = dialog;
                dialog.input = input;
                pages[1] = page.gameObject;
                M3UdonBridge.Sync(input);
            }
            if (modes == TimePickerDialogModes.ScrollAndInput)
            {
                var page = Page(surface, "Scroll", contentTop);
                var scroll = ScrollContent(page, inner, hour, minute, out widths[2], out heights[2]);
                scroll.changeListener = dialog;
                dialog.scrollPicker = scroll;
                pages[2] = page.gameObject;
                M3UdonBridge.Sync(scroll);
            }
            for (int i = 0; i < 3; i++) if (pages[i] != null) ((RectTransform)pages[i].transform).sizeDelta = new Vector2(widths[i], heights[i]);
            dialog.pages = pages;
            dialog.contentWidths = widths;
            dialog.contentHeights = heights;
            dialog.surface = surface;

            // actions: Row(spacedBy 8) { modeToggle, weight, dismiss, confirm }
            float ay = bottom + actionsH / 2f;
            if (modes != TimePickerDialogModes.Picker)
            {
                var toggle = M3IconButtons.Create(surface, inner, mode == 1 ? "schedule" : "keyboard", contentColor: ColorRole.OnSurfaceVariant);
                var tl = toggle.GetComponent<LayoutElement>(); if (tl != null) tl.ignoreLayout = true;
                toggle.anchorMin = toggle.anchorMax = new Vector2(0f, 0f);
                toggle.pivot = new Vector2(0.5f, 0.5f);
                toggle.anchoredPosition = new Vector2(pad + 24f, ay);
                M3UdonBridge.Wire(toggle.GetComponent<Button>().onClick, dialog, nameof(M3TimePickerDialog._ToggleMode));
                dialog.toggleIcon = FindDeep(toggle, "Icon").GetComponent<TextMeshProUGUI>();
                // DisplayModeToggle: Keyboard (picker) / Schedule (input); ScrollDisplayModeToggle: Keyboard (scroll) / SwipeVertical (input)
                dialog.toggleGlyphs = new[] { M3Icons.Glyph("keyboard"), M3Icons.Glyph(otherMode == 2 ? "swipe_vertical" : "schedule"), M3Icons.Glyph("keyboard") };
            }
            float bx = -pad;
            foreach (var label in new[] { "OK", "Cancel" })
            {
                var b = M3Buttons.Create(surface, inner, label, ButtonStyle.Text);
                var bl = b.GetComponent<LayoutElement>(); if (bl != null) bl.ignoreLayout = true;
                b.anchorMin = b.anchorMax = new Vector2(1f, 0f);
                b.pivot = new Vector2(1f, 0.5f);
                b.anchoredPosition = new Vector2(bx, ay);
                bx -= b.sizeDelta.x + 8f;
            }

            // the root reserves the largest mode
            float maxW = 0f, maxH = 0f;
            for (int i = 0; i < 3; i++) { maxW = Mathf.Max(maxW, widths[i]); maxH = Mathf.Max(maxH, heights[i] + (i == 1 && !vibrant ? 0f : 0f)); }
            if (modes != TimePickerDialogModes.Picker && vibrant) maxH = Mathf.Max(maxH, VibrantTimeFieldHeight + SupportLabelTop + 32f);
            var rootSize = new Vector2(maxW + 2f * pad, dialog.chromeHeight + maxH);
            root.sizeDelta = rootSize;
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = rootSize.x;
            le.preferredHeight = le.minHeight = rootSize.y;
            surface.sizeDelta = new Vector2(widths[mode] + 2f * pad, dialog.chromeHeight + heights[mode]);
            foreach (var p in pages) if (p != null) p.SetActive(p == pages[mode]);
            M3UdonBridge.Sync(dialog);
            return root;
        }

        static RectTransform Page(RectTransform surface, string name, float top)
        {
            var page = M3Build.Rect(name, surface);
            page.anchorMin = page.anchorMax = new Vector2(0.5f, 1f);
            page.pivot = new Vector2(0.5f, 1f);
            page.anchoredPosition = new Vector2(0f, -top);
            return page;
        }

        // ---- clock picker ------------------------------------------------------------------------

        /// <summary>A standalone clock picker (TimePicker composable); vibrant = with TimePickerShapes.</summary>
        public static RectTransform Picker(Transform parent, M3Context ctx, int hour = 9, int minute = 30, bool vibrant = false)
        {
            var root = M3Build.Rect("TimePicker", parent);
            ClockContent(root, ctx, vibrant, hour, minute, out var w, out var h);
            Size(root, w, h);
            return root;
        }

        static M3TimePicker ClockContent(RectTransform page, M3Context ctx, bool vibrant, int hour, int minute, out float width, out float height)
        {
            float selW = vibrant ? VibrantTimeFieldWidth : TimePickerTokens.TimeSelectorContainerWidth;
            float selH = vibrant ? VibrantTimeFieldHeight : TimePickerTokens.TimeSelectorContainerHeight;
            float sepW = vibrant ? VibrantSeparatorWidth : DisplaySeparatorWidth;
            float sepH = vibrant ? VibrantTimeFieldHeight : TimePickerTokens.PeriodSelectorVerticalContainerHeight;
            float perPad = vibrant ? VibrantPeriodToggleLargePadding : PeriodTogglePaddingSmall;
            float perW = vibrant ? VibrantPeriodToggleWidth : TimePickerTokens.PeriodSelectorVerticalContainerWidth;
            float perH = vibrant ? VibrantPeriodToggleHeight : TimePickerTokens.PeriodSelectorVerticalContainerHeight;
            float perGap = vibrant ? VibrantPeriodTogglePadding : PeriodTogglePaddingSmall;
            float gap = vibrant ? VibrantVerticalTimePickerGap : ClockDisplayBottomMargin;
            float bottomGap = vibrant ? 0f : ClockFaceBottomMargin;
            float dial = TimePickerTokens.ClockDialContainerSize;
            float rowW = selW * 2f + sepW + perPad + perW;
            width = Mathf.Max(rowW, dial);
            height = selH + gap + dial + bottomGap;

            var picker = M3UdonBridge.Add<M3TimePicker>(page.gameObject);
            picker.hour = hour; picker.minute = minute;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            picker.spatialDamping = spatial.DampingRatio;
            picker.spatialStiffness = spatial.Stiffness;
            picker.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;

            // VerticalClockDisplay: Row(Center) { hour, separator, minute, padding, period toggle }
            float rx = (width - rowW) / 2f;
            var hourSel = Selector(page, ctx, vibrant, out var hourText, rx, 0f, selW, selH, true);
            Separator(page, ctx, TimePickerTokens.TimeSelectorLabelTextFont, rx + selW, 0f, sepW, sepH);
            var minuteSel = Selector(page, ctx, vibrant, out var minuteText, rx + selW + sepW, 0f, selW, selH, false);
            hourSel.exclusiveWith = minuteSel.exclusiveWith = new[] { hourSel, minuteSel };
            M3UdonBridge.Wire(hourSel.GetComponent<Button>().onClick, picker, nameof(M3TimePicker._SelHour));
            M3UdonBridge.Wire(minuteSel.GetComponent<Button>().onClick, picker, nameof(M3TimePicker._SelMinute));
            float px = rx + selW * 2f + sepW + perPad;
            float itemH = (perH - perGap) / 2f;
            var am = PeriodItem(page, ctx, "AM", px, 0f, perW, itemH, hour < 12);
            var pm = PeriodItem(page, ctx, "PM", px, itemH + perGap, perW, itemH, hour >= 12);
            am.exclusiveWith = pm.exclusiveWith = new[] { am, pm };
            M3UdonBridge.Wire(am.GetComponent<Button>().onClick, picker, nameof(M3TimePicker._AM));
            M3UdonBridge.Wire(pm.GetComponent<Button>().onClick, picker, nameof(M3TimePicker._PM));
            picker.hourText = hourText; picker.minuteText = minuteText;
            picker.hourSelector = hourSel; picker.minuteSelector = minuteSel;
            picker.amToggle = am; picker.pmToggle = pm;
            foreach (var it in new[] { hourSel, minuteSel, am, pm }) M3UdonBridge.Sync(it);

            // ClockFace (vibrant: clockDialColor = SurfaceContainerLowest)
            float dialY = selH + gap;
            var dialRt = M3Build.Rect("ClockFace", page);
            TopLeft(dialRt, (width - dial) / 2f, dialY, dial, dial);
            M3Build.Shape("Dial", dialRt, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full),
                ColorRef.Role(vibrant ? ColorRole.SurfaceContainerLowest : TimePickerTokens.ClockDialColor));
            var hourFace = Face(dialRt, ctx, Hours, "Hours", false, picker, "_H");
            var minuteFace = Face(dialRt, ctx, Minutes, "Minutes", false, picker, "_M");
            var lineImg = M3Build.Shape("Track", dialRt, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(TimePickerTokens.ClockDialSelectorTrackContainerColor), false);
            lineImg.raycastTarget = false;
            var lineRt = (RectTransform)lineImg.transform;
            lineRt.anchorMin = lineRt.anchorMax = new Vector2(0.5f, 0.5f);
            lineRt.pivot = new Vector2(0f, 0.5f);
            lineRt.anchoredPosition = Vector2.zero;
            lineRt.sizeDelta = new Vector2(0f, TimePickerTokens.ClockDialSelectorTrackContainerWidth);
            var dot = M3Build.Shape("CenterDot", dialRt, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Role(TimePickerTokens.ClockDialSelectorCenterContainerColor), false);
            dot.raycastTarget = false;
            var dotRt = (RectTransform)dot.transform;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = Vector2.one * TimePickerTokens.ClockDialSelectorCenterContainerSize;
            // the handle: a Primary circle masking an OnPrimary copy of the faces (drawSelector's XOR)
            var handleImg = M3Build.Shape("Selector", dialRt, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Role(TimePickerTokens.ClockDialSelectorHandleContainerColor), false);
            handleImg.raycastTarget = false;
            var handle = (RectTransform)handleImg.transform;
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = Vector2.one * TimePickerTokens.ClockDialSelectorHandleContainerSize;
            handle.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var handleContent = M3Build.Rect("Content", handle);
            handleContent.anchorMin = handleContent.anchorMax = new Vector2(0.5f, 0.5f);
            handleContent.sizeDelta = new Vector2(dial, dial);
            var hourSelFace = Face(handleContent, ctx, Hours, "HoursSelected", true, null, null);
            var minuteSelFace = Face(handleContent, ctx, Minutes, "MinutesSelected", true, null, null);
            picker.dialSize = dial;
            picker.handleSize = TimePickerTokens.ClockDialSelectorHandleContainerSize;
            picker.handle = handle; picker.handleContent = handleContent; picker.line = lineRt;
            picker.hourFace = hourFace; picker.minuteFace = minuteFace;
            picker.hourFaceSelected = hourSelFace; picker.minuteFaceSelected = minuteSelFace;
            minuteFace.alpha = 0f; minuteSelFace.alpha = 0f;
            return picker;
        }

        /// <summary>TimeSelector: Surface(selected, onClick) with the time field shape; vibrant adds a 2 dp Primary border when selected.</summary>
        static M3Interactive Selector(RectTransform parent, M3Context ctx, bool vibrant, out TextMeshProUGUI text, float x, float y, float w, float h, bool selected)
        {
            var rt = M3Build.Rect("TimeSelector", parent);
            TopLeft(rt, x, y, w, h);
            float r = vibrant ? ShapeScale.Get(ShapeRole.CornerLarge).TopStart : ShapeScale.Get(TimePickerTokens.TimeSelectorContainerShape).TopStart;
            var p = new M3Pressable(rt, ctx, new PressableStyle
            {
                Container = vibrant ? ColorRole.SurfaceContainerLowest : TimePickerTokens.TimeSelectorUnselectedContainerColor,
                ContainerChecked = vibrant ? ColorRole.SurfaceContainerLowest : TimePickerTokens.TimeSelectorSelectedContainerColor,
                Content = vibrant ? ColorRole.OnSurface : TimePickerTokens.TimeSelectorUnselectedLabelTextColor,
                ContentChecked = vibrant ? ColorRole.Primary : TimePickerTokens.TimeSelectorSelectedLabelTextColor,
                Toggle = true, Selected = selected,
                RestShape = r, PressedShape = r, CheckedShape = r, MaxShape = r,
            });
            if (vibrant)
            {
                var border = M3Build.Shape("Border", rt, ShapeCell.Create(ShapeKind.Stroke).WithRadius(r).WithStroke(2f), ColorRef.Role(ColorRole.Primary));
                border.raycastTarget = false;
                M3Pressable.Ignore(border);
                p.Spec.ShowWhenSelected.Add(border.gameObject);
            }
            text = M3Build.Text("Value", rt, "00", TimePickerTokens.TimeSelectorLabelTextFont,
                ColorRef.Role(vibrant ? ColorRole.OnSurface : TimePickerTokens.TimeSelectorUnselectedLabelTextColor), ctx);
            M3Build.Fill(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            p.Content(text);
            return p.Finish();
        }

        /// <summary>DisplaySeparator: ":" centered in its box, offset(y = -4.dp).</summary>
        static void Separator(RectTransform parent, M3Context ctx, TypeRole font, float x, float y, float w, float h)
        {
            var sep = M3Build.Text("Separator", parent, ":", font, ColorRef.Role(TimeInputTokens.TimeFieldSeparatorColor), ctx);
            sep.alignment = TextAlignmentOptions.Center;
            sep.raycastTarget = false;
            TopLeft(sep.rectTransform, x, y - 4f, w, h);
        }

        /// <summary>ToggleItem with the updated toggle: ToggleButton(shape Circle, pressed / checked RoundedCorner 12), bold label when checked.</summary>
        static M3Interactive PeriodItem(RectTransform parent, M3Context ctx, string label, float x, float y, float w, float h, bool selected)
        {
            var rt = M3Build.Rect("PeriodToggle " + label, parent);
            TopLeft(rt, x, y, w, h);
            float full = Mathf.Min(w, h) / 2f;
            var p = new M3Pressable(rt, ctx, new PressableStyle
            {
                Container = ColorRole.SurfaceContainerLowest,
                ContainerChecked = ColorRole.PrimaryContainer,
                Content = TimePickerTokens.PeriodSelectorUnselectedLabelTextColor,
                ContentChecked = ColorRole.OnPrimaryContainer,
                Toggle = true, Selected = selected,
                RestShape = full, PressedShape = 12f, CheckedShape = 12f, CheckedPressedShape = 12f, MaxShape = full,
                Spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial),
            });
            var regular = M3Build.Text("Label", rt, label, TimePickerTokens.PeriodSelectorLabelTextFont, ColorRef.Role(TimePickerTokens.PeriodSelectorUnselectedLabelTextColor), ctx);
            var bold = M3Build.Text("LabelBold", rt, label, TimePickerTokens.PeriodSelectorLabelTextFont, ColorRef.Role(ColorRole.OnPrimaryContainer), ctx);
            bold.fontWeight = FontWeight.Bold;
            foreach (var t in new[] { regular, bold }) { M3Build.Fill(t.rectTransform); t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; p.Content(t); }
            p.Spec.ShowWhenSelected.Add(bold.gameObject);
            p.Spec.HideWhenSelected.Add(regular.gameObject);
            return p.Finish();
        }

        /// <summary>CircularLayout(radiusToSizeRatio = OuterCircleToSizeRatio) of 48 dp number boxes.</summary>
        static CanvasGroup Face(RectTransform dial, M3Context ctx, int[] values, string name, bool selectedCopy, M3TimePicker picker, string eventPrefix)
        {
            var face = M3Build.Rect(name, dial);
            M3Build.Fill(face);
            var group = face.gameObject.AddComponent<CanvasGroup>();
            float size = TimePickerTokens.ClockDialContainerSize;
            float radius = size * (101f / 256f);
            for (int i = 0; i < values.Length; i++)
            {
                float theta = Mathf.PI * 2f / values.Length * i - Mathf.PI / 2f;
                var box = M3Build.Rect("Number", face);
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
                box.sizeDelta = Vector2.one * NumberBox;
                box.anchoredPosition = new Vector2(Mathf.Round(radius * Mathf.Cos(theta)), -Mathf.Round(radius * Mathf.Sin(theta)));
                var color = selectedCopy ? TimePickerTokens.ClockDialSelectedLabelTextColor : TimePickerTokens.ClockDialUnselectedLabelTextColor;
                var t = M3Build.Text("Text", box, values[i].ToString(), TimePickerTokens.ClockDialLabelTextFont, ColorRef.Role(color), ctx);
                M3Build.Fill(t.rectTransform);
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
                if (!selectedCopy && picker != null)
                {
                    var hit = box.gameObject.AddComponent<Image>();
                    hit.color = new Color(0, 0, 0, 0);
                    var b = box.gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                    M3UdonBridge.Wire(b.onClick, picker, eventPrefix + i);
                }
            }
            if (selectedCopy) group.blocksRaycasts = false;
            return group;
        }

        // ---- time input --------------------------------------------------------------------------

        /// <summary>A standalone time input (TimeInput composable); vibrant = with TimePickerShapes.</summary>
        public static RectTransform Input(Transform parent, M3Context ctx, int hour = 9, int minute = 30, bool vibrant = false)
        {
            var root = M3Build.Rect("TimeInput", parent);
            InputContent(root, ctx, vibrant, hour, minute, out var w, out var h);
            if (vibrant) h = VibrantTimeFieldHeight + SupportLabelTop + 32f; // room for an error
            Size(root, w, h);
            return root;
        }

        static M3TimeInput InputContent(RectTransform page, M3Context ctx, bool vibrant, int hour, int minute, out float width, out float height)
        {
            float fieldW = vibrant ? VibrantTimeFieldWidth : TimeInputTokens.TimeFieldContainerWidth;
            float fieldH = vibrant ? VibrantTimeFieldHeight : TimeInputTokens.TimeFieldContainerHeight;
            float sepW = vibrant ? VibrantSeparatorWidth : DisplaySeparatorWidth;
            float sepH = vibrant ? VibrantTimeFieldHeight : TimeInputTokens.PeriodSelectorContainerHeight;
            float perPad = vibrant ? VibrantPeriodTogglePadding : PeriodTogglePaddingSmall;
            float perW = vibrant ? VibrantPeriodToggleWidth : TimeInputTokens.PeriodSelectorContainerWidth;
            float perH = vibrant ? VibrantPeriodToggleHeight : TimeInputTokens.PeriodSelectorContainerHeight;
            float perGap = vibrant ? VibrantPeriodTogglePadding : PeriodTogglePaddingSmall;
            var font = vibrant ? TypeRole.DisplayLarge : TimeInputTokens.TimeFieldLabelTextFont;
            float supportH = SupportLabelTop + 32f; // minLines = 2 of bodySmall (16 dp lines)
            width = fieldW * 2f + sepW + perPad + perW;
            height = fieldH + (vibrant ? 0f : supportH);

            var input = M3UdonBridge.Add<M3TimeInput>(page.gameObject);
            input.hour = hour; input.minute = minute;
            input.alwaysShowSupporting = !vibrant;
            input.fieldHeight = fieldH;
            input.supportingHeight = supportH;
            input.contentHeight = height;

            var inputs = new TMP_InputField[2];
            var texts = new TextMeshProUGUI[2];
            var containers = new Graphic[2];
            var thin = new GameObject[2];
            var thick = new GameObject[2];
            var thinG = new Graphic[2];
            var thickG = new Graphic[2];
            var supporting = new TextMeshProUGUI[2];
            float r = vibrant ? ShapeScale.Get(ShapeRole.CornerLarge).TopStart : ShapeScale.Get(TimeInputTokens.TimeFieldContainerShape).TopStart;
            for (int i = 0; i < 2; i++)
            {
                bool isHour = i == 0;
                float x = isHour ? 0f : fieldW + sepW;
                var field = M3Build.Rect(isHour ? "HourField" : "MinuteField", page);
                TopLeft(field, x, 0f, fieldW, fieldH);
                var container = M3Build.Shape("Container", field, ShapeCell.Create(ShapeKind.Fill).WithRadius(r), ColorRef.Clear);
                var t1 = M3Build.Shape("Border", field, ShapeCell.Create(ShapeKind.Stroke).WithRadius(r).WithStroke(1f), ColorRef.Clear);
                var t2 = M3Build.Shape("FocusedBorder", field, ShapeCell.Create(ShapeKind.Stroke).WithRadius(r).WithStroke(2f), ColorRef.Clear);
                t1.raycastTarget = t2.raycastTarget = false;
                var viewport = M3Build.Rect("TextArea", field);
                M3Build.Fill(viewport);
                viewport.gameObject.AddComponent<RectMask2D>();
                var text = M3Build.Text("Text", viewport, "", font, ColorRef.Role(ColorRole.OnSurface), ctx);
                M3Build.Fill(text.rectTransform);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                var sup = M3Build.Text("SupportingText", page, isHour ? "Hour" : "Minute", TimeInputTokens.TimeFieldSupportingTextFont, ColorRef.Role(TimeInputTokens.TimeFieldSupportingTextColor), ctx);
                sup.alignment = TextAlignmentOptions.TopLeft;
                sup.enableWordWrapping = true;
                sup.raycastTarget = false;
                TopLeft(sup.rectTransform, x, fieldH + SupportLabelTop, fieldW, 32f);
                foreach (var g in new Graphic[] { container, t1, t2, text, sup })
                {
                    var b = g.GetComponent<M3ColorBinding>(); if (b != null) Object.DestroyImmediate(b);
                }

                var tmp = field.gameObject.AddComponent<TMP_InputField>();
                tmp.textViewport = viewport;
                tmp.textComponent = text;
                tmp.targetGraphic = container;
                tmp.transition = Selectable.Transition.None;
                tmp.lineType = TMP_InputField.LineType.SingleLine;
                tmp.characterLimit = 3; // the third digit replaces the value (isReplacingTwoDigits)
                tmp.fontAsset = text.font;
                tmp.pointSize = text.fontSize;
                tmp.caretWidth = 2;
                tmp.customCaretColor = true;
                tmp.richText = false;
                tmp.onFocusSelectAll = false;
                tmp.navigation = new Navigation { mode = Navigation.Mode.None };
                M3UdonBridge.Wire(tmp.onValueChanged, input, isHour ? nameof(M3TimeInput._HourChanged) : nameof(M3TimeInput._MinuteChanged));
                M3UdonBridge.Wire(tmp.onSelect, input, isHour ? nameof(M3TimeInput._HourSelect) : nameof(M3TimeInput._MinuteSelect));
                M3UdonBridge.Wire(tmp.onDeselect, input, isHour ? nameof(M3TimeInput._HourDeselect) : nameof(M3TimeInput._MinuteDeselect));

                inputs[i] = tmp; texts[i] = text; containers[i] = container;
                thin[i] = t1.gameObject; thick[i] = t2.gameObject; thinG[i] = t1; thickG[i] = t2;
                supporting[i] = sup;
            }
            Separator(page, ctx, font, fieldW, 0f, sepW, sepH);
            float px = fieldW * 2f + sepW + perPad;
            float itemH = (perH - perGap) / 2f;
            var am = PeriodItem(page, ctx, "AM", px, 0f, perW, itemH, hour < 12);
            var pm = PeriodItem(page, ctx, "PM", px, itemH + perGap, perW, itemH, hour >= 12);
            am.exclusiveWith = pm.exclusiveWith = new[] { am, pm };
            M3UdonBridge.Wire(am.GetComponent<Button>().onClick, input, nameof(M3TimeInput._AM));
            M3UdonBridge.Wire(pm.GetComponent<Button>().onClick, input, nameof(M3TimeInput._PM));
            M3UdonBridge.Sync(am); M3UdonBridge.Sync(pm);

            input.inputs = inputs; input.texts = texts; input.containers = containers;
            input.thinBorders = thin; input.thickBorders = thick;
            input.thinBorderGraphics = thinG; input.thickBorderGraphics = thickG;
            input.supporting = supporting;
            input.amToggle = am; input.pmToggle = pm;

            // TimeInputDefaults.colors / vibrantColors (timeTextFieldColors)
            var sc = page.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(vibrant ? ColorRole.Primary : ColorRole.Primary),                                         // caret
                ColorRef.Role(ColorRole.Error),
                ColorRef.Translucent(ColorRole.Primary, 0.4f),                                                           // text selection
                ColorRef.Role(vibrant ? ColorRole.SurfaceContainerLowest : TimePickerTokens.TimeSelectorUnselectedContainerColor),
                ColorRef.Role(vibrant ? ColorRole.SurfaceContainerLowest : TimePickerTokens.TimeSelectorSelectedContainerColor),
                ColorRef.Role(vibrant ? ColorRole.OnSurface : TimePickerTokens.TimeSelectorUnselectedLabelTextColor),
                ColorRef.Role(vibrant ? ColorRole.Primary : TimePickerTokens.TimeSelectorSelectedLabelTextColor),
                ColorRef.Role(vibrant ? ColorRole.Primary : ColorRole.Outline),                                         // focused border
                vibrant ? ColorRef.Clear : ColorRef.Role(ColorRole.Outline),                                             // unfocused border
                ColorRef.Role(ColorRole.ErrorContainer),
                ColorRef.Role(ColorRole.Error),
                ColorRef.Role(ColorRole.OnErrorContainer),
                ColorRef.Role(TimeInputTokens.TimeFieldSupportingTextColor),
            };
            return input;
        }

        // ---- time scroll -------------------------------------------------------------------------

        /// <summary>A standalone time scroll (TimeScroll composable, vibrant).</summary>
        public static RectTransform Scroll(Transform parent, M3Context ctx, int hour = 9, int minute = 30)
        {
            var root = M3Build.Rect("TimeScroll", parent);
            ScrollContent(root, ctx, hour, minute, out var w, out var h);
            Size(root, w, h);
            return root;
        }

        static M3TimeScroll ScrollContent(RectTransform page, M3Context ctx, int hour, int minute, out float width, out float height)
        {
            float fieldW = 100f, fieldH = VibrantTimeFieldHeight;
            float sepW = VibrantSeparatorWidth;
            float perPad = VibrantPeriodTogglePadding, perW = VibrantPeriodToggleWidth, perH = VibrantPeriodToggleHeight, perGap = VibrantPeriodTogglePadding;
            width = fieldW * 2f + sepW + perPad + perW;
            height = fieldH;
            var ts = M3UdonBridge.Add<M3TimeScroll>(page.gameObject);
            ts.hour = hour; ts.minute = minute;
            int h12 = hour % 12;
            var hourField = ScrollFieldAt(page, ctx, 0f, 0f, fieldW, fieldH, 12, h12 == 0 ? 11 : h12 - 1, 1);
            Separator(page, ctx, TypeRole.DisplayLarge, fieldW, 0f, sepW, fieldH);
            var minuteField = ScrollFieldAt(page, ctx, fieldW + sepW, 0f, fieldW, fieldH, 60, minute, 0);
            hourField.changeListener = ts; hourField.changeEvent = nameof(M3TimeScroll._HourScrolled);
            minuteField.changeListener = ts; minuteField.changeEvent = nameof(M3TimeScroll._MinuteScrolled);
            M3UdonBridge.Sync(hourField); M3UdonBridge.Sync(minuteField);
            float px = fieldW * 2f + sepW + perPad;
            float itemH = (perH - perGap) / 2f;
            var am = PeriodItem(page, ctx, "AM", px, 0f, perW, itemH, hour < 12);
            var pm = PeriodItem(page, ctx, "PM", px, itemH + perGap, perW, itemH, hour >= 12);
            am.exclusiveWith = pm.exclusiveWith = new[] { am, pm };
            M3UdonBridge.Wire(am.GetComponent<Button>().onClick, ts, nameof(M3TimeScroll._AM));
            M3UdonBridge.Wire(pm.GetComponent<Button>().onClick, ts, nameof(M3TimeScroll._PM));
            M3UdonBridge.Sync(am); M3UdonBridge.Sync(pm);
            ts.hourField = hourField; ts.minuteField = minuteField;
            ts.amToggle = am; ts.pmToggle = pm;
            return ts;
        }

        // ---- scroll field ------------------------------------------------------------------------

        /// <summary>A standalone ScrollField (default height ScrollFieldHeight).</summary>
        public static RectTransform ScrollField(Transform parent, M3Context ctx, int itemCount, int selected = 0, float width = 100f, float height = ScrollFieldHeight)
        {
            var holder = M3Build.Rect("ScrollFieldHolder", parent);
            Size(holder, width, height);
            var f = ScrollFieldAt(holder, ctx, 0f, 0f, width, height, itemCount, selected, 0);
            M3UdonBridge.Sync(f);
            return holder;
        }

        static M3ScrollField ScrollFieldAt(RectTransform parent, M3Context ctx, float x, float y, float w, float h, int itemCount, int selected, int labelOffset)
        {
            var root = M3Build.Rect("ScrollField", parent);
            TopLeft(root, x, y, w, h);
            // clip(ScrollFieldDefaults.shape) + background(containerColor)
            var bgImg = M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(ShapeRole.CornerLarge)), ColorRef.Role(ColorRole.SurfaceContainerLowest));
            bgImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            float pageH = ScrollFieldHeight / 3f; // PageSize.Fixed(ScrollFieldHeight / 3)
            int pageCount = itemCount * Mathf.Max(1, 2400 / itemCount);
            var content = M3Build.Rect("Pages", bgImg.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, pageCount * pageH);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = (RectTransform)bgImg.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scroll.inertia = true; // the release velocity picks the snap page
            scroll.scrollSensitivity = pageH;

            var field = M3UdonBridge.Add<M3ScrollField>(root.gameObject);
            field.itemCount = itemCount;
            field.selectedOption = selected;
            field.labelOffset = labelOffset;
            field.pageCount = pageCount;
            field.scroll = scroll;
            field.content = content;
            field.fieldHeight = h;
            field.pageHeight = pageH;
            var fast = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            field.fastSpatialDamping = fast.DampingRatio;
            field.fastSpatialStiffness = fast.Stiffness;
            var medium = TypeScale.Get(TypeRole.DisplayMedium);
            var large = TypeScale.Get(TypeRole.DisplayLargeEmphasized);
            field.largeScale = large.Size / medium.Size;

            int slotsN = Mathf.Min(6, Mathf.CeilToInt(h / pageH) + 2);
            var slots = new RectTransform[slotsN];
            var baseT = new TextMeshProUGUI[slotsN];
            var emphT = new TextMeshProUGUI[slotsN];
            var baseR = new RectTransform[slotsN];
            var emphR = new RectTransform[slotsN];
            for (int i = 0; i < slotsN; i++)
            {
                var slot = M3Build.Rect("Item", content);
                slot.anchorMin = new Vector2(0f, 1f);
                slot.anchorMax = new Vector2(1f, 1f);
                slot.pivot = new Vector2(0.5f, 1f);
                slot.sizeDelta = new Vector2(0f, pageH);
                slot.anchoredPosition = new Vector2(0f, -i * pageH);
                var hit = slot.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var button = slot.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
                M3UdonBridge.Wire(button.onClick, field, "_I" + i);
                var b = M3Build.Text("Text", slot, "00", TypeRole.DisplayMedium, ColorRef.Role(ColorRole.Outline), ctx);
                var e = M3Build.Text("TextEmphasized", slot, "00", TypeRole.DisplayLargeEmphasized, ColorRef.Role(ColorRole.OnSurface), ctx);
                e.fontSize = b.fontSize;          // scaled up with the selection like b
                e.characterSpacing = b.characterSpacing;
                foreach (var t in new[] { b, e })
                {
                    t.alignment = TextAlignmentOptions.Center;
                    t.raycastTarget = false;
                    t.enableWordWrapping = false;
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    t.rectTransform.sizeDelta = new Vector2(w, pageH);
                    t.rectTransform.anchoredPosition = Vector2.zero;
                    var cb = t.GetComponent<M3ColorBinding>(); if (cb != null) Object.DestroyImmediate(cb);
                }
                slots[i] = slot; baseT[i] = b; emphT[i] = e; baseR[i] = b.rectTransform; emphR[i] = e.rectTransform;
            }
            field.slots = slots;
            field.baseTexts = baseT; field.emphasisTexts = emphT;
            field.baseRects = baseR; field.emphasisRects = emphR;

            var trigger = root.gameObject.AddComponent<EventTrigger>();
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.BeginDrag, field, nameof(M3ScrollField._BeginDrag));
            M3UdonBridge.WireTrigger(trigger, EventTriggerType.EndDrag, field, nameof(M3ScrollField._EndDrag));
            M3UdonBridge.Wire(scroll.onValueChanged, field, nameof(M3ScrollField._Scroll));

            // ScrollFieldDefaults.colors: contentColor Outline, selectedContentColor OnSurface
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef> { ColorRef.Role(ColorRole.Outline), ColorRef.Role(ColorRole.OnSurface) };
            return field;
        }

        // ---- helpers -----------------------------------------------------------------------------

        static void Size(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = h;
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return null;
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
