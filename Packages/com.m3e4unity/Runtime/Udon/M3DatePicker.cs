#if UDONSHARP
using UdonSharp;
#endif
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Date picker (DatePicker.kt DatePickerContent / Month / Day): a 6 × 7 grid of 48 dp day cells
    /// for the displayed month (Sunday first, en-US formats: headline "MMM d, yyyy", month button
    /// "MMMM yyyy"), today outlined, the selected day filled. Day cells call _Day0 … _Day41; the
    /// arrows call _Prev / _Next.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3DatePicker : UdonSharpBehaviour
#else
    public class M3DatePicker : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int displayedYear;
        public int displayedMonth;     // 1..12
        public int selectedYear;
        public int selectedMonth;
        public int selectedDay;        // 0 = no selection
        public int minYear = 1900;
        public int maxYear = 2100;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts")]
        public TextMeshProUGUI headline;
        public string noSelectionHeadline = "Selected date";
        public TextMeshProUGUI monthYear;
        public GameObject[] cells;
        public TextMeshProUGUI[] dayTexts;
        public Graphic[] selectedCircles;
        public Graphic[] todayRings;
        public M3Interactive[] dayInteractives;
        public Selectable prevButton;
        public Selectable nextButton;

        [Header("Year picker (YearPicker)")]
        [Tooltip("Clipping panel over the weekdays and month grid; its height animates (expandVertically from the bottom).")]
        public RectTransform yearPanel;
        [Tooltip("Full-height content of the panel, anchored to the panel's bottom.")]
        public RectTransform yearPanelContent;
        public CanvasGroup yearGroup;
        public ScrollRect yearScroll;
        public RectTransform yearScrollContent;
        public RectTransform yearArrow;
        [Tooltip("Pooled year cells (YearsInRow per row), recycled as the grid scrolls; they call _Year0 … _YearN.")]
        public RectTransform[] yearCells;
        public TextMeshProUGUI[] yearTexts;
        public Graphic[] yearSelected;
        public Graphic[] yearRings;
        public M3Interactive[] yearInteractives;
        public float yearPanelHeight = 336f;     // 48 dp * (MaxCalendarRows + 1)
        public float yearViewportHeight = 335f;  // minus the divider below the grid
        public float yearRowHeight = 36f;        // SelectionYearContainerHeight
        public float yearRowSpacing = 16f;       // YearsVerticalPadding
        public float yearColumnWidth = 112f;     // (360 - 2 * 12) / YearsInRow
        public float yearPadding = 12f;          // DatePickerHorizontalPadding
        public bool yearPickerVisible;
        public float effectsStiffness = 1600f;   // DefaultEffects (size, fade in)
        public float fastEffectsStiffness = 3800f; // FastEffects (fade out)

        [Header("Display mode (SwitchableDateEntryContent)")]
        [Tooltip("DisplayMode.Input: the date is typed into a text field instead of picked.")]
        public bool inputMode;
        public string inputNoSelectionHeadline = "Entered date";
        public TextMeshProUGUI modeIcon;
        public string pickerModeGlyph;
        public string inputModeGlyph;
        [Tooltip("The dialog surface; its height is header + content + buttons.")]
        public RectTransform surface;
        [Tooltip("The clipping content area (SizeTransform(clip = true)).")]
        public RectTransform content;
        public RectTransform pickerPage;
        public CanvasGroup pickerGroup;
        public RectTransform inputPage;
        public CanvasGroup inputGroup;
        public M3TextField inputField;
        public TMP_InputField inputText;
        public TextMeshProUGUI inputError;
        public string patternError = "Date does not match expected pattern: MM/DD/YYYY";
        public string rangeError = "Date out of expected year range 1900 - 2100";
        public float patternErrorHeight = 16f;
        public float rangeErrorHeight = 16f;
        public float headerHeight = 120f;
        public float buttonsHeight = 56f;
        public float pickerHeight = 392f;
        public float inputHeight = 90f;
        public float parallax = 48f;             // the -48 dp parallax offset of the leaving / entering picker
        public float spatialDamping = 0.8f;      // DefaultSpatial (slides, size)
        public float spatialStiffness = 380f;

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: day content, today content, selected content, selected container, today border, year content, current year content, selected year content, selected year container.")]
        public int[] extraColors;

        int firstOffset;
        int daysInMonth;
        int todayYear;
        int todayMonth;
        int todayDay;

        const int YearsInRow = 3;
        int yearFirstRow;
        float panelH;
        float panelV;
        float panelA = 1f;
        float panelAV;
        float lastTime;
        bool ticking;

        // display mode animation: content height, page offsets (y down) and alphas
        float contentH;
        float contentV;
        float pickerY;
        float pickerYV;
        float pickerA = 1f;
        float pickerAV;
        float inputY;
        float inputYV;
        float inputA;
        float inputAV;
        float errorHeight;
        bool formatting;
        string lastInput = "";

        void Start()
        {
            Snap();
        }

        public void _M3Refresh()
        {
            Snap();
        }

        void Snap()
        {
            Refresh();
            panelH = yearPickerVisible ? yearPanelHeight : 0f;
            panelA = 1f;
            ApplyPanel();
            if (inputMode) ResetInput();
            contentH = ContentTarget(); contentV = 0f;
            pickerY = 0f; pickerA = inputMode ? 0f : 1f; pickerYV = 0f; pickerAV = 0f;
            inputY = inputMode ? 0f : inputHeight; inputA = inputMode ? 1f : 0f; inputYV = 0f; inputAV = 0f;
            ApplyMode();
        }
        public void _M3ThemeChanged() { Refresh(); }

        void Refresh()
        {
            DateTime now = DateTime.Now;
            todayYear = now.Year;
            todayMonth = now.Month;
            todayDay = now.Day;
            if (displayedYear == 0) { displayedYear = selectedDay > 0 ? selectedYear : todayYear; displayedMonth = selectedDay > 0 ? selectedMonth : todayMonth; }
            Layout();
        }

        static string MonthName(int m)
        {
            if (m == 1) return "January";
            if (m == 2) return "February";
            if (m == 3) return "March";
            if (m == 4) return "April";
            if (m == 5) return "May";
            if (m == 6) return "June";
            if (m == 7) return "July";
            if (m == 8) return "August";
            if (m == 9) return "September";
            if (m == 10) return "October";
            if (m == 11) return "November";
            return "December";
        }

        void Layout()
        {
            if (displayedMonth < 1) displayedMonth = 1;
            daysInMonth = DateTime.DaysInMonth(displayedYear, displayedMonth);
            // daysFromStartOfWeekToFirstOfMonth with Sunday as the first day of the week
            firstOffset = (int)new DateTime(displayedYear, displayedMonth, 1).DayOfWeek;
            if (monthYear != null) monthYear.text = MonthName(displayedMonth) + " " + displayedYear;
            if (headline != null)
                headline.text = selectedDay > 0
                    ? MonthName(selectedMonth).Substring(0, 3) + " " + selectedDay + ", " + selectedYear
                    : inputMode ? inputNoSelectionHeadline : noSelectionHeadline;
            if (modeIcon != null) modeIcon.text = inputMode ? inputModeGlyph : pickerModeGlyph;
            if (prevButton != null)
            {
                prevButton.interactable = !(displayedYear <= minYear && displayedMonth == 1);
                prevButton.gameObject.SetActive(!yearPickerVisible); // the arrows only show with the month grid
            }
            if (nextButton != null)
            {
                nextButton.interactable = !(displayedYear >= maxYear && displayedMonth == 12);
                nextButton.gameObject.SetActive(!yearPickerVisible);
            }
            if (yearArrow != null) yearArrow.localEulerAngles = new Vector3(0f, 0f, yearPickerVisible ? 180f : 0f);
            LayoutYears();
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                int day = i - firstOffset + 1;
                bool inMonth = day >= 1 && day <= daysInMonth;
                if (cells[i] != null) cells[i].SetActive(inMonth);
                if (!inMonth) continue;
                bool isSelected = selectedDay > 0 && selectedYear == displayedYear && selectedMonth == displayedMonth && selectedDay == day;
                bool isToday = todayYear == displayedYear && todayMonth == displayedMonth && todayDay == day;
                if (dayTexts[i] != null) dayTexts[i].text = day.ToString();
                if (selectedCircles[i] != null) selectedCircles[i].gameObject.SetActive(isSelected);
                if (todayRings[i] != null) todayRings[i].gameObject.SetActive(isToday && !isSelected);
                if (dayInteractives != null && dayInteractives[i] != null)
                {
                    if (isSelected) dayInteractives[i]._Select(); else dayInteractives[i]._Deselect();
                }
                if (theme != null && extraColors != null && extraColors.Length >= 5)
                {
                    int content = isSelected ? extraColors[2] : isToday ? extraColors[1] : extraColors[0];
                    if (dayTexts[i] != null && content >= 0) dayTexts[i].color = theme.Get(content);
                    if (selectedCircles[i] != null && extraColors[3] >= 0) selectedCircles[i].color = theme.Get(extraColors[3]);
                    if (todayRings[i] != null && extraColors[4] >= 0) todayRings[i].color = theme.Get(extraColors[4]);
                }
            }
        }

        void Pick(int cell)
        {
            int day = cell - firstOffset + 1;
            if (day < 1 || day > daysInMonth) return;
            selectedYear = displayedYear;
            selectedMonth = displayedMonth;
            selectedDay = day;
            Layout();
            if (changeListener != null) changeListener.SendCustomEvent("_M3DateChanged");
        }

        public void _Prev()
        {
            if (displayedYear <= minYear && displayedMonth == 1) return;
            displayedMonth--;
            if (displayedMonth < 1) { displayedMonth = 12; displayedYear--; }
            Layout();
        }

        public void _Next()
        {
            if (displayedYear >= maxYear && displayedMonth == 12) return;
            displayedMonth++;
            if (displayedMonth > 12) { displayedMonth = 1; displayedYear++; }
            Layout();
        }

        // ---- year picker -------------------------------------------------------------------------

        int YearRows() { return (maxYear - minYear + YearsInRow) / YearsInRow; }
        float YearPitch() { return yearRowHeight + yearRowSpacing; }

        /// <summary>The year menu button: shows / hides the year grid.</summary>
        public void _ToggleYears()
        {
            yearPickerVisible = !yearPickerVisible;
            if (yearPickerVisible)
            {
                // rememberLazyGridState(initialFirstVisibleItemIndex = max(0, displayedYear - first - YearsInRow))
                if (yearScrollContent != null)
                {
                    int index = Mathf.Max(0, displayedYear - minYear - YearsInRow);
                    float contentH = YearRows() * yearRowHeight + (YearRows() - 1) * yearRowSpacing;
                    float y = Mathf.Min((index / YearsInRow) * YearPitch(), Mathf.Max(0f, contentH - yearViewportHeight));
                    yearScrollContent.anchoredPosition = new Vector2(yearScrollContent.anchoredPosition.x, y);
                }
                if (yearScroll != null) yearScroll.velocity = Vector2.zero;
                if (panelH <= 0.5f) panelA = 0.6f; // fadeIn(initialAlpha = 0.6f)
            }
            Layout();
            Kick();
        }

        public void _YearScroll() { LayoutYears(); }

        void PickYear(int cell)
        {
            int year = minYear + (yearFirstRow + cell / YearsInRow) * YearsInRow + cell % YearsInRow;
            if (year < minYear || year > maxYear) return;
            displayedYear = year; // keeps the displayed month
            _ToggleYears();
        }

        void LayoutYears()
        {
            if (yearCells == null || yearScrollContent == null) return;
            int rows = YearRows();
            float pitch = YearPitch();
            float contentH = rows * yearRowHeight + (rows - 1) * yearRowSpacing;
            yearScrollContent.sizeDelta = new Vector2(yearScrollContent.sizeDelta.x, contentH);
            int poolRows = yearCells.Length / YearsInRow;
            yearFirstRow = Mathf.Clamp(Mathf.FloorToInt(yearScrollContent.anchoredPosition.y / pitch), 0, Mathf.Max(0, rows - poolRows));
            bool hasExtra = theme != null && extraColors != null && extraColors.Length >= 9;
            for (int i = 0; i < yearCells.Length; i++)
            {
                RectTransform rt = yearCells[i];
                if (rt == null) continue;
                int row = yearFirstRow + i / YearsInRow;
                int col = i % YearsInRow;
                int year = minYear + row * YearsInRow + col;
                bool visible = year <= maxYear;
                rt.gameObject.SetActive(visible);
                if (!visible) continue;
                // each fixed column is (width - 2 * padding) / YearsInRow wide; the 72 x 36 year is centered in it
                rt.anchoredPosition = new Vector2(yearPadding + (col + 0.5f) * yearColumnWidth, -(row * pitch + yearRowHeight / 2f));
                bool selected = year == displayedYear;
                bool current = year == todayYear;
                if (yearTexts[i] != null) yearTexts[i].text = year.ToString();
                if (yearSelected[i] != null) yearSelected[i].gameObject.SetActive(selected);
                if (yearRings[i] != null) yearRings[i].gameObject.SetActive(current && !selected);
                if (yearInteractives != null && yearInteractives[i] != null)
                {
                    if (selected) yearInteractives[i]._Select(); else yearInteractives[i]._Deselect();
                }
                if (hasExtra)
                {
                    int content = selected ? extraColors[7] : current ? extraColors[6] : extraColors[5];
                    if (yearTexts[i] != null && content >= 0) yearTexts[i].color = theme.Get(content);
                    if (yearSelected[i] != null && extraColors[8] >= 0) yearSelected[i].color = theme.Get(extraColors[8]);
                    if (yearRings[i] != null && extraColors[4] >= 0) yearRings[i].color = theme.Get(extraColors[4]);
                }
            }
        }

        void Kick()
        {
            if (yearPanel != null && yearPickerVisible) yearPanel.gameObject.SetActive(true);
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            // expandVertically / shrinkVertically: DefaultEffects on the size (visibility threshold 1 px)
            float th = yearPickerVisible ? yearPanelHeight : 0f;
            float x = panelH - th;
            if (Mathf.Abs(x) > 0.5f || Mathf.Abs(panelV) > 1f)
            {
                Step(x, panelV, dt, effectsStiffness);
                panelH = th + stepX; panelV = stepV; busy = true;
            }
            else { panelH = th; panelV = 0f; }
            // fadeIn: DefaultEffects, fadeOut: FastEffects
            float ta = yearPickerVisible ? 1f : 0f;
            x = panelA - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(panelAV) > 0.01f)
            {
                Step(x, panelAV, dt, yearPickerVisible ? effectsStiffness : fastEffectsStiffness);
                panelA = ta + stepX; panelAV = stepV; busy = true;
            }
            else { panelA = ta; panelAV = 0f; }
            ApplyPanel();

            // display mode: SizeTransform (DefaultSpatial), slides (DefaultSpatial, 1 px threshold),
            // fade in DefaultEffects, fade out FastEffects
            float tc = ContentTarget();
            x = contentH - tc;
            if (Mathf.Abs(x) > 0.5f || Mathf.Abs(contentV) > 1f)
            {
                StepZ(x, contentV, dt, spatialDamping, spatialStiffness);
                contentH = tc + stepX; contentV = stepV; busy = true;
            }
            else { contentH = tc; contentV = 0f; }
            // the picker rests at 0 when shown and leaves / enters at -parallax
            float tpy = inputMode ? -parallax : 0f;
            x = pickerY - tpy;
            if (Mathf.Abs(x) > 0.5f || Mathf.Abs(pickerYV) > 1f)
            {
                StepZ(x, pickerYV, dt, spatialDamping, spatialStiffness);
                pickerY = tpy + stepX; pickerYV = stepV; busy = true;
            }
            else { pickerY = tpy; pickerYV = 0f; }
            // the input enters from / leaves to its full height below
            float tiy = inputMode ? 0f : inputHeight;
            x = inputY - tiy;
            if (Mathf.Abs(x) > 0.5f || Mathf.Abs(inputYV) > 1f)
            {
                StepZ(x, inputYV, dt, spatialDamping, spatialStiffness);
                inputY = tiy + stepX; inputYV = stepV; busy = true;
            }
            else { inputY = tiy; inputYV = 0f; }
            ta = inputMode ? 0f : 1f;
            x = pickerA - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(pickerAV) > 0.01f)
            {
                Step(x, pickerAV, dt, inputMode ? fastEffectsStiffness : effectsStiffness);
                pickerA = ta + stepX; pickerAV = stepV; busy = true;
            }
            else { pickerA = ta; pickerAV = 0f; }
            ta = inputMode ? 1f : 0f;
            x = inputA - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(inputAV) > 0.01f)
            {
                Step(x, inputAV, dt, inputMode ? effectsStiffness : fastEffectsStiffness);
                inputA = ta + stepX; inputAV = stepV; busy = true;
            }
            else { inputA = ta; inputAV = 0f; }
            ApplyMode();

            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        float stepX;
        float stepV;

        // critically damped spring (the effects springs have damping ratio 1)
        void Step(float x0, float v0, float dt, float k)
        {
            float w = Mathf.Sqrt(k);
            float cb = v0 + w * x0;
            float e = Mathf.Exp(-w * dt);
            stepX = (x0 + cb * dt) * e;
            stepV = (cb - w * (x0 + cb * dt)) * e;
        }

        void StepZ(float x0, float v0, float dt, float z, float k)
        {
            if (z >= 1f) { Step(x0, v0, dt, k); return; }
            float w = Mathf.Sqrt(k);
            float wd = w * Mathf.Sqrt(1f - z * z);
            float sc = (z * w * x0 + v0) / wd;
            float e = Mathf.Exp(-z * w * dt);
            float cs = Mathf.Cos(wd * dt);
            float sn = Mathf.Sin(wd * dt);
            stepX = e * (x0 * cs + sc * sn);
            stepV = -z * w * stepX + e * (-x0 * wd * sn + sc * wd * cs);
        }

        // ---- display mode (DatePicker displayMode / DateInputContent) -------------------------------

        float ContentTarget() { return inputMode ? inputHeight + errorHeight : pickerHeight; }

        /// <summary>The mode toggle: DisplayMode.Picker ↔ DisplayMode.Input.</summary>
        public void _ToggleMode()
        {
            // DatePickerStateImpl.displayMode: switching shows the selected date's month
            if (selectedDay > 0) { displayedYear = selectedYear; displayedMonth = selectedMonth; }
            inputMode = !inputMode;
            if (yearPickerVisible) { yearPickerVisible = false; panelH = 0f; panelA = 1f; ApplyPanel(); }
            if (inputMode)
            {
                // AnimatedContent composes a fresh DateInputContent: its text starts from the selection
                ResetInput();
                if (inputA <= 0.001f) inputY = inputHeight;
                if (inputPage != null) inputPage.SetAsLastSibling();
            }
            else
            {
                if (pickerA <= 0.001f) pickerY = -parallax;
                if (pickerPage != null) pickerPage.SetAsLastSibling();
#if !UDONSHARP
                if (inputText != null) inputText.DeactivateInputField();
#endif
            }
            Layout();
            Kick();
        }

        void ResetInput()
        {
            errorHeight = 0f;
            SetError("", 0f);
            string text = "";
            if (selectedDay > 0) text = Pad2(selectedMonth) + "/" + Pad2(selectedDay) + "/" + Pad4(selectedYear);
            lastInput = text;
            if (inputText != null)
            {
                formatting = true;
                inputText.text = text;
                formatting = false;
            }
            if (inputField != null) inputField._M3Refresh();
        }

        static string Pad2(int v) { return v < 10 ? "0" + v : v.ToString(); }
        static string Pad4(int v)
        {
            string s = v.ToString();
            while (s.Length < 4) s = "0" + s;
            return s;
        }

        static string Digits(string s)
        {
            string d = "";
            if (s == null) return d;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') d += c;
            }
            return d;
        }

        // DateOutputTransformation: the delimiters are shown after the 2nd and 4th digits
        static string WithDelimiters(string d)
        {
            if (d.Length <= 2) return d.Length == 2 ? d + "/" : d;
            if (d.Length <= 4) return d.Substring(0, 2) + "/" + d.Substring(2) + (d.Length == 4 ? "/" : "");
            return d.Substring(0, 2) + "/" + d.Substring(2, 2) + "/" + d.Substring(4);
        }

        /// <summary>The input text changed (M3TextField change listener).</summary>
        public void _M3TextChanged()
        {
            if (formatting || inputText == null) return;
            string raw = inputText.text;
            string d = Digits(raw);
            // DateInputTransformation rejects input beyond the pattern length or with other characters
            bool rejected = d.Length > 8;
            for (int i = 0; i < raw.Length && !rejected; i++)
            {
                char c = raw[i];
                if (!(c >= '0' && c <= '9') && c != '/') rejected = true;
            }
            if (rejected)
            {
                formatting = true;
                inputText.text = lastInput;
                formatting = false;
                return;
            }
#if !UDONSHARP
            // the caret can only be moved outside Udon: there the delimiters are added as the digits are typed.
            // The delimiters are display-only in Compose, so deleting one deletes the digit before it.
            if (raw.Length < lastInput.Length && d == Digits(lastInput) && d.Length > 0) d = d.Substring(0, d.Length - 1);
            string shown = WithDelimiters(d);
            if (shown != raw)
            {
                formatting = true;
                inputText.text = shown;
                inputText.MoveTextEnd(false);
                formatting = false;
            }
            lastInput = shown;
#else
            lastInput = raw;
#endif
            Validate(d);
        }

        /// <summary>End of editing: shows the delimiters (in Udon the text is only reformatted here).</summary>
        public void _InputEndEdit()
        {
            if (inputText == null) return;
            string d = Digits(inputText.text);
            string shown = d.Length == 8 ? WithDelimiters(d) : inputText.text;
            if (shown != inputText.text)
            {
                formatting = true;
                inputText.text = shown;
                formatting = false;
            }
            lastInput = shown;
        }

        // DateInputValidator.validate for the "MMddyyyy" pattern (java.time SMART resolution: a day past the
        // month's end resolves to its last day)
        void Validate(string d)
        {
            bool hadSelection = selectedDay > 0;
            if (d.Length < 8)
            {
                selectedDay = 0;
                SetError("", 0f);
            }
            else
            {
                int month = int.Parse(d.Substring(0, 2));
                int day = int.Parse(d.Substring(2, 2));
                int year = int.Parse(d.Substring(4, 4));
                if (month < 1 || month > 12 || day < 1 || day > 31 || year < 1)
                {
                    selectedDay = 0;
                    SetError(patternError, patternErrorHeight);
                }
                else if (year < minYear || year > maxYear)
                {
                    selectedDay = 0;
                    SetError(rangeError, rangeErrorHeight);
                }
                else
                {
                    int last = DateTime.DaysInMonth(year, month);
                    selectedYear = year; selectedMonth = month; selectedDay = day > last ? last : day;
                    SetError("", 0f);
                }
            }
            Layout();
            Kick();
            if (hadSelection || selectedDay > 0)
                if (changeListener != null) changeListener.SendCustomEvent("_M3DateChanged");
        }

        void SetError(string message, float height)
        {
            bool isError = message.Length > 0;
            errorHeight = isError ? height : 0f;
            if (inputError != null)
            {
                inputError.text = message;
                inputError.gameObject.SetActive(isError);
            }
            if (inputField != null)
            {
                if (isError) inputField._SetError(); else inputField._ClearError();
            }
        }

        void ApplyMode()
        {
            if (content == null) return;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Round(contentH));
            if (surface != null) surface.sizeDelta = new Vector2(surface.sizeDelta.x, headerHeight + Mathf.Round(contentH) + buttonsHeight);
            if (pickerPage != null)
            {
                bool shown = !inputMode || pickerA > 0.001f;
                pickerPage.gameObject.SetActive(shown);
                pickerPage.anchoredPosition = new Vector2(pickerPage.anchoredPosition.x, -Mathf.Round(pickerY));
            }
            if (pickerGroup != null)
            {
                pickerGroup.alpha = Mathf.Clamp01(pickerA);
                pickerGroup.interactable = !inputMode;
                pickerGroup.blocksRaycasts = !inputMode;
            }
            if (inputPage != null)
            {
                bool shown = inputMode || inputA > 0.001f;
                inputPage.gameObject.SetActive(shown);
                inputPage.anchoredPosition = new Vector2(inputPage.anchoredPosition.x, -Mathf.Round(inputY));
                inputPage.sizeDelta = new Vector2(inputPage.sizeDelta.x, inputHeight + errorHeight);
            }
            if (inputGroup != null)
            {
                inputGroup.alpha = Mathf.Clamp01(inputA);
                inputGroup.interactable = inputMode;
                inputGroup.blocksRaycasts = inputMode;
            }
        }

        void ApplyPanel()
        {
            if (yearPanel == null) return;
            bool shown = yearPickerVisible || panelH > 0.5f;
            yearPanel.gameObject.SetActive(shown);
            yearPanel.sizeDelta = new Vector2(yearPanel.sizeDelta.x, Mathf.Max(0f, Mathf.Round(panelH)));
            if (yearPanelContent != null) yearPanelContent.sizeDelta = new Vector2(yearPanelContent.sizeDelta.x, yearPanelHeight);
            if (yearGroup != null)
            {
                yearGroup.alpha = Mathf.Clamp01(panelA);
                yearGroup.interactable = yearPickerVisible;
                yearGroup.blocksRaycasts = shown;
            }
        }

        public void _Year0() { PickYear(0); } public void _Year1() { PickYear(1); } public void _Year2() { PickYear(2); }
        public void _Year3() { PickYear(3); } public void _Year4() { PickYear(4); } public void _Year5() { PickYear(5); }
        public void _Year6() { PickYear(6); } public void _Year7() { PickYear(7); } public void _Year8() { PickYear(8); }
        public void _Year9() { PickYear(9); } public void _Year10() { PickYear(10); } public void _Year11() { PickYear(11); }
        public void _Year12() { PickYear(12); } public void _Year13() { PickYear(13); } public void _Year14() { PickYear(14); }
        public void _Year15() { PickYear(15); } public void _Year16() { PickYear(16); } public void _Year17() { PickYear(17); }
        public void _Year18() { PickYear(18); } public void _Year19() { PickYear(19); } public void _Year20() { PickYear(20); }
        public void _Year21() { PickYear(21); } public void _Year22() { PickYear(22); } public void _Year23() { PickYear(23); }

        public void _Day0() { Pick(0); } public void _Day1() { Pick(1); } public void _Day2() { Pick(2); } public void _Day3() { Pick(3); }
        public void _Day4() { Pick(4); } public void _Day5() { Pick(5); } public void _Day6() { Pick(6); } public void _Day7() { Pick(7); }
        public void _Day8() { Pick(8); } public void _Day9() { Pick(9); } public void _Day10() { Pick(10); } public void _Day11() { Pick(11); }
        public void _Day12() { Pick(12); } public void _Day13() { Pick(13); } public void _Day14() { Pick(14); } public void _Day15() { Pick(15); }
        public void _Day16() { Pick(16); } public void _Day17() { Pick(17); } public void _Day18() { Pick(18); } public void _Day19() { Pick(19); }
        public void _Day20() { Pick(20); } public void _Day21() { Pick(21); } public void _Day22() { Pick(22); } public void _Day23() { Pick(23); }
        public void _Day24() { Pick(24); } public void _Day25() { Pick(25); } public void _Day26() { Pick(26); } public void _Day27() { Pick(27); }
        public void _Day28() { Pick(28); } public void _Day29() { Pick(29); } public void _Day30() { Pick(30); } public void _Day31() { Pick(31); }
        public void _Day32() { Pick(32); } public void _Day33() { Pick(33); } public void _Day34() { Pick(34); } public void _Day35() { Pick(35); }
        public void _Day36() { Pick(36); } public void _Day37() { Pick(37); } public void _Day38() { Pick(38); } public void _Day39() { Pick(39); }
        public void _Day40() { Pick(40); } public void _Day41() { Pick(41); }
    }
}
