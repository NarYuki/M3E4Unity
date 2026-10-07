#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Time input (TimePicker.kt TimeInputImpl / TimePickerTextField / TimeInputTransformation, 12-hour):
    /// an hour and a minute field. The selected field is an outlined text field (OutlinedTextFieldDefaults
    /// container, 2 dp border when focused, 1 dp otherwise); the other one looks like a TimeSelector.
    /// Input accepts digits only; a third digit replaces the value with the typed digit; the hour is
    /// valid in 0 … 11 (AM) or 12 … 23 (PM); a valid hour typed with two digits, or a new digit 2 … 9,
    /// moves on to the minute. Invalid values show in the error colors with the error supporting text.
    /// Events: _HourChanged, _MinuteChanged, _HourSelect, _MinuteSelect, _HourDeselect, _MinuteDeselect, _AM, _PM.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TimeInput : UdonSharpBehaviour
#else
    public class M3TimeInput : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int hour = 9;        // 0..23
        public int minute;          // 0..59
        public bool minuteSelected;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Parts (hour, minute)")]
        public TMP_InputField[] inputs;
        public TextMeshProUGUI[] texts;
        public Graphic[] containers;
        public GameObject[] thinBorders;     // 1 dp outline (unfocused)
        public GameObject[] thickBorders;    // 2 dp outline (focused)
        public Graphic[] thinBorderGraphics;
        public Graphic[] thickBorderGraphics;
        public TextMeshProUGUI[] supporting;
        public M3Interactive amToggle;
        public M3Interactive pmToggle;
        [Tooltip("Standard: the supporting text (Hour / Minute) always shows; vibrant: only for errors.")]
        public bool alwaysShowSupporting = true;
        public float fieldHeight = 72f;
        public float supportingHeight = 39f;  // SupportLabelTop 7 + two bodySmall lines
        [Tooltip("The content height (the fields, plus the supporting text when it shows).")]
        public float contentHeight = 111f;
        public string hourLabel = "Hour";
        public string minuteLabel = "Minute";
        public string hourError = "Hour must be 1–12";
        public string minuteError = "Minute must be 0–59";

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: caret, -, selection, container, focused container, text, focused text, focused border, border, error container, error, on error container, supporting.")]
        public int[] extraColors;

        int hourInput;
        int minuteInput;
        bool hourFocused;
        bool minuteFocused;
        bool formatting;
        string lastHour = "";
        string lastMinute = "";

        void Start() { Refresh(); }
        public void _M3Refresh() { Refresh(); }
        public void _M3ThemeChanged() { Style(); }

        bool IsPm() { return hour >= 12; }
        bool HourValid() { return IsPm() ? hourInput >= 12 && hourInput <= 23 : hourInput >= 0 && hourInput <= 11; }
        bool MinuteValid() { return minuteInput >= 0 && minuteInput <= 59; }

        static string Two(int v) { return v >= 0 && v < 10 ? "0" + v : v.ToString(); }

        // hourTextValue / minuteTextValue
        string HourText()
        {
            if (HourValid()) { int h = hour % 12; return Two(h == 0 ? 12 : h); }
            return Two(hourInput);
        }

        string MinuteText() { return MinuteValid() ? Two(minute) : Two(minuteInput); }

        /// <summary>Re-reads hour / minute (e.g. after the dialog switched modes) and resets the fields.</summary>
        void Refresh()
        {
            hourInput = hour;
            minuteInput = minute;
            // the selected field requests focus (LaunchedEffect(selection))
            hourFocused = !minuteSelected;
            minuteFocused = minuteSelected;
            SetText(0, HourText());
            SetText(1, MinuteText());
            Toggles();
            Style();
        }

        void SetText(int i, string s)
        {
            if (i == 0) lastHour = s; else lastMinute = s;
            if (inputs == null || inputs[i] == null) return;
            formatting = true;
            inputs[i].text = s;
            formatting = false;
        }

        void Toggles()
        {
            if (amToggle != null) { if (IsPm()) amToggle._Deselect(); else amToggle._Select(); }
            if (pmToggle != null) { if (IsPm()) pmToggle._Select(); else pmToggle._Deselect(); }
        }

        void Select(bool minutes)
        {
            if (minuteSelected == minutes) return;
            minuteSelected = minutes;
            // the field that is no longer selected shows its value as a TimeSelector
            if (minutes) SetText(0, HourText()); else SetText(1, MinuteText());
#if !UDONSHARP
            if (inputs != null && inputs[minutes ? 1 : 0] != null) inputs[minutes ? 1 : 0].ActivateInputField();
#endif
            Style();
        }

        public void _HourSelect() { hourFocused = true; Select(false); Style(); }
        public void _MinuteSelect() { minuteFocused = true; Select(true); Style(); }
        public void _HourDeselect() { hourFocused = false; Style(); }
        public void _MinuteDeselect() { minuteFocused = false; Style(); }
        public void _HourChanged() { Changed(0); }
        public void _MinuteChanged() { Changed(1); }

        void Changed(int i)
        {
            if (formatting || inputs == null || inputs[i] == null) return;
            string last = i == 0 ? lastHour : lastMinute;
            string text = inputs[i].text;
            if (text.Length == 0)
            {
                if (i == 0) { hourInput = IsPm() ? 12 : 0; hour = hourInput; }
                else { minuteInput = 0; minute = 0; }
                if (i == 0) lastHour = text; else lastMinute = text;
                Done();
                return;
            }
            for (int k = 0; k < text.Length; k++)
            {
                char c = text[k];
                if (c < '0' || c > '9') { SetText(i, last); return; }
            }
            bool inserted = text.Length > last.Length;
            // isReplacingTwoDigits: the typed digit replaces both
            if (last.Length == 2 && text.Length == 3)
            {
                int at = 0;
                while (at < 2 && last[at] == text[at]) at++;
                text = text.Substring(at, 1);
                SetText(i, text);
            }
            if (text.Length > 2) { SetText(i, last); return; }
            int value = int.Parse(text);
            if (i == 0)
            {
                int target = value == 12 ? (IsPm() ? 12 : 0) : IsPm() ? value + 12 : value;
                hourInput = target;
                if (HourValid()) hour = target;
                lastHour = text;
                bool autoAdvance = (text.Length == 2 || (inserted && value >= 2 && value <= 9)) && HourValid();
                if (autoAdvance) Select(true);
            }
            else
            {
                minuteInput = value;
                if (MinuteValid()) minute = value;
                lastMinute = text;
            }
            Done();
        }

        void Done()
        {
            Style();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        public void _AM()
        {
            if (IsPm() && HourValid()) { hour -= 12; hourInput = hour; }
            Toggles();
            Done();
        }

        public void _PM()
        {
            if (!IsPm() && HourValid()) { hour += 12; hourInput = hour; }
            Toggles();
            Done();
        }

        Color Pal(int slot)
        {
            if (theme == null || extraColors == null || slot >= extraColors.Length || extraColors[slot] < 0) return Color.clear;
            return theme.Get(extraColors[slot]);
        }

        void Style()
        {
            bool anyError = !HourValid() || !MinuteValid();
            contentHeight = fieldHeight + (alwaysShowSupporting || anyError ? supportingHeight : 0f);
            for (int i = 0; i < 2; i++)
            {
                bool selected = (i == 1) == minuteSelected;
                bool valid = i == 0 ? HourValid() : MinuteValid();
                bool focused = i == 0 ? hourFocused : minuteFocused;
                Color container;
                Color text;
                if (selected)
                {
                    container = !valid ? Pal(9) : focused ? Pal(4) : Pal(3);
                    text = valid ? Pal(6) : Pal(10);
                }
                else
                {
                    container = valid ? Pal(3) : Pal(9);
                    text = valid ? Pal(5) : Pal(11);
                }
                if (containers != null && containers[i] != null) containers[i].color = container;
                if (texts != null && texts[i] != null) texts[i].color = text;
                Color border = !valid ? Pal(10) : focused ? Pal(7) : Pal(8);
                if (thinBorders != null && thinBorders[i] != null) thinBorders[i].SetActive(selected && !focused);
                if (thickBorders != null && thickBorders[i] != null) thickBorders[i].SetActive(selected && focused);
                if (thinBorderGraphics != null && thinBorderGraphics[i] != null) thinBorderGraphics[i].color = border;
                if (thickBorderGraphics != null && thickBorderGraphics[i] != null) thickBorderGraphics[i].color = border;
                if (supporting != null && supporting[i] != null)
                {
                    bool show = alwaysShowSupporting || !valid;
                    supporting[i].gameObject.SetActive(show);
                    supporting[i].text = valid ? (i == 0 ? hourLabel : minuteLabel) : (i == 0 ? hourError : minuteError);
                    supporting[i].color = valid ? Pal(12) : Pal(10);
                }
            }
        }
    }
}
