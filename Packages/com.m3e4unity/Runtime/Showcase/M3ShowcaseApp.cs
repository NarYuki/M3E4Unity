#if !UDONSHARP
using System.Collections.Generic;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Showcase
{
    /// <summary>
    /// The component showcase app (plain Unity): a page per component family, a navigation list, the
    /// theme switcher (seed color × scheme variant × light / dark × contrast, all baked into the canvas
    /// theme) and an event log of the components' change events. Built by Tools > M3E4Unity > Showcase.
    /// </summary>
    public sealed class M3ShowcaseApp : M3BehaviourBase
    {
        public M3Theme theme;

        [Header("Pages")]
        public GameObject[] pages;
        public string[] pageTitles;
        public Vector2[] pageSizes;
        public ScrollRect contentScroll;
        public RectTransform pageHolder;
        public TextMeshProUGUI pageTitle;
        public RectTransform[] navItems;
        public RectTransform navIndicator;
        public TextMeshProUGUI[] navLabels;
        public int currentPage;

        [Header("Theme (scheme index = ((seed * variants + variant) * 2 + dark) * contrasts + contrast)")]
        public int seedCount = 1;
        public int variantCount = 1;
        public int contrastCount = 1;
        public string[] variantNames;
        public int seed;
        public int variant;
        public bool dark;
        public int contrast;
        public RectTransform[] seedRings;
        public TextMeshProUGUI variantLabel;
        public TextMeshProUGUI darkIcon;
        public string lightGlyph;
        public string darkGlyph;
        public M3Interactive[] contrastToggles;
        [Tooltip("The theme switcher's scroll view and content: right-aligned when it fits, scrollable from the start when it does not.")]
        public RectTransform themeView;
        public RectTransform themeContent;

        [Header("Event log")]
        public TextMeshProUGUI logText;
        public int logLines = 8;

        readonly List<string> log = new List<string>();
        GameObject lastSelected;

        void Start()
        {
            ShowPage(currentPage);
            ApplyScheme();
            Log("Ready — click, drag and type anywhere to try the components");
        }

        // ---- pages ---------------------------------------------------------------------------------

        public void ShowPage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;
            currentPage = index;
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == index);
            if (pageHolder != null && pageSizes != null && index < pageSizes.Length) pageHolder.sizeDelta = pageSizes[index];
            if (contentScroll != null) { contentScroll.normalizedPosition = new Vector2(0f, 1f); contentScroll.velocity = Vector2.zero; }
            if (pageTitle != null && pageTitles != null && index < pageTitles.Length) pageTitle.text = pageTitles[index];
            if (navIndicator != null && navItems != null && index < navItems.Length && navItems[index] != null)
            {
                navIndicator.SetParent(navItems[index], false);
                navIndicator.SetAsFirstSibling();
                navIndicator.anchorMin = Vector2.zero;
                navIndicator.anchorMax = Vector2.one;
                navIndicator.offsetMin = new Vector2(12f, 0f);
                navIndicator.offsetMax = new Vector2(-12f, 0f);
            }
            StyleNav();
        }

        void StyleNav()
        {
            if (navLabels == null || theme == null) return;
            // drawer item colors: selected = OnSecondaryContainer, unselected = OnSurfaceVariant (palette entries 0 / 1 of the app)
            for (int i = 0; i < navLabels.Length; i++)
                if (navLabels[i] != null) navLabels[i].fontStyle = i == currentPage ? FontStyles.Bold : FontStyles.Normal;
        }

        // ---- theme ---------------------------------------------------------------------------------

        public void SetSeed(int index) { seed = Mathf.Clamp(index, 0, seedCount - 1); ApplyScheme(); }
        public void NextVariant() { variant = (variant + 1) % Mathf.Max(1, variantCount); ApplyScheme(); }
        public void ToggleDark() { dark = !dark; ApplyScheme(); }
        public void SetContrast(int index) { contrast = Mathf.Clamp(index, 0, contrastCount - 1); ApplyScheme(); }

        void ApplyScheme()
        {
            if (theme == null) return;
            int index = ((seed * variantCount + variant) * 2 + (dark ? 1 : 0)) * contrastCount + contrast;
            if (index < theme.SchemeCount()) theme.SetScheme(index);
            if (seedRings != null)
                for (int i = 0; i < seedRings.Length; i++) if (seedRings[i] != null) seedRings[i].gameObject.SetActive(i == seed);
            if (variantLabel != null && variantNames != null && variant < variantNames.Length) variantLabel.text = variantNames[variant];
            if (darkIcon != null) darkIcon.text = dark ? lightGlyph : darkGlyph;
            if (contrastToggles != null)
                for (int i = 0; i < contrastToggles.Length; i++)
                    if (contrastToggles[i] != null) { if (i == contrast) contrastToggles[i]._Select(); else contrastToggles[i]._Deselect(); }
            Log("Theme: " + (variantNames != null && variant < variantNames.Length ? variantNames[variant] : "") + (dark ? ", dark" : ", light") +
                (contrast == 0 ? "" : contrast == 1 ? ", medium contrast" : ", high contrast"));
        }

        // ---- event log -----------------------------------------------------------------------------

        void LateUpdate()
        {
            if (themeView == null || themeContent == null) return;
            bool overflow = themeContent.rect.width > themeView.rect.width;
            float side = overflow ? 0f : 1f;
            if (themeContent.pivot.x != side)
            {
                themeContent.anchorMin = themeContent.anchorMax = new Vector2(side, 0.5f);
                themeContent.pivot = new Vector2(side, 0.5f);
                themeContent.anchoredPosition = Vector2.zero;
            }
        }

        void Update()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected != null && selected != lastSelected && selected.GetComponentInParent<M3ShowcaseApp>() == null)
                Log("Focus: " + Describe(selected));
            lastSelected = selected;
        }

        static string Describe(GameObject go)
        {
            if (go == null) return "";
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            string text = label != null && !string.IsNullOrEmpty(label.text) && label.text.Length < 40 && label.font != null && !label.font.name.Contains("Symbols") ? " \"" + label.text + "\"" : "";
            var parent = go.transform.parent;
            return go.name + text + (parent != null ? "  ‹ " + parent.name : "");
        }

        void Log(string line)
        {
            log.Add(System.DateTime.Now.ToString("HH:mm:ss") + "  " + line);
            while (log.Count > logLines) log.RemoveAt(0);
            if (logText != null) logText.text = string.Join("\n", log);
        }

        void Event(string what)
        {
            if (Time.timeSinceLevelLoad < 1f) return; // components settling at start
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            Log(what + (go != null ? " — " + Describe(go) : ""));
        }

        // the components' change events (changeListener / selectionListener)
        public void _M3SelectionChanged() { Event("Selection changed"); }
        public void _M3SliderChanged() { Event("Slider changed"); }
        public void _M3RangeChanged() { Event("Range changed"); }
        public void _M3TextChanged() { Event("Text changed"); }
        public void _M3NavChanged() { Event("Navigation changed"); }
        public void _M3TabChanged() { Event("Tab changed"); }
        public void _M3DateChanged() { Event("Date changed"); }
        public void _M3TimeChanged() { Event("Time changed"); }
        public void _M3SearchPicked() { Event("Search suggestion picked"); }
    }
}
#endif
