#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Runtime side of an M3E4Unity theme. The editor bakes every color the UI under this
    /// object needs (roles, composited state layers, disabled colors...) into a palette per
    /// scheme (light, dark, extra seeds...). Switching scheme recolors all bound graphics
    /// and tells the interactive components to refresh. Works in VRChat (Udon) and in plain Unity.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Theme : UdonSharpBehaviour
#else
    public class M3Theme : M3BehaviourBase
#endif
    {
        [Tooltip("Display names of the baked schemes, e.g. Light, Dark.")]
        public string[] schemeNames;

        [Tooltip("Number of colors per scheme.")]
        public int paletteSize;

        [Tooltip("All schemes' palettes, scheme-major (scheme * paletteSize + index).")]
        public Color[] palettes;

        [Tooltip("Scheme shown at start.")]
        public int currentScheme;

        [Tooltip("Graphics colored straight from the palette.")]
        public Graphic[] graphics;

        [Tooltip("Palette index of each graphic.")]
        public int[] graphicColors;

#if UDONSHARP
        [Tooltip("Components that read colors from this theme; they get _M3ThemeChanged.")]
        public UdonSharpBehaviour[] listeners;
#else
        [Tooltip("Components that read colors from this theme; they get _M3ThemeChanged.")]
        public M3BehaviourBase[] listeners;
#endif

        void Start()
        {
            ApplyScheme();
        }

        /// <summary>Color at palette index for the current scheme.</summary>
        public Color Get(int index)
        {
            if (index < 0 || index >= paletteSize) return Color.magenta;
            return palettes[currentScheme * paletteSize + index];
        }

        public int SchemeCount()
        {
            return paletteSize > 0 ? palettes.Length / paletteSize : 0;
        }

        public void SetScheme(int scheme)
        {
            int count = SchemeCount();
            if (count == 0) return;
            currentScheme = Mathf.Clamp(scheme, 0, count - 1);
            ApplyScheme();
        }

        /// <summary>Cycles to the next scheme (wire a button's OnClick to this event).</summary>
        public void NextScheme()
        {
            int count = SchemeCount();
            if (count == 0) return;
            currentScheme = (currentScheme + 1) % count;
            ApplyScheme();
        }

        public void ApplyScheme()
        {
            if (graphics != null)
            {
                int offset = currentScheme * paletteSize;
                for (int i = 0; i < graphics.Length; i++)
                {
                    Graphic g = graphics[i];
                    if (g == null) continue;
                    g.color = palettes[offset + graphicColors[i]];
                }
            }
            if (listeners != null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] != null) listeners[i].SendCustomEvent("_M3ThemeChanged");
                }
            }
        }
    }
}
