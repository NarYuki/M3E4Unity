#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Time scroll (TimePicker.kt TimeScrollImpl, 12-hour): an hour ScrollField (12 items shown as
    /// 1 … 12) and a minute ScrollField (60 items) with the period toggle. The hour option is
    /// (hour % 12) - 1 (11 for 12 o'clock); a selected option sets hour = (option + 1) % 12 (+ 12 for PM).
    /// Events: _HourScrolled, _MinuteScrolled, _AM, _PM.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TimeScroll : UdonSharpBehaviour
#else
    public class M3TimeScroll : M3BehaviourBase
#endif
    {
        public int hour = 9;
        public int minute;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif
        public M3ScrollField hourField;
        public M3ScrollField minuteField;
        public M3Interactive amToggle;
        public M3Interactive pmToggle;

        void Start() { Refresh(); }
        public void _M3Refresh() { Refresh(); }

        int HourOption() { int h = hour % 12; return h == 0 ? 11 : h - 1; }

        void Refresh()
        {
            if (hourField != null) { hourField.selectedOption = HourOption(); hourField._M3Refresh(); }
            if (minuteField != null) { minuteField.selectedOption = minute; minuteField._M3Refresh(); }
            Toggles();
        }

        void Toggles()
        {
            bool pm = hour >= 12;
            if (amToggle != null) { if (pm) amToggle._Deselect(); else amToggle._Select(); }
            if (pmToggle != null) { if (pm) pmToggle._Select(); else pmToggle._Deselect(); }
        }

        public void _HourScrolled()
        {
            if (hourField == null) return;
            int h = (hourField.selectedOption + 1) % 12 + (hour >= 12 ? 12 : 0);
            if (h == hour) return;
            hour = h;
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        public void _MinuteScrolled()
        {
            if (minuteField == null) return;
            if (minuteField.selectedOption == minute) return;
            minute = minuteField.selectedOption;
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        public void _AM()
        {
            if (hour >= 12) { hour -= 12; if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged"); }
            Toggles();
        }

        public void _PM()
        {
            if (hour < 12) { hour += 12; if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged"); }
            Toggles();
        }
    }
}
