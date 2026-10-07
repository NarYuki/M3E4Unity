#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Docked search bar (SearchBar.kt DockedSearchBar): focusing the input expands the results below
    /// a divider (AnimationEnterFloatSpec: 600 ms EmphasizedDecelerate after 100 ms; exit: 350 ms
    /// CubicBezier(0, 1, 0, 1) after 100 ms). Suggestions call _Pick0 … _Pick7, which fill the input
    /// with their text and collapse.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3SearchBar : UdonSharpBehaviour
#else
    public class M3SearchBar : M3BehaviourBase
#endif
    {
        public TMP_InputField input;
        public RectTransform surface;
        public CanvasGroup results;
        public float collapsedHeight = 56f;
        public float expandedHeight = 296f;
        public bool expanded;
        public TextMeshProUGUI[] suggestionTexts;
        [Header("Colors (the theme baker sets the input caret / selection from extraColors)")]
        public M3Theme theme;
        public UnityEngine.UI.Graphic[] colored;
        public int[] colorIndices;
        public int[] extraColors;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        float t;
        float from;
        float to;
        float startTime;
        bool ticking;

        void Start()
        {
            t = expanded ? 1f : 0f;
            to = t;
            Apply();
        }

        public void _M3Refresh() { t = expanded ? 1f : 0f; to = t; Apply(); }
        public void _Expand() { Go(true); }
        public void _Collapse() { Go(false); }

        void Go(bool open)
        {
            if (expanded == open && ticking) return;
            expanded = open;
            from = t;
            to = open ? 1f : 0f;
            startTime = Time.time;
            if (ticking) return;
            ticking = true;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float lo = 0f;
            float hi = 1f;
            float s = x;
            for (int i = 0; i < 24; i++)
            {
                s = (lo + hi) * 0.5f;
                float u = 1f - s;
                float bx = 3f * u * u * s * x1 + 3f * u * s * s * x2 + s * s * s;
                if (bx < x) lo = s; else hi = s;
            }
            float v = 1f - s;
            return 3f * v * v * s * y1 + 3f * v * s * s * y2 + s * s * s;
        }

        public void _Tick()
        {
            float delay = 0.1f;                      // AnimationDelayMillis = DurationShort2
            float duration = expanded ? 0.6f : 0.35f; // DurationLong4 / DurationMedium3
            float p = Mathf.Clamp01((Time.time - startTime - delay) / duration);
            float e = expanded ? Bezier(0.05f, 0.7f, 0.1f, 1f, p) : Bezier(0f, 1f, 0f, 1f, p);
            t = Mathf.Lerp(from, to, e);
            Apply();
            if (p < 1f) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            if (surface != null) surface.sizeDelta = new Vector2(surface.sizeDelta.x, Mathf.Lerp(collapsedHeight, expandedHeight, t));
            if (results != null)
            {
                results.alpha = t;
                results.blocksRaycasts = expanded;
                results.gameObject.SetActive(t > 0f || expanded);
            }
        }

        void Pick(int i)
        {
            if (input != null && suggestionTexts != null && i < suggestionTexts.Length && suggestionTexts[i] != null)
                input.text = suggestionTexts[i].text;
            if (changeListener != null) changeListener.SendCustomEvent("_M3SearchPicked");
            _Collapse();
        }

        public void _Pick0() { Pick(0); }
        public void _Pick1() { Pick(1); }
        public void _Pick2() { Pick(2); }
        public void _Pick3() { Pick(3); }
        public void _Pick4() { Pick(4); }
        public void _Pick5() { Pick(5); }
        public void _Pick6() { Pick(6); }
        public void _Pick7() { Pick(7); }
    }
}
