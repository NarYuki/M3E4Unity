#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Full-screen search (SearchBar.kt SearchBar + ExpandedFullScreenSearchBar, FullScreenSearchBarLayout).
    /// The state's animatable goes to 1 with SlowSpatial (expand) or to 0 with DefaultSpatial (collapse);
    /// progress is that value clamped. The surface grows from the collapsed bar's bounds to the screen,
    /// its corner radius is SearchBarCornerRadius * (1 - progress); the input field keeps the collapsed
    /// height, its width and center follow the unclamped value; the top / bottom field padding grows to
    /// SearchBarVerticalPadding; the content (divider + results) fades with progress. The expanded
    /// layer shows while the value is above the 0.02 collapse threshold or expanding.
    /// Events: _Open, _Close, _CollapsedChanged, _ExpandedChanged, _Pick0 … _Pick7.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3FullScreenSearchBar : UdonSharpBehaviour
#else
    public class M3FullScreenSearchBar : M3BehaviourBase
#endif
    {
        public bool expanded;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Collapsed bar (screen coordinates, y down)")]
        public float collapsedX;
        public float collapsedY;
        public float collapsedWidth = 360f;
        public float collapsedHeight = 56f;
        public TMP_InputField collapsedInput;

        [Header("Expanded layer")]
        public GameObject layer;
        public RectTransform surface;
        public Image surfaceImage;
        public Sprite[] radiusFrames;          // corner radius 0 … cornerRadius in 1 dp steps
        public float cornerRadius = 28f;       // SearchBarCornerRadius = InputFieldHeight / 2
        public RectTransform field;
        public TMP_InputField expandedInput;
        public RectTransform content;
        public CanvasGroup contentGroup;
        public float screenWidth = 412f;
        public float screenHeight = 915f;
        public float verticalPadding = 8f;     // SearchBarVerticalPadding
        public TextMeshProUGUI[] suggestionTexts;
        [Tooltip("Both input fields (their caret / selection colors are baked by the theme).")]
        public TMP_InputField[] inputs;

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: caret, error caret, text selection.")]
        public int[] extraColors;

        [Header("Motion")]
        public float expandDamping = 0.8f;     // SlowSpatial
        public float expandStiffness = 200f;
        public float collapseDamping = 0.8f;   // DefaultSpatial
        public float collapseStiffness = 380f;

        float value;
        float velocity;
        float lastTime;
        bool ticking;
        bool syncing;

        void Start() { Snap(); }
        public void _M3Refresh() { Snap(); }

        void Snap()
        {
            value = expanded ? 1f : 0f;
            velocity = 0f;
            Apply();
        }

        public void _Open()
        {
            if (expanded) return;
            expanded = true;
            if (layer != null) layer.SetActive(true);
#if !UDONSHARP
            // ExpandedFullScreenSearchBarImpl: focus the input field on the first expansion
            if (expandedInput != null) expandedInput.ActivateInputField();
#endif
            Kick();
        }

        public void _Close()
        {
            if (!expanded) return;
            expanded = false;
#if !UDONSHARP
            if (expandedInput != null) expandedInput.DeactivateInputField();
#endif
            Kick();
        }

        // the input field's TextFieldState is shared by the collapsed and the expanded bar
        public void _CollapsedChanged() { Sync(collapsedInput, expandedInput); }
        public void _ExpandedChanged() { Sync(expandedInput, collapsedInput); }

        void Sync(TMP_InputField from, TMP_InputField to)
        {
            if (syncing || from == null || to == null) return;
            syncing = true;
            if (to.text != from.text) to.text = from.text;
            syncing = false;
            if (changeListener != null) changeListener.SendCustomEvent("_M3TextChanged");
        }

        void Pick(int i)
        {
            if (suggestionTexts == null || i >= suggestionTexts.Length || suggestionTexts[i] == null) return;
            if (expandedInput != null) expandedInput.text = suggestionTexts[i].text;
            _Close();
        }

        public void _Pick0() { Pick(0); } public void _Pick1() { Pick(1); } public void _Pick2() { Pick(2); } public void _Pick3() { Pick(3); }
        public void _Pick4() { Pick(4); } public void _Pick5() { Pick(5); } public void _Pick6() { Pick(6); } public void _Pick7() { Pick(7); }

        void Kick()
        {
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
            float target = expanded ? 1f : 0f;
            float z = expanded ? expandDamping : collapseDamping;
            float k = expanded ? expandStiffness : collapseStiffness;
            float x0 = value - target;
            bool busy = Mathf.Abs(x0) > 0.001f || Mathf.Abs(velocity) > 0.01f;
            if (busy)
            {
                float w = Mathf.Sqrt(k);
                if (z < 1f)
                {
                    float wd = w * Mathf.Sqrt(1f - z * z);
                    float sc = (z * w * x0 + velocity) / wd;
                    float e = Mathf.Exp(-z * w * dt);
                    float cs = Mathf.Cos(wd * dt);
                    float sn = Mathf.Sin(wd * dt);
                    float x = e * (x0 * cs + sc * sn);
                    velocity = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
                    value = target + x;
                }
                else
                {
                    float cb = velocity + w * x0;
                    float e = Mathf.Exp(-w * dt);
                    value = target + (x0 + cb * dt) * e;
                    velocity = (cb - w * (x0 + cb * dt)) * e;
                }
            }
            else { value = target; velocity = 0f; }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        static void Put(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void Apply()
        {
            float p = Mathf.Clamp01(value);
            bool shown = expanded || value > 0.02f; // currentValue == Expanded while animating
            if (layer != null) layer.SetActive(shown);
            if (!shown) return;
            float width = Mathf.Round(Mathf.LerpUnclamped(collapsedWidth, screenWidth, p));
            float height = Mathf.Round(Mathf.LerpUnclamped(collapsedHeight, screenHeight, p));
            float offsetX = Mathf.Round(Mathf.LerpUnclamped(collapsedX, 0f, p));
            float offsetY = Mathf.Round(Mathf.LerpUnclamped(collapsedY, 0f, p));
            Put(surface, offsetX, offsetY, width, height);
            if (surfaceImage != null && radiusFrames != null && radiusFrames.Length > 0)
            {
                float r = cornerRadius * (1f - p);
                int f = Mathf.Clamp(Mathf.RoundToInt(r), 0, radiusFrames.Length - 1);
                if (surfaceImage.sprite != radiusFrames[f]) surfaceImage.sprite = radiusFrames[f];
            }
            float fieldW = Mathf.Round(Mathf.LerpUnclamped(collapsedWidth, width, value));
            float centerX = Mathf.LerpUnclamped(collapsedX + collapsedWidth / 2f, offsetX + width / 2f, value);
            float topPad = Mathf.Round(verticalPadding * p);
            float bottomPad = Mathf.Round(verticalPadding * p);
            Put(field, Mathf.Round(centerX - fieldW / 2f), offsetY + topPad, fieldW, collapsedHeight);
            float contentY = offsetY + topPad + collapsedHeight + bottomPad;
            Put(content, offsetX, contentY, width, Mathf.Max(0f, height - (contentY - offsetY)));
            if (contentGroup != null) contentGroup.alpha = p;
        }
    }
}
