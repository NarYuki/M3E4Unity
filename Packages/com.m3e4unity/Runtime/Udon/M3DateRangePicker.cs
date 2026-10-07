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
    /// Date range picker (DateRangePicker.kt): a vertical list of months (VerticalMonthsList), each a
    /// "MMMM yyyy" subhead and a 6 × 7 grid, with the selection range drawn behind the days
    /// (drawRangeBackground). Dates are yyyymmdd ints (0 = none). The months are a pool recycled as
    /// the list scrolls; the day cells call _D0 … _D(n). The input mode (DateRangeInputContent) has a
    /// start and an end text field, cross-faded with the list (FastEffects).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3DateRangePicker : UdonSharpBehaviour
#else
    public class M3DateRangePicker : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int startDate;          // yyyymmdd, 0 = none
        public int endDate;
        public int displayedYear;
        public int displayedMonth;
        public int minYear = 1900;
        public int maxYear = 2100;
        public bool inputMode;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Header")]
        public TextMeshProUGUI title;
        public string pickerTitle = "Select dates";
        public string inputTitle = "Enter dates";
        public TextMeshProUGUI headline;
        public string startHeadline = "Start date";
        public string endHeadline = "End date";
        public TextMeshProUGUI modeIcon;
        public string pickerModeGlyph;
        public string inputModeGlyph;
        [Tooltip("Enabled once an end date is selected (the sample's Save button).")]
        public Selectable saveButton;

        [Header("Months list")]
        public ScrollRect scroll;
        public RectTransform scrollContent;
        public float viewportHeight = 600f;
        public float monthHeight = 336f;      // subhead + 6 rows of 48 dp
        public float subheadHeight = 48f;     // CalendarMonthSubheadPadding top 20 + line + bottom 8
        public float gridWidth = 388f;        // the Column's width inside DatePickerHorizontalPadding
        public float cell = 48f;
        public float stateLayerHeight = 40f;  // DateStateLayerHeight
        public RectTransform[] months;        // pooled month items
        public TextMeshProUGUI[] subheads;
        public GameObject[] cells;            // 42 per pooled month
        public RectTransform[] cellRects;
        public TextMeshProUGUI[] dayTexts;
        public Graphic[] selectedCircles;
        public Graphic[] todayRings;
        public M3Interactive[] dayInteractives;
        public RectTransform[] rangeRows;     // 6 per pooled month
        public Graphic[] rangeGraphics;

        [Header("Input mode")]
        public CanvasGroup pickerGroup;
        public CanvasGroup inputGroup;
        public M3TextField startField;
        public M3TextField endField;
        public TMP_InputField startText;
        public TMP_InputField endText;
        public TextMeshProUGUI startError;
        public TextMeshProUGUI endError;
        public string patternError = "Date does not match expected pattern: MM/DD/YYYY";
        public string rangeError = "Date out of expected year range 1900 - 2100";
        public string invalidRangeError = "Invalid date range input";
        public float fastEffectsStiffness = 3800f;

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: day content, today content, selected content, selected container, today border, in-range content, in-range container.")]
        public int[] extraColors;

        const int Cells = 42;
        int firstMonth = -1;
        int todayDate;
        float pickerA = 1f;
        float pickerAV;
        float inputA;
        float inputAV;
        float lastTime;
        bool ticking;
        bool formatting;
        string lastStart = "";
        string lastEnd = "";

        void Start() { Snap(); }
        public void _M3Refresh() { Snap(); }
        public void _M3ThemeChanged() { firstMonth = -1; LayoutMonths(); }

        void Snap()
        {
            DateTime now = DateTime.Now;
            todayDate = now.Year * 10000 + now.Month * 100 + now.Day;
            if (displayedYear == 0)
            {
                if (startDate > 0) { displayedYear = startDate / 10000; displayedMonth = startDate / 100 % 100; }
                else { displayedYear = now.Year; displayedMonth = now.Month; }
            }
            ScrollToDisplayed();
            if (inputMode) ResetInputs();
            pickerA = inputMode ? 0f : 1f; pickerAV = 0f;
            inputA = inputMode ? 1f : 0f; inputAV = 0f;
            ApplyMode();
            Header();
        }

        int MonthCount() { return (maxYear - minYear + 1) * 12; }

        // rememberLazyListState(initialFirstVisibleItemIndex = monthIndex)
        void ScrollToDisplayed()
        {
            if (scrollContent == null) return;
            float contentH = MonthCount() * monthHeight;
            scrollContent.sizeDelta = new Vector2(scrollContent.sizeDelta.x, contentH);
            int index = Mathf.Max(0, (displayedYear - minYear) * 12 + displayedMonth - 1);
            float y = Mathf.Min(index * monthHeight, Mathf.Max(0f, contentH - viewportHeight));
            scrollContent.anchoredPosition = new Vector2(scrollContent.anchoredPosition.x, y);
            if (scroll != null) scroll.velocity = Vector2.zero;
            firstMonth = -1;
            LayoutMonths();
        }

        public void _Scroll() { LayoutMonths(); }

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

        static string Short(int date)
        {
            return MonthName(date / 100 % 100).Substring(0, 3) + " " + (date % 100) + ", " + (date / 10000);
        }

        void Header()
        {
            if (title != null) title.text = inputMode ? inputTitle : pickerTitle;
            if (headline != null)
                headline.text = (startDate > 0 ? Short(startDate) : startHeadline) + "<space=4>-<space=4>" + (endDate > 0 ? Short(endDate) : endHeadline);
            if (modeIcon != null) modeIcon.text = inputMode ? inputModeGlyph : pickerModeGlyph;
            if (saveButton != null) saveButton.interactable = endDate > 0;
        }

        void LayoutMonths()
        {
            if (scrollContent == null || months == null) return;
            int total = MonthCount();
            int first = Mathf.Clamp(Mathf.FloorToInt(scrollContent.anchoredPosition.y / monthHeight), 0, Mathf.Max(0, total - 1));
            // updateDisplayedMonth: the first visible item is the displayed month
            displayedYear = minYear + first / 12;
            displayedMonth = first % 12 + 1;
            if (first == firstMonth) return;
            firstMonth = first;
            for (int j = 0; j < months.Length; j++) FillMonth(j);
        }

        void RefreshMonths()
        {
            if (months == null) return;
            for (int j = 0; j < months.Length; j++) FillMonth(j);
        }

        void FillMonth(int j)
        {
            RectTransform rt = months[j];
            if (rt == null) return;
            int index = firstMonth + j;
            bool visible = index >= 0 && index < MonthCount();
            rt.gameObject.SetActive(visible);
            if (!visible) return;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -index * monthHeight);
            int year = minYear + index / 12;
            int month = index % 12 + 1;
            if (subheads[j] != null) subheads[j].text = MonthName(month) + " " + year;
            int days = DateTime.DaysInMonth(year, month);
            int offset = (int)new DateTime(year, month, 1).DayOfWeek; // Sunday first
            int monthStart = year * 10000 + month * 100 + 1;
            int monthEnd = monthStart + days - 1;
            bool range = startDate > 0 && endDate > 0;
            bool hasColors = theme != null && extraColors != null && extraColors.Length >= 7;
            for (int c = 0; c < Cells; c++)
            {
                int i = j * Cells + c;
                int day = c - offset + 1;
                bool inMonth = day >= 1 && day <= days;
                if (cells[i] != null) cells[i].SetActive(inMonth);
                if (!inMonth) continue;
                int date = monthStart + day - 1;
                bool selected = date == startDate || date == endDate;
                bool inRange = range && date >= startDate && date <= endDate;
                bool today = date == todayDate;
                if (dayTexts[i] != null) dayTexts[i].text = day.ToString();
                if (selectedCircles[i] != null) selectedCircles[i].gameObject.SetActive(selected);
                if (todayRings[i] != null) todayRings[i].gameObject.SetActive(today && !selected);
                if (dayInteractives != null && dayInteractives[i] != null)
                {
                    if (selected) dayInteractives[i]._Select(); else dayInteractives[i]._Deselect();
                }
                if (hasColors)
                {
                    // dayContentColor: selected, in range, today, default
                    int content = selected ? extraColors[2] : inRange ? extraColors[5] : today ? extraColors[1] : extraColors[0];
                    if (dayTexts[i] != null && content >= 0) dayTexts[i].color = theme.Get(content);
                    if (selectedCircles[i] != null && extraColors[3] >= 0) selectedCircles[i].color = theme.Get(extraColors[3]);
                    if (todayRings[i] != null && extraColors[4] >= 0) todayRings[i].color = theme.Get(extraColors[4]);
                }
            }

            // SelectedRangeInfo.calculateRangeInfo + drawRangeBackground
            bool show = range && !(startDate > monthEnd || endDate < monthStart);
            int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            bool firstIsStart = false, lastIsEnd = false;
            if (show)
            {
                firstIsStart = startDate >= monthStart;
                lastIsEnd = endDate <= monthEnd;
                int s = firstIsStart ? offset + startDate % 100 - 1 : offset;
                int e = lastIsEnd ? offset + endDate % 100 - 1 : offset + days - 1;
                x1 = s % 7; y1 = s / 7; x2 = e % 7; y2 = e / 7;
            }
            float space = (gridWidth - 7f * cell) / 7f;
            float startX = x1 * (cell + space) + (firstIsStart ? cell / 2f : 0f) + space / 2f;
            float endX = x2 * (cell + space) + (lastIsEnd ? cell / 2f : cell) + space / 2f;
            float pad = (cell - stateLayerHeight) / 2f;
            for (int r = 0; r < 6; r++)
            {
                RectTransform row = rangeRows[j * 6 + r];
                if (row == null) continue;
                bool on = show && r >= y1 && r <= y2;
                row.gameObject.SetActive(on);
                if (!on) continue;
                float a = r == y1 ? startX : 0f;
                float b = r == y2 ? endX : gridWidth;
                row.anchoredPosition = new Vector2(a, -(subheadHeight + r * cell + pad));
                row.sizeDelta = new Vector2(Mathf.Max(0f, b - a), stateLayerHeight);
                if (hasColors && extraColors[6] >= 0 && rangeGraphics[j * 6 + r] != null) rangeGraphics[j * 6 + r].color = theme.Get(extraColors[6]);
            }
        }

        void Pick(int n)
        {
            int j = n / Cells;
            int c = n % Cells;
            int index = firstMonth + j;
            if (index < 0 || index >= MonthCount()) return;
            int year = minYear + index / 12;
            int month = index % 12 + 1;
            int offset = (int)new DateTime(year, month, 1).DayOfWeek;
            int day = c - offset + 1;
            if (day < 1 || day > DateTime.DaysInMonth(year, month)) return;
            int date = year * 10000 + month * 100 + day;
            // updateDateSelection
            if ((startDate == 0 && endDate == 0) || (startDate != 0 && endDate != 0)) SetSelection(date, 0);
            else if (startDate != 0 && date >= startDate) SetSelection(startDate, date);
            else SetSelection(date, 0);
            RefreshMonths();
            Header();
            if (changeListener != null) changeListener.SendCustomEvent("_M3DateChanged");
        }

        // DateRangePickerStateImpl.setSelection: an end date needs a start date on or before it
        void SetSelection(int start, int end)
        {
            if (start > 0 && (end == 0 || start <= end)) { startDate = start; endDate = end; }
            else { startDate = 0; endDate = 0; }
        }

        // ---- display mode -----------------------------------------------------------------------

        public void _ToggleMode()
        {
            // displayMode setter: the start date's month is displayed
            if (startDate > 0) { displayedYear = startDate / 10000; displayedMonth = startDate / 100 % 100; }
            inputMode = !inputMode;
            if (inputMode) ResetInputs();
            else
            {
                ScrollToDisplayed();
#if !UDONSHARP
                if (startText != null) startText.DeactivateInputField();
                if (endText != null) endText.DeactivateInputField();
#endif
            }
            Header();
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
            // Crossfade(animationSpec = FastEffects)
            float ta = inputMode ? 0f : 1f;
            float x = pickerA - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(pickerAV) > 0.01f)
            {
                Step(x, pickerAV, dt, fastEffectsStiffness);
                pickerA = ta + stepX; pickerAV = stepV; busy = true;
            }
            else { pickerA = ta; pickerAV = 0f; }
            ta = 1f - ta;
            x = inputA - ta;
            if (Mathf.Abs(x) > 0.002f || Mathf.Abs(inputAV) > 0.01f)
            {
                Step(x, inputAV, dt, fastEffectsStiffness);
                inputA = ta + stepX; inputAV = stepV; busy = true;
            }
            else { inputA = ta; inputAV = 0f; }
            ApplyMode();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        float stepX;
        float stepV;

        void Step(float x0, float v0, float dt, float k)
        {
            float w = Mathf.Sqrt(k);
            float cb = v0 + w * x0;
            float e = Mathf.Exp(-w * dt);
            stepX = (x0 + cb * dt) * e;
            stepV = (cb - w * (x0 + cb * dt)) * e;
        }

        void ApplyMode()
        {
            if (pickerGroup != null)
            {
                pickerGroup.gameObject.SetActive(!inputMode || pickerA > 0.001f);
                pickerGroup.alpha = Mathf.Clamp01(pickerA);
                pickerGroup.interactable = !inputMode;
                pickerGroup.blocksRaycasts = !inputMode;
            }
            if (inputGroup != null)
            {
                inputGroup.gameObject.SetActive(inputMode || inputA > 0.001f);
                inputGroup.alpha = Mathf.Clamp01(inputA);
                inputGroup.interactable = inputMode;
                inputGroup.blocksRaycasts = inputMode;
            }
        }

        // ---- input mode (DateRangeInputContent) ---------------------------------------------------

        static string Pad2(int v) { return v < 10 ? "0" + v : v.ToString(); }
        static string Pad4(int v)
        {
            string s = v.ToString();
            while (s.Length < 4) s = "0" + s;
            return s;
        }

        static string Format(int date)
        {
            if (date <= 0) return "";
            return Pad2(date / 100 % 100) + "/" + Pad2(date % 100) + "/" + Pad4(date / 10000);
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

        static string WithDelimiters(string d)
        {
            if (d.Length <= 2) return d.Length == 2 ? d + "/" : d;
            if (d.Length <= 4) return d.Substring(0, 2) + "/" + d.Substring(2) + (d.Length == 4 ? "/" : "");
            return d.Substring(0, 2) + "/" + d.Substring(2, 2) + "/" + d.Substring(4);
        }

        void ResetInputs()
        {
            formatting = true;
            lastStart = Format(startDate);
            lastEnd = Format(endDate);
            if (startText != null) startText.text = lastStart;
            if (endText != null) endText.text = lastEnd;
            formatting = false;
            ShowError(startField, startError, "");
            ShowError(endField, endError, "");
            if (startField != null) startField._M3Refresh();
            if (endField != null) endField._M3Refresh();
        }

        void ShowError(M3TextField field, TextMeshProUGUI error, string message)
        {
            bool isError = message.Length > 0;
            if (error != null)
            {
                error.text = message;
                error.gameObject.SetActive(isError);
            }
            if (field != null)
            {
                if (isError) field._SetError(); else field._ClearError();
            }
        }

        /// <summary>The start field changed (its M3TextField change listener is a relay calling this).</summary>
        public void _StartChanged() { FieldChanged(true); }
        public void _EndChanged() { FieldChanged(false); }
        public void _StartEndEdit() { EndEdit(startText, true); }
        public void _EndEndEdit() { EndEdit(endText, false); }

        void FieldChanged(bool isStart)
        {
            TMP_InputField input = isStart ? startText : endText;
            if (formatting || input == null) return;
            string last = isStart ? lastStart : lastEnd;
            string raw = input.text;
            string d = Digits(raw);
            bool rejected = d.Length > 8;
            for (int i = 0; i < raw.Length && !rejected; i++)
            {
                char c = raw[i];
                if (!(c >= '0' && c <= '9') && c != '/') rejected = true;
            }
            if (rejected)
            {
                formatting = true;
                input.text = last;
                formatting = false;
                return;
            }
#if !UDONSHARP
            if (raw.Length < last.Length && d == Digits(last) && d.Length > 0) d = d.Substring(0, d.Length - 1);
            string shown = WithDelimiters(d);
            if (shown != raw)
            {
                formatting = true;
                input.text = shown;
                input.MoveTextEnd(false);
                formatting = false;
            }
#else
            string shown = raw;
#endif
            if (isStart) lastStart = shown; else lastEnd = shown;

            // DateInputTransformation + DateInputValidator (Start / EndDateInput)
            string error = "";
            int parsed = 0;
            if (d.Length == 8)
            {
                int month = int.Parse(d.Substring(0, 2));
                int day = int.Parse(d.Substring(2, 2));
                int year = int.Parse(d.Substring(4, 4));
                if (month < 1 || month > 12 || day < 1 || day > 31 || year < 1) error = patternError;
                else if (year < minYear || year > maxYear) error = rangeError;
                else
                {
                    int last2 = DateTime.DaysInMonth(year, month);
                    parsed = year * 10000 + month * 100 + (day > last2 ? last2 : day);
                    if (isStart && endDate > 0 && parsed > endDate) error = invalidRangeError;
                    if (!isStart && parsed < startDate) error = invalidRangeError;
                }
            }
            int value = error.Length == 0 ? parsed : 0;
            if (isStart) SetSelection(value, endDate); else SetSelection(startDate, value);
            if (isStart) ShowError(startField, startError, error); else ShowError(endField, endError, error);
            Header();
            if (changeListener != null) changeListener.SendCustomEvent("_M3DateChanged");
        }

        void EndEdit(TMP_InputField input, bool isStart)
        {
            if (input == null) return;
            string d = Digits(input.text);
            string shown = d.Length == 8 ? WithDelimiters(d) : input.text;
            if (shown != input.text)
            {
                formatting = true;
                input.text = shown;
                formatting = false;
            }
            if (isStart) lastStart = shown; else lastEnd = shown;
        }

        public void _D0() { Pick(0); } public void _D1() { Pick(1); } public void _D2() { Pick(2); } public void _D3() { Pick(3); } public void _D4() { Pick(4); } public void _D5() { Pick(5); }
        public void _D6() { Pick(6); } public void _D7() { Pick(7); } public void _D8() { Pick(8); } public void _D9() { Pick(9); } public void _D10() { Pick(10); } public void _D11() { Pick(11); }
        public void _D12() { Pick(12); } public void _D13() { Pick(13); } public void _D14() { Pick(14); } public void _D15() { Pick(15); } public void _D16() { Pick(16); } public void _D17() { Pick(17); }
        public void _D18() { Pick(18); } public void _D19() { Pick(19); } public void _D20() { Pick(20); } public void _D21() { Pick(21); } public void _D22() { Pick(22); } public void _D23() { Pick(23); }
        public void _D24() { Pick(24); } public void _D25() { Pick(25); } public void _D26() { Pick(26); } public void _D27() { Pick(27); } public void _D28() { Pick(28); } public void _D29() { Pick(29); }
        public void _D30() { Pick(30); } public void _D31() { Pick(31); } public void _D32() { Pick(32); } public void _D33() { Pick(33); } public void _D34() { Pick(34); } public void _D35() { Pick(35); }
        public void _D36() { Pick(36); } public void _D37() { Pick(37); } public void _D38() { Pick(38); } public void _D39() { Pick(39); } public void _D40() { Pick(40); } public void _D41() { Pick(41); }
        public void _D42() { Pick(42); } public void _D43() { Pick(43); } public void _D44() { Pick(44); } public void _D45() { Pick(45); } public void _D46() { Pick(46); } public void _D47() { Pick(47); }
        public void _D48() { Pick(48); } public void _D49() { Pick(49); } public void _D50() { Pick(50); } public void _D51() { Pick(51); } public void _D52() { Pick(52); } public void _D53() { Pick(53); }
        public void _D54() { Pick(54); } public void _D55() { Pick(55); } public void _D56() { Pick(56); } public void _D57() { Pick(57); } public void _D58() { Pick(58); } public void _D59() { Pick(59); }
        public void _D60() { Pick(60); } public void _D61() { Pick(61); } public void _D62() { Pick(62); } public void _D63() { Pick(63); } public void _D64() { Pick(64); } public void _D65() { Pick(65); }
        public void _D66() { Pick(66); } public void _D67() { Pick(67); } public void _D68() { Pick(68); } public void _D69() { Pick(69); } public void _D70() { Pick(70); } public void _D71() { Pick(71); }
        public void _D72() { Pick(72); } public void _D73() { Pick(73); } public void _D74() { Pick(74); } public void _D75() { Pick(75); } public void _D76() { Pick(76); } public void _D77() { Pick(77); }
        public void _D78() { Pick(78); } public void _D79() { Pick(79); } public void _D80() { Pick(80); } public void _D81() { Pick(81); } public void _D82() { Pick(82); } public void _D83() { Pick(83); }
        public void _D84() { Pick(84); } public void _D85() { Pick(85); } public void _D86() { Pick(86); } public void _D87() { Pick(87); } public void _D88() { Pick(88); } public void _D89() { Pick(89); }
        public void _D90() { Pick(90); } public void _D91() { Pick(91); } public void _D92() { Pick(92); } public void _D93() { Pick(93); } public void _D94() { Pick(94); } public void _D95() { Pick(95); }
        public void _D96() { Pick(96); } public void _D97() { Pick(97); } public void _D98() { Pick(98); } public void _D99() { Pick(99); } public void _D100() { Pick(100); } public void _D101() { Pick(101); }
        public void _D102() { Pick(102); } public void _D103() { Pick(103); } public void _D104() { Pick(104); } public void _D105() { Pick(105); } public void _D106() { Pick(106); } public void _D107() { Pick(107); }
        public void _D108() { Pick(108); } public void _D109() { Pick(109); } public void _D110() { Pick(110); } public void _D111() { Pick(111); } public void _D112() { Pick(112); } public void _D113() { Pick(113); }
        public void _D114() { Pick(114); } public void _D115() { Pick(115); } public void _D116() { Pick(116); } public void _D117() { Pick(117); } public void _D118() { Pick(118); } public void _D119() { Pick(119); }
        public void _D120() { Pick(120); } public void _D121() { Pick(121); } public void _D122() { Pick(122); } public void _D123() { Pick(123); } public void _D124() { Pick(124); } public void _D125() { Pick(125); }
        public void _D126() { Pick(126); } public void _D127() { Pick(127); } public void _D128() { Pick(128); } public void _D129() { Pick(129); } public void _D130() { Pick(130); } public void _D131() { Pick(131); }
        public void _D132() { Pick(132); } public void _D133() { Pick(133); } public void _D134() { Pick(134); } public void _D135() { Pick(135); } public void _D136() { Pick(136); } public void _D137() { Pick(137); }
        public void _D138() { Pick(138); } public void _D139() { Pick(139); } public void _D140() { Pick(140); } public void _D141() { Pick(141); } public void _D142() { Pick(142); } public void _D143() { Pick(143); }
        public void _D144() { Pick(144); } public void _D145() { Pick(145); } public void _D146() { Pick(146); } public void _D147() { Pick(147); } public void _D148() { Pick(148); } public void _D149() { Pick(149); }
        public void _D150() { Pick(150); } public void _D151() { Pick(151); } public void _D152() { Pick(152); } public void _D153() { Pick(153); } public void _D154() { Pick(154); } public void _D155() { Pick(155); }
        public void _D156() { Pick(156); } public void _D157() { Pick(157); } public void _D158() { Pick(158); } public void _D159() { Pick(159); } public void _D160() { Pick(160); } public void _D161() { Pick(161); }
        public void _D162() { Pick(162); } public void _D163() { Pick(163); } public void _D164() { Pick(164); } public void _D165() { Pick(165); } public void _D166() { Pick(166); } public void _D167() { Pick(167); }
        public void _D168() { Pick(168); } public void _D169() { Pick(169); } public void _D170() { Pick(170); } public void _D171() { Pick(171); } public void _D172() { Pick(172); } public void _D173() { Pick(173); }
        public void _D174() { Pick(174); } public void _D175() { Pick(175); } public void _D176() { Pick(176); } public void _D177() { Pick(177); } public void _D178() { Pick(178); } public void _D179() { Pick(179); }
        public void _D180() { Pick(180); } public void _D181() { Pick(181); } public void _D182() { Pick(182); } public void _D183() { Pick(183); } public void _D184() { Pick(184); } public void _D185() { Pick(185); }
        public void _D186() { Pick(186); } public void _D187() { Pick(187); } public void _D188() { Pick(188); } public void _D189() { Pick(189); } public void _D190() { Pick(190); } public void _D191() { Pick(191); }
        public void _D192() { Pick(192); } public void _D193() { Pick(193); } public void _D194() { Pick(194); } public void _D195() { Pick(195); } public void _D196() { Pick(196); } public void _D197() { Pick(197); }
        public void _D198() { Pick(198); } public void _D199() { Pick(199); } public void _D200() { Pick(200); } public void _D201() { Pick(201); } public void _D202() { Pick(202); } public void _D203() { Pick(203); }
        public void _D204() { Pick(204); } public void _D205() { Pick(205); } public void _D206() { Pick(206); } public void _D207() { Pick(207); } public void _D208() { Pick(208); } public void _D209() { Pick(209); }
    }
}
