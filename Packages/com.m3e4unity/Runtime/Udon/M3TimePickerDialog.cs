#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Time picker dialog (TimePickerDialog.kt TimePickerDialog / VibrantTimePickerDialog with a
    /// DisplayModeToggle or ScrollDisplayModeToggle): switches its content between the clock picker
    /// (mode 0), the time input (mode 1) and the time scroll (mode 2), carrying the time over, and
    /// resizes the dialog to the content (TimePickerCustomLayout / VibrantTimePickerCustomLayout).
    /// Events: _ToggleMode; forwards _M3TimeChanged to changeListener.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TimePickerDialog : UdonSharpBehaviour
#else
    public class M3TimePickerDialog : M3BehaviourBase
#endif
    {
        public int mode;                 // 0 picker, 1 input, 2 scroll
        [Tooltip("The mode the toggle switches to from the input mode (0 picker or 2 scroll).")]
        public int otherMode;
        public int hour = 9;
        public int minute;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Content")]
        public M3TimePicker picker;
        public M3TimeInput input;
        public M3TimeScroll scrollPicker;
        public GameObject[] pages;       // per mode (null when absent)
        public float[] contentWidths;
        public float[] contentHeights;

        [Header("Chrome")]
        public RectTransform surface;
        public float horizontalPadding = 24f;
        [Tooltip("Height of everything but the content (paddings, title, actions).")]
        public float chromeHeight = 132f;
        public TextMeshProUGUI title;
        public string[] titles;          // per mode
        public TextMeshProUGUI toggleIcon;
        public string[] toggleGlyphs;    // per mode (the icon shown while in that mode)

        void Start() { Apply(); }
        public void _M3Refresh() { Apply(); }

        void Read()
        {
            if (mode == 0 && picker != null) { hour = picker.hour; minute = picker.minute; }
            else if (mode == 1 && input != null) { hour = input.hour; minute = input.minute; }
            else if (mode == 2 && scrollPicker != null) { hour = scrollPicker.hour; minute = scrollPicker.minute; }
        }

        void Push()
        {
            if (mode == 0 && picker != null) { picker.hour = hour; picker.minute = minute; picker._M3Refresh(); }
            else if (mode == 1 && input != null) { input.hour = hour; input.minute = minute; input._M3Refresh(); }
            else if (mode == 2 && scrollPicker != null) { scrollPicker.hour = hour; scrollPicker.minute = minute; scrollPicker._M3Refresh(); }
        }

        public void _ToggleMode()
        {
            Read();
            mode = mode == 1 ? otherMode : 1;
            Push();
            Apply();
        }

        public void _M3TimeChanged()
        {
            Read();
            Resize();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        void Apply()
        {
            if (pages != null)
                for (int i = 0; i < pages.Length; i++)
                    if (pages[i] != null) pages[i].SetActive(i == mode);
            if (title != null && titles != null && mode < titles.Length) title.text = titles[mode];
            if (toggleIcon != null && toggleGlyphs != null && mode < toggleGlyphs.Length) toggleIcon.text = toggleGlyphs[mode];
            Resize();
        }

        void Resize()
        {
            if (surface == null || contentWidths == null || contentHeights == null) return;
            float h = contentHeights[mode];
            if (mode == 1 && input != null) h = input.contentHeight; // the vibrant input grows with its error text
            surface.sizeDelta = new Vector2(contentWidths[mode] + 2f * horizontalPadding, chromeHeight + h);
        }
    }
}
