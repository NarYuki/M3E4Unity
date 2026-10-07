using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Date picker dialog (DatePickerDialog.android.kt + DatePicker.kt): header (title "Select date",
    /// headline, mode toggle, divider), month navigation (year menu button, previous / next), weekday
    /// letters and the 6-row month grid, then the dialog buttons. M3DatePicker drives the grid.
    /// </summary>
    public static class M3DatePickers
    {
        const float Width = 360f;                 // DatePickerModalTokens.ContainerWidth
        const float HorizontalPadding = 12f;      // DatePickerHorizontalPadding
        const float MonthYearHeight = 56f;
        const float Cell = 48f;                   // RecommendedSizeForAccessibility
        const int Rows = 6, Days = 7;             // MaxCalendarRows, DaysInWeek
        static readonly string[] WeekdayLetters = { "S", "M", "T", "W", "T", "F", "S" }; // en-US, Sunday first

        public static RectTransform Dialog(Transform parent, M3Context ctx, int selectedYear = 0, int selectedMonth = 0, int selectedDay = 0,
            int displayedYear = 0, int displayedMonth = 0, bool yearPickerVisible = false, bool inputMode = false)
        {
            var bg = DatePickerModalTokens.ContainerColor;
            var inner = ctx.On(bg);
            float headerH = DatePickerModalTokens.HeaderContainerHeight;
            float height = DatePickerModalTokens.ContainerHeight; // heightIn(max = ContainerHeight): the picker mode fills it
            float buttonsH = 48f + 8f;                            // the 48 dp buttons + DialogButtonsPadding bottom 8
            float pickerH = MonthYearHeight + Cell * (Rows + 1);  // navigation + weekdays + month grid

            // the root keeps the picker's size for layouts; the surface wraps the content and animates its height
            var root = M3Build.Rect("DatePickerDialog", parent);
            root.sizeDelta = new Vector2(Width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = Width;
            le.preferredHeight = le.minHeight = height;
            var surfaceRoot = M3Build.Rect("Surface", root);
            Place(surfaceRoot, 0f, 0f, Width, height);
            M3Build.Shape("Container", surfaceRoot, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(DatePickerModalTokens.ContainerShape)), ColorRef.Role(bg));
            var surface = surfaceRoot; // header parts and buttons
            var root0 = root;

            var picker = M3UdonBridge.Add<M3DatePicker>(root.gameObject);
            picker.selectedYear = selectedYear; picker.selectedMonth = selectedMonth; picker.selectedDay = selectedDay;
            picker.displayedYear = displayedYear; picker.displayedMonth = displayedMonth;

            // header: title at the top (padding 24 / 12 / top 16), headline row at the bottom (padding 24 / 12 / bottom 12)
            var title = M3Build.Text("Title", surface, "Select date", DatePickerModalTokens.HeaderSupportingTextFont, ColorRef.Role(DatePickerModalTokens.HeaderSupportingTextColor), inner);
            title.alignment = TextAlignmentOptions.TopLeft;
            var (th, _, _) = M3Build.MeasureText(title, "Select date", Width - 36f);
            Place(title.rectTransform, 24f, 16f, Width - 36f, th);
            var headline = M3Build.Text("Headline", surface, "Selected date", DatePickerModalTokens.HeaderHeadlineFont, ColorRef.Role(DatePickerModalTokens.HeaderHeadlineColor), inner);
            headline.alignment = TextAlignmentOptions.Left;
            float hh = Mathf.Ceil(headline.GetPreferredValues("Selected date").y);
            // the row is vertically centered: headline (with its bottom 12) vs. the 48 dp mode toggle (with its bottom 12)
            float rowH = Mathf.Max(hh, 48f) + 12f;
            float rowTop = headerH - 1f - rowH; // above the divider
            Place(headline.rectTransform, 24f, rowTop + (rowH - 12f - hh) / 2f, Width - 24f - 12f - 48f - 12f, hh);
            var toggle = M3IconButtons.Create(surface, inner, inputMode ? "date_range" : "edit", contentColor: DatePickerModalTokens.HeaderHeadlineColor);
            Center(toggle, Width - 12f - 24f, rowTop + (rowH - 12f) / 2f);
            var divider = M3Build.Shape("Divider", surface, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DividerTokens.Color), false);
            Place((RectTransform)divider.transform, 0f, headerH - DividerTokens.Thickness, Width, DividerTokens.Thickness);
            // DisplayModeToggleButton: Icons.Filled.Edit in the picker mode, Icons.Filled.DateRange in the input mode
            M3UdonBridge.Wire(toggle.GetComponent<Button>().onClick, picker, nameof(M3DatePicker._ToggleMode));
            picker.modeIcon = FindDeep(toggle, "Icon").GetComponent<TextMeshProUGUI>();
            picker.pickerModeGlyph = M3Icons.Glyph("edit");
            picker.inputModeGlyph = M3Icons.Glyph("date_range");

            // SwitchableDateEntryContent: AnimatedContent with SizeTransform(clip = true) between the header and the buttons
            var content = M3Build.Rect("Content", surface);
            Place(content, 0f, headerH, Width, inputMode ? InputHeight : pickerH);
            content.gameObject.AddComponent<RectMask2D>();
            var page = M3Build.Rect("Picker", content);
            Place(page, 0f, 0f, Width, pickerH);
            var pageGroup = page.gameObject.AddComponent<CanvasGroup>();
            root = page; // the picker parts below go into the picker page

            // month navigation
            float navTop = 0f;
            var yearButton = M3Buttons.Create(root, inner, "September 2026", ButtonStyle.Text, trailingIcon: "arrow_drop_down", contentColor: ColorRole.OnSurfaceVariant);
            var yl = yearButton.GetComponent<LayoutElement>(); if (yl != null) yl.ignoreLayout = true;
            yearButton.anchorMin = yearButton.anchorMax = new Vector2(0f, 1f);
            yearButton.pivot = new Vector2(0f, 0.5f);
            yearButton.anchoredPosition = new Vector2(HorizontalPadding, -(navTop + MonthYearHeight / 2f));
            var prev = M3IconButtons.Create(root, inner, "chevron_left", contentColor: ColorRole.OnSurfaceVariant);
            var next = M3IconButtons.Create(root, inner, "chevron_right", contentColor: ColorRole.OnSurfaceVariant);
            Center(prev, Width - HorizontalPadding - 48f - 24f, navTop + MonthYearHeight / 2f);
            Center(next, Width - HorizontalPadding - 24f, navTop + MonthYearHeight / 2f);
            M3UdonBridge.Wire(prev.GetComponent<Button>().onClick, picker, nameof(M3DatePicker._Prev));
            M3UdonBridge.Wire(next.GetComponent<Button>().onClick, picker, nameof(M3DatePicker._Next));
            picker.prevButton = prev.GetComponent<Button>();
            picker.nextButton = next.GetComponent<Button>();
            picker.monthYear = yearButton.GetComponentInChildren<TextMeshProUGUI>();
            foreach (var t in yearButton.GetComponentsInChildren<TextMeshProUGUI>()) if (t.name != "Icon" && !t.name.Contains("Icon")) { picker.monthYear = t; break; }

            // weekday letters
            float gridX = HorizontalPadding;
            float weekTop = navTop + MonthYearHeight;
            for (int d = 0; d < Days; d++)
            {
                var w = M3Build.Text("Weekday", root, WeekdayLetters[d], DatePickerModalTokens.WeekdaysLabelTextFont, ColorRef.Role(DatePickerModalTokens.WeekdaysLabelTextColor), inner);
                w.alignment = TextAlignmentOptions.Center;
                Place(w.rectTransform, gridX + d * Cell, weekTop, Cell, Cell);
            }

            // the month grid: 48 dp cells, each with a 40 dp day surface
            float gridTop = weekTop + Cell;
            float dayW = DatePickerModalTokens.DateContainerWidth, dayH = DatePickerModalTokens.DateContainerHeight;
            var cells = new List<GameObject>();
            var texts = new List<TextMeshProUGUI>();
            var circles = new List<Graphic>();
            var rings = new List<Graphic>();
            var interactives = new List<M3Interactive>();
            var circle = ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full);
            for (int i = 0; i < Rows * Days; i++)
            {
                var cell = M3Build.Rect("DayCell", root);
                Place(cell, gridX + (i % Days) * Cell, gridTop + (i / Days) * Cell, Cell, Cell);
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
                var text = M3Build.Text("Label", day, (i + 1).ToString(), DatePickerModalTokens.DateLabelTextFont, ColorRef.Role(DatePickerModalTokens.DateUnselectedLabelTextColor), inner);
                M3Build.Fill(text.rectTransform);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                var spec = new InteractionSpec
                {
                    Root = day, Context = inner, StateLayer = stateLayer, Ripple = ripple, RippleClipShape = CornerShape.Full,
                    RestShape = dayH / 2f, PressedShape = dayH / 2f, SelectedShape = dayH / 2f, SelectedPressedShape = dayH / 2f, MaxShape = dayH / 2f,
                };
                spec.ShapeLayers.Add((stateLayer, _ => circle));
                spec.Color(stateLayer, ColorRef.Over(bg, DatePickerModalTokens.DateUnselectedLabelTextColor, StateLayer.Hover),
                    ColorRef.Over(DatePickerModalTokens.DateSelectedContainerColor, DatePickerModalTokens.DateSelectedLabelTextColor, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                spec.RippleColor = ColorRef.Over(bg, DatePickerModalTokens.DateUnselectedLabelTextColor, StateLayer.Hover, DatePickerModalTokens.DateUnselectedLabelTextColor, StateLayer.Pressed);
                spec.RippleColorSelected = ColorRef.Over(DatePickerModalTokens.DateSelectedContainerColor, DatePickerModalTokens.DateSelectedLabelTextColor, StateLayer.Hover,
                    DatePickerModalTokens.DateSelectedLabelTextColor, StateLayer.Pressed);
                var it = M3Interaction.Apply(spec);
                M3UdonBridge.Wire(day.GetComponent<Button>().onClick, picker, "_Day" + i);
                cells.Add(cell.gameObject); texts.Add(text); circles.Add(sel); rings.Add(ring); interactives.Add(it);
                // their colors are set by M3DatePicker (today / selected depend on the month shown)
                foreach (var g in new Graphic[] { text, sel, ring })
                {
                    var b = g.GetComponent<M3ColorBinding>();
                    if (b != null) Object.DestroyImmediate(b);
                }
            }
            picker.cells = cells.ToArray();
            picker.dayTexts = texts.ToArray();
            picker.selectedCircles = circles.ToArray();
            picker.todayRings = rings.ToArray();
            picker.dayInteractives = interactives.ToArray();
            picker.headline = headline;

            // the year picker over the weekdays + month grid, toggled by the year menu button
            M3UdonBridge.Wire(yearButton.GetComponent<Button>().onClick, picker, nameof(M3DatePicker._ToggleYears));
            var arrow = FindDeep(yearButton, "TrailingIcon");
            if (arrow != null) picker.yearArrow = (RectTransform)arrow;
            YearPicker(root, inner, picker, weekTop, yearPickerVisible);

            DateInput(content, inner, picker);
            picker.surface = surface;
            picker.content = content;
            picker.pickerPage = page;
            picker.pickerGroup = pageGroup;
            picker.headerHeight = headerH;
            picker.buttonsHeight = buttonsH;
            picker.pickerHeight = pickerH;
            picker.inputMode = inputMode;
            var spatial = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            picker.spatialDamping = spatial.DampingRatio;
            picker.spatialStiffness = spatial.Stiffness;
            picker.effectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.DefaultEffects).Stiffness;
            picker.fastEffectsStiffness = MotionScheme.Get(ctx.Motion, MotionRole.FastEffects).Stiffness;

            var sc = root0.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(DatePickerModalTokens.DateUnselectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateTodayLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateSelectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateSelectedContainerColor),
                ColorRef.Role(DatePickerModalTokens.DateTodayContainerOutlineColor),
                ColorRef.Role(DatePickerModalTokens.SelectionYearUnselectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.DateTodayLabelTextColor), // currentYearContentColor
                ColorRef.Role(DatePickerModalTokens.SelectionYearSelectedLabelTextColor),
                ColorRef.Role(DatePickerModalTokens.SelectionYearSelectedContainerColor),
            };

            // dialog buttons: AlertDialogFlowRow at the end, padding bottom 8 / end 6, below the content
            var ok = M3Buttons.Create(surface, inner, "OK", ButtonStyle.Text);
            var cancel = M3Buttons.Create(surface, inner, "Cancel", ButtonStyle.Text);
            float x = Width - 6f;
            foreach (var b in new[] { ok, cancel })
            {
                var bl = b.GetComponent<LayoutElement>(); if (bl != null) bl.ignoreLayout = true;
                b.anchorMin = b.anchorMax = new Vector2(0f, 0f);
                b.pivot = new Vector2(1f, 0.5f);
                b.anchoredPosition = new Vector2(x, 8f + 24f); // 48 dp touch target, centered
                x -= b.sizeDelta.x + 8f;
            }
            surface.sizeDelta = new Vector2(Width, headerH + (inputMode ? InputHeight : pickerH) + buttonsH);
            M3UdonBridge.Sync(picker);
            return root0;
        }

        // DateInputContent: InputTextFieldPadding (24 / 24 / top 10), the outlined field (8 dp label cutout + 56),
        // InputTextNonErroneousBottomPadding 16 (an error's supporting text takes its place, minus the supporting top padding)
        const float InputTop = 10f, InputSide = 24f, InputBottom = 16f, SupportingTop = 4f;
        const float InputFieldHeight = 8f + 56f;
        const float InputHeight = InputTop + InputFieldHeight + InputBottom;

        /// <summary>
        /// DateInputContent: an outlined text field labelled "Date" with the "MM/DD/YYYY" placeholder;
        /// M3DatePicker validates it (DateInputValidator) and shows the error as supporting text.
        /// </summary>
        static void DateInput(RectTransform content, M3Context inner, M3DatePicker picker)
        {
            var page = M3Build.Rect("Input", content);
            Place(page, 0f, 0f, Width, InputHeight);
            var group = page.gameObject.AddComponent<CanvasGroup>();
            float fieldW = Width - 2f * InputSide;
            var field = M3TextFields.Create(page, inner, TextFieldStyle.Outlined, "Date", "", "MM/DD/YYYY", supporting: " ", width: fieldW);
            var fle = field.GetComponent<LayoutElement>(); if (fle != null) fle.ignoreLayout = true;
            Place(field, InputSide, InputTop, fieldW, InputFieldHeight);
            var tf = field.GetComponent<M3TextField>();
            tf.changeListener = picker;
            var input = field.GetComponentInChildren<TMP_InputField>();
            M3UdonBridge.Wire(input.onEndEdit, picker, nameof(M3DatePicker._InputEndEdit));
            var error = FindDeep(field, "SupportingText").GetComponent<TextMeshProUGUI>();
            error.text = "";

            // the error strings (m3c_date_input_invalid_for_pattern / _invalid_year_range) and their wrapped heights
            float ew = fieldW - 32f;
            string patternError = "Date does not match expected pattern: MM/DD/YYYY";
            string rangeError = "Date out of expected year range " + picker.minYear + " - " + picker.maxYear;
            error.enableWordWrapping = true;
            picker.patternError = patternError;
            picker.rangeError = rangeError;
            picker.patternErrorHeight = Mathf.Ceil(error.GetPreferredValues(patternError, ew, 0f).y);
            picker.rangeErrorHeight = Mathf.Ceil(error.GetPreferredValues(rangeError, ew, 0f).y);
            error.rectTransform.sizeDelta = new Vector2(ew, Mathf.Max(picker.patternErrorHeight, picker.rangeErrorHeight));
            error.gameObject.SetActive(false);

            picker.inputPage = page;
            picker.inputGroup = group;
            picker.inputField = tf;
            picker.inputText = input;
            picker.inputError = error;
            // with an error the bottom padding becomes 16 - SupportingTop, so the content grows by the error text height
            picker.inputHeight = InputHeight;
        }

        /// <summary>
        /// YearPicker: a 3-column grid (GridCells.Fixed, 16 dp between rows) of 72 × 36 years, as tall
        /// as the weekdays + month grid minus the divider composed below it. The years are a pool of
        /// cells recycled by M3DatePicker as the grid scrolls (the LazyVerticalGrid).
        /// </summary>
        static void YearPicker(RectTransform root, M3Context inner, M3DatePicker picker, float top, bool visible)
        {
            var bg = DatePickerModalTokens.ContainerColor;
            float panelH = Cell * (Rows + 1);
            float viewportH = panelH - DividerTokens.Thickness;
            float yearW = DatePickerModalTokens.SelectionYearContainerWidth, yearH = DatePickerModalTokens.SelectionYearContainerHeight;
            float spacing = 16f; // YearsVerticalPadding
            float columnW = (Width - 2f * HorizontalPadding) / 3f;

            // clip (AnimatedVisibility + clipToBounds): its height animates, the content stays bottom-aligned
            var panel = M3Build.Rect("YearPicker", root);
            Place(panel, 0f, top, Width, visible ? panelH : 0f);
            panel.gameObject.AddComponent<RectMask2D>();
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var content = M3Build.Rect("Content", panel);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, panelH);

            // the grid's background(colors.containerColor), then the HorizontalDivider
            var gridBg = M3Build.Shape("Background", content, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(bg));
            Place((RectTransform)gridBg.transform, 0f, 0f, Width, viewportH);
            var divider = M3Build.Shape("Divider", content, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(DividerTokens.Color), false);
            Place((RectTransform)divider.transform, 0f, viewportH, Width, DividerTokens.Thickness);

            var viewport = M3Build.Rect("Viewport", content);
            Place(viewport, 0f, 0f, Width, viewportH);
            viewport.gameObject.AddComponent<RectMask2D>();
            var grid = M3Build.Rect("Years", viewport);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = Vector2.zero;
            grid.sizeDelta = new Vector2(0f, viewportH);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = yearH + spacing;
            // the viewport needs a raycast target to be dragged between the years
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;

            int poolRows = Mathf.CeilToInt(viewportH / (yearH + spacing)) + 1;
            var cells = new List<RectTransform>();
            var texts = new List<TextMeshProUGUI>();
            var sels = new List<Graphic>();
            var rings = new List<Graphic>();
            var its = new List<M3Interactive>();
            var pill = ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(DatePickerModalTokens.SelectionYearStateLayerShape));
            for (int i = 0; i < poolRows * 3; i++)
            {
                var year = M3Build.Rect("Year", grid);
                year.anchorMin = year.anchorMax = new Vector2(0f, 1f);
                year.pivot = new Vector2(0.5f, 0.5f);
                year.sizeDelta = new Vector2(yearW, yearH);
                year.anchoredPosition = new Vector2(HorizontalPadding + (i % 3 + 0.5f) * columnW, -((i / 3) * (yearH + spacing) + yearH / 2f));
                var sel = M3Build.Shape("Selected", year, pill, ColorRef.Role(DatePickerModalTokens.SelectionYearSelectedContainerColor));
                sel.raycastTarget = false;
                var ring = M3Build.Shape("Current", year, ShapeCell.Create(ShapeKind.Stroke).WithShape(CornerShape.Full).WithStroke(DatePickerModalTokens.DateTodayContainerOutlineWidth),
                    ColorRef.Role(DatePickerModalTokens.DateTodayContainerOutlineColor));
                ring.raycastTarget = false;
                var stateLayer = M3Build.Shape("StateLayer", year, pill, ColorRef.Clear);
                var ripple = M3Build.Shape("Ripple", year, ShapeCell.Create(ShapeKind.Ripple).WithShape(CornerShape.Full), ColorRef.Clear);
                var text = M3Build.Text("Label", year, "2026", DatePickerModalTokens.SelectionYearLabelTextFont, ColorRef.Role(DatePickerModalTokens.SelectionYearUnselectedLabelTextColor), inner);
                M3Build.Fill(text.rectTransform);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                var spec = new InteractionSpec
                {
                    Root = year, Context = inner, StateLayer = stateLayer, Ripple = ripple, RippleClipShape = CornerShape.Full,
                    RestShape = yearH / 2f, PressedShape = yearH / 2f, SelectedShape = yearH / 2f, SelectedPressedShape = yearH / 2f, MaxShape = yearH / 2f,
                };
                spec.ShapeLayers.Add((stateLayer, _ => pill));
                // Surface(selected, onClick): the state layer and ripple use the year's content color
                spec.Color(stateLayer, ColorRef.Over(bg, DatePickerModalTokens.SelectionYearUnselectedLabelTextColor, StateLayer.Hover),
                    ColorRef.Over(DatePickerModalTokens.SelectionYearSelectedContainerColor, DatePickerModalTokens.SelectionYearSelectedLabelTextColor, StateLayer.Hover), ColorRef.Clear, ColorRef.Clear);
                spec.RippleColor = ColorRef.Over(bg, DatePickerModalTokens.SelectionYearUnselectedLabelTextColor, StateLayer.Hover,
                    DatePickerModalTokens.SelectionYearUnselectedLabelTextColor, StateLayer.Pressed);
                spec.RippleColorSelected = ColorRef.Over(DatePickerModalTokens.SelectionYearSelectedContainerColor, DatePickerModalTokens.SelectionYearSelectedLabelTextColor, StateLayer.Hover,
                    DatePickerModalTokens.SelectionYearSelectedLabelTextColor, StateLayer.Pressed);
                var it = M3Interaction.Apply(spec);
                M3UdonBridge.Wire(year.GetComponent<Button>().onClick, picker, "_Year" + i);
                foreach (var g in new Graphic[] { text, sel, ring })
                {
                    var b = g.GetComponent<M3ColorBinding>();
                    if (b != null) Object.DestroyImmediate(b);
                }
                cells.Add(year); texts.Add(text); sels.Add(sel); rings.Add(ring); its.Add(it);
            }
            M3UdonBridge.Wire(scroll.onValueChanged, picker, nameof(M3DatePicker._YearScroll));

            picker.yearPanel = panel;
            picker.yearPanelContent = content;
            picker.yearGroup = group;
            picker.yearScroll = scroll;
            picker.yearScrollContent = grid;
            picker.yearCells = cells.ToArray();
            picker.yearTexts = texts.ToArray();
            picker.yearSelected = sels.ToArray();
            picker.yearRings = rings.ToArray();
            picker.yearInteractives = its.ToArray();
            picker.yearPanelHeight = panelH;
            picker.yearViewportHeight = viewportH;
            picker.yearRowHeight = yearH;
            picker.yearRowSpacing = spacing;
            picker.yearColumnWidth = columnW;
            picker.yearPadding = HorizontalPadding;
            picker.yearPickerVisible = visible;
            if (visible)
            {
                // initialFirstVisibleItemIndex = max(0, displayedYear - first - YearsInRow), as _ToggleYears does
                int shown = picker.displayedYear != 0 ? picker.displayedYear : picker.selectedDay > 0 ? picker.selectedYear : System.DateTime.Now.Year;
                int rows = (picker.maxYear - picker.minYear + 3) / 3;
                float contentH = rows * yearH + (rows - 1) * spacing;
                int index = Mathf.Max(0, shown - picker.minYear - 3);
                grid.anchoredPosition = new Vector2(0f, Mathf.Min((index / 3) * (yearH + spacing), contentH - viewportH));
            }
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
