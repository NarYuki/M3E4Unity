using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Full-screen date range picker: the DateRangePickerSample layout (a row with a close icon button
    /// and a "Save" text button, padding 12, over the picker's container color) and DateRangePicker
    /// (DateRangePicker.kt): header (title / headline at start 64, mode toggle, divider), weekdays and
    /// the vertical months list, or the start / end date input fields. M3DateRangePicker drives it.
    /// </summary>
    public static class M3DateRangePickers
    {
        const float HorizontalPadding = 12f;      // DatePickerHorizontalPadding
        const float Cell = 48f;                   // RecommendedSizeForAccessibility
        const float TitleStart = 64f;             // DateRangePickerTitlePadding / HeadlinePadding start
        const float EndPadding = 12f;
        const float MaxPoolMonths = 5;            // M3DateRangePicker has day events for 5 × 42 cells
        static readonly string[] WeekdayLetters = { "S", "M", "T", "W", "T", "F", "S" };

        public static RectTransform FullScreen(Transform parent, M3Context ctx, float width = 412f, float height = 915f,
            int startDate = 0, int endDate = 0, bool inputMode = false)
        {
            var bg = DatePickerModalTokens.ContainerColor;
            var inner = ctx.On(bg);

            var root = M3Build.Rect("DateRangePicker", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;
            M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(DatePickerModalTokens.RangeSelectionContainerShape)), ColorRef.Role(bg));

            var picker = M3UdonBridge.Add<M3DateRangePicker>(root.gameObject);
            picker.startDate = startDate;
            picker.endDate = endDate;
            picker.inputMode = inputMode;

            // the sample's action row: close (IconButton) at the start, Save (TextButton, enabled with an end date) at the end
            float barH = Cell;
            var close = M3IconButtons.Create(root, inner, "close", contentColor: ColorRole.OnSurfaceVariant);
            Center(close, HorizontalPadding + 24f, barH / 2f);
            var save = M3Buttons.Create(root, inner, "Save", ButtonStyle.Text);
            var sl = save.GetComponent<LayoutElement>(); if (sl != null) sl.ignoreLayout = true;
            save.anchorMin = save.anchorMax = new Vector2(0f, 1f);
            save.pivot = new Vector2(1f, 0.5f);
            save.anchoredPosition = new Vector2(width - HorizontalPadding, -barH / 2f);
            picker.saveButton = save.GetComponent<Button>();

            // DatePickerHeader: title, then the headline row (headline with bottom 12, toggle with end / bottom 12)
            float headerTop = barH;
            var title = M3Build.Text("Title", root, "Select dates", DatePickerModalTokens.HeaderSupportingTextFont, ColorRef.Role(DatePickerModalTokens.HeaderSupportingTextColor), inner);
            title.alignment = TextAlignmentOptions.TopLeft;
            var (th, _, _) = M3Build.MeasureText(title, "Select dates", width - TitleStart - EndPadding);
            Place(title.rectTransform, TitleStart, headerTop, width - TitleStart - EndPadding, th);
            var headline = M3Build.Text("Headline", root, "Start date - End date", DatePickerModalTokens.RangeSelectionHeaderHeadlineFont,
                ColorRef.Role(DatePickerModalTokens.HeaderHeadlineColor), inner);
            headline.alignment = TextAlignmentOptions.Left;
            headline.richText = true;
            headline.enableWordWrapping = true;
            float hh = Mathf.Ceil(headline.GetPreferredValues("Start date").y);
            float rowH = Mathf.Max(hh + 12f, 48f + 12f);
            float rowTop = headerTop + th;
            float headerH = Mathf.Max(DatePickerModalTokens.RangeSelectionHeaderContainerHeight - 60f, th + rowH); // HeaderHeightOffset 60
            rowTop = headerTop + headerH - rowH;
            float headlineW = width - TitleStart - EndPadding - 48f - 12f;
            Place(headline.rectTransform, TitleStart, rowTop + (rowH - hh - 12f) / 2f, headlineW, hh);
            var toggle = M3IconButtons.Create(root, inner, inputMode ? "date_range" : "edit", contentColor: DatePickerModalTokens.HeaderHeadlineColor);
            Center(toggle, width - 12f - 24f, rowTop + (rowH - 12f) / 2f);
            M3UdonBridge.Wire(toggle.GetComponent<Button>().onClick, picker, nameof(M3DateRangePicker._ToggleMode));
            var divider = M3Build.Shape("Divider", root, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DividerTokens.Color), false);
            float dividerTop = headerTop + headerH;
            Place((RectTransform)divider.transform, 0f, dividerTop, width, DividerTokens.Thickness);
            picker.title = title;
            picker.headline = headline;
            picker.modeIcon = FindDeep(toggle, "Icon").GetComponent<TextMeshProUGUI>();
            picker.pickerModeGlyph = M3Icons.Glyph("edit");
            picker.inputModeGlyph = M3Icons.Glyph("date_range");

            float contentTop = dividerTop + DividerTokens.Thickness;
            float contentH = height - contentTop;
            PickerPage(root, inner, picker, width, contentTop, contentH);
            InputPage(root, inner, picker, width, contentTop, contentH);

            picker.fastEffectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(DatePickerModalTokens.DateUnselectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateTodayLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateSelectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateSelectedContainerColor),
                ColorRef.Role(DatePickerModalTokens.DateTodayContainerOutlineColor),
                ColorRef.Role(DatePickerModalTokens.SelectionDateInRangeLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.RangeSelectionActiveIndicatorContainerColor),
            };
            M3UdonBridge.Sync(picker);
            return root;
        }

        /// <summary>DateRangePickerContent: weekdays + VerticalMonthsList inside the horizontal padding.</summary>
        static void PickerPage(RectTransform root, M3Context inner, M3DateRangePicker picker, float width, float top, float height)
        {
            var bg = DatePickerModalTokens.ContainerColor;
            var page = M3Build.Rect("Picker", root);
            Place(page, 0f, top, width, height);
            picker.pickerGroup = page.gameObject.AddComponent<CanvasGroup>();
            float gridW = width - 2f * HorizontalPadding;
            // Row(Arrangement.SpaceEvenly) of 48 dp cells
            float gap = (gridW - 7f * Cell) / 8f;

            for (int d = 0; d < 7; d++)
            {
                var w = M3Build.Text("Weekday", page, WeekdayLetters[d], DatePickerModalTokens.WeekdaysLabelTextFont, ColorRef.Role(DatePickerModalTokens.WeekdaysLabelTextColor), inner);
                w.alignment = TextAlignmentOptions.Center;
                Place(w.rectTransform, HorizontalPadding + gap + d * (Cell + gap), 0f, Cell, Cell);
            }

            float viewportH = height - Cell;
            var viewport = M3Build.Rect("Months", page);
            Place(viewport, HorizontalPadding, Cell, gridW, viewportH);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            var list = M3Build.Rect("List", viewport);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(0f, viewportH);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = Cell;

            // month item: subhead (CalendarMonthSubheadPadding start 24, top 20, bottom 8) + 6 rows
            var probe = M3Build.Text("Probe", page, "October 2026", DatePickerModalTokens.RangeSelectionMonthSubheadFont, ColorRef.Role(DatePickerModalTokens.RangeSelectionMonthSubheadColor), inner);
            var (lineH, _, _) = M3Build.MeasureText(probe, "October 2026", gridW);
            Object.DestroyImmediate(probe.gameObject);
            float subheadH = 20f + lineH + 8f;
            float monthH = subheadH + Cell * 6;
            int pool = Mathf.Min((int)MaxPoolMonths, Mathf.CeilToInt(viewportH / monthH) + 1);

            float dayW = DatePickerModalTokens.DateContainerWidth, dayH = DatePickerModalTokens.DateContainerHeight;
            var circle = ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full);
            var months = new List<RectTransform>();
            var subheads = new List<TextMeshProUGUI>();
            var cells = new List<GameObject>();
            var cellRects = new List<RectTransform>();
            var texts = new List<TextMeshProUGUI>();
            var circles = new List<Graphic>();
            var rings = new List<Graphic>();
            var its = new List<M3Interactive>();
            var rows = new List<RectTransform>();
            var rowGraphics = new List<Graphic>();
            for (int m = 0; m < pool; m++)
            {
                var month = M3Build.Rect("Month", list);
                Place(month, 0f, m * monthH, gridW, monthH);
                var sub = M3Build.Text("Subhead", month, "October 2026", DatePickerModalTokens.RangeSelectionMonthSubheadFont, ColorRef.Role(DatePickerModalTokens.RangeSelectionMonthSubheadColor), inner);
                sub.alignment = TextAlignmentOptions.TopLeft;
                Place(sub.rectTransform, 24f, 20f, gridW - 24f, lineH);
                // drawRangeBackground rows, drawn behind the days
                for (int r = 0; r < 6; r++)
                {
                    var rowImg = M3Build.Shape("Range", month, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DatePickerModalTokens.RangeSelectionActiveIndicatorContainerColor), false);
                    rowImg.raycastTarget = false;
                    var rrt = (RectTransform)rowImg.transform;
                    Place(rrt, 0f, subheadH + r * Cell + 4f, gridW, DatePickerModalTokens.DateStateLayerHeight);
                    rrt.gameObject.SetActive(false);
                    var b = rowImg.GetComponent<M3ColorBinding>(); if (b != null) Object.DestroyImmediate(b);
                    rows.Add(rrt); rowGraphics.Add(rowImg);
                }
                for (int i = 0; i < 42; i++)
                {
                    var cell = M3Build.Rect("DayCell", month);
                    Place(cell, gap + (i % 7) * (Cell + gap), subheadH + (i / 7) * Cell, Cell, Cell);
                    var day = M3Build.Rect("Day", cell);
                    day.anchorMin = day.anchorMax = new Vector2(0.5f, 0.5f);
                    day.sizeDelta = new Vector2(dayW, dayH);
                    var sel = M3Build.Shape("Selected", day, circle, ColorRef.Role(DatePickerModalTokens.DateSelectedContainerColor));
                    sel.raycastTarget = false;
                    var ring = M3Build.Shape("Today", day, ShapeCell.Create(ShapeKind.Stroke).WithShape(CornerShape.Full).WithStroke(DatePickerModalTokens.DateTodayContainerOutlineWidth),
                        ColorRef.Role(DatePickerModalTokens.DateTodayContainerOutlineColor));
                    ring.raycastTarget = false;
                    var stateLayer = M3Build.Shape("StateLayer", day, circle, ColorRef.Clear);
                    var ripple = M3Build.Shape("Ripple", day, ShapeCell.Create(ShapeKind.Ripple).WithShape(CornerShape.Full), ColorRef.Clear);
                    var text = M3Build.Text("Label", day, "1", DatePickerModalTokens.DateLabelTextFont, ColorRef.Role(DatePickerModalTokens.DateUnselectedLabelTextColor), inner);
                    M3Build.Fill(text.rectTransform);
                    text.alignment = TextAlignmentOptions.Center;
                    text.raycastTarget = false;
                    var spec = new InteractionSpec
                    {
                        Root = day, Context = inner, StateLayer = stateLayer, Ripple = ripple, RippleClipShape = CornerShape.Full,
                        RestShape = dayH / 2f, PressedShape = dayH / 2f, SelectedShape = dayH / 2f, SelectedPressedShape = dayH / 2f, MaxShape = dayH / 2f,
                    };
                    spec.ShapeLayers.Add((stateLayer, _ => circle));
                    // the state layer and ripple use LocalContentColor (OnSurface, or OnPrimary on a selected day);
                    // they are left translucent because the day may sit on the range background
                    spec.Color(stateLayer, ColorRef.Translucent(DatePickerModalTokens.DateUnselectedLabelTextColor, StateLayer.Hover),
                        ColorRef.Translucent(DatePickerModalTokens.DateSelectedLabelTextColor, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                    spec.RippleColor = ColorRef.Translucent(DatePickerModalTokens.DateUnselectedLabelTextColor, StateLayer.Pressed);
                    spec.RippleColorSelected = ColorRef.Translucent(DatePickerModalTokens.DateSelectedLabelTextColor, StateLayer.Pressed);
                    var it = M3Interaction.Apply(spec);
                    M3UdonBridge.Wire(day.GetComponent<Button>().onClick, picker, "_D" + (m * 42 + i));
                    foreach (var g in new Graphic[] { text, sel, ring })
                    {
                        var b = g.GetComponent<M3ColorBinding>();
                        if (b != null) Object.DestroyImmediate(b);
                    }
                    cells.Add(cell.gameObject); cellRects.Add(cell); texts.Add(text); circles.Add(sel); rings.Add(ring); its.Add(it);
                }
                months.Add(month); subheads.Add(sub);
            }
            M3UdonBridge.Wire(scroll.onValueChanged, picker, nameof(M3DateRangePicker._Scroll));

            picker.scroll = scroll;
            picker.scrollContent = list;
            picker.viewportHeight = viewportH;
            picker.monthHeight = monthH;
            picker.subheadHeight = subheadH;
            picker.gridWidth = gridW;
            picker.cell = Cell;
            picker.stateLayerHeight = DatePickerModalTokens.DateStateLayerHeight;
            picker.months = months.ToArray();
            picker.subheads = subheads.ToArray();
            picker.cells = cells.ToArray();
            picker.cellRects = cellRects.ToArray();
            picker.dayTexts = texts.ToArray();
            picker.selectedCircles = circles.ToArray();
            picker.todayRings = rings.ToArray();
            picker.dayInteractives = its.ToArray();
            picker.rangeRows = rows.ToArray();
            picker.rangeGraphics = rowGraphics.ToArray();
        }

        /// <summary>DateRangeInputContent: two outlined fields (weight 0.5, 8 dp apart) inside InputTextFieldPadding.</summary>
        static void InputPage(RectTransform root, M3Context inner, M3DateRangePicker picker, float width, float top, float height)
        {
            var page = M3Build.Rect("Input", root);
            Place(page, 0f, top, width, height);
            picker.inputGroup = page.gameObject.AddComponent<CanvasGroup>();
            float fieldW = (width - 48f - 8f) / 2f;
            var fields = new RectTransform[2];
            for (int k = 0; k < 2; k++)
            {
                bool isStart = k == 0;
                var field = M3TextFields.Create(page, inner, TextFieldStyle.Outlined, isStart ? "Start date" : "End date", "", "MM/DD/YYYY", supporting: " ", width: fieldW);
                var fle = field.GetComponent<LayoutElement>(); if (fle != null) fle.ignoreLayout = true;
                Place(field, 24f + k * (fieldW + 8f), 10f, fieldW, 64f);
                var tf = field.GetComponent<M3TextField>();
                tf.changeListener = picker;
                tf.changeEvent = isStart ? nameof(M3DateRangePicker._StartChanged) : nameof(M3DateRangePicker._EndChanged);
                var input = field.GetComponentInChildren<TMP_InputField>();
                M3UdonBridge.Wire(input.onEndEdit, picker, isStart ? nameof(M3DateRangePicker._StartEndEdit) : nameof(M3DateRangePicker._EndEndEdit));
                var error = FindDeep(field, "SupportingText").GetComponent<TextMeshProUGUI>();
                error.enableWordWrapping = true;
                error.text = "";
                error.rectTransform.sizeDelta = new Vector2(fieldW - 32f, 80f);
                error.gameObject.SetActive(false);
                if (isStart) { picker.startField = tf; picker.startText = input; picker.startError = error; }
                else { picker.endField = tf; picker.endText = input; picker.endError = error; }
                fields[k] = field;
            }
            picker.rangeError = "Date out of expected year range " + picker.minYear + " - " + picker.maxYear;
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return null;
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
