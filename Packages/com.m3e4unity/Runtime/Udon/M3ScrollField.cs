#if UDONSHARP
using UdonSharp;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Scroll field (ScrollField.kt): a wrapping vertical pager of fixed ScrollFieldHeight / 3 pages
    /// snapped to the center. A drag release snaps to the nearest page, or one page on in the fling
    /// direction above the minimum fling velocity (PagerDefaults.flingBehavior with
    /// PagerSnapDistance.atMost(1)), with spring(StiffnessMediumLow). Tapping an item scrolls to it.
    /// Items (ScrollFieldDefaults.Item) blend displayMedium → displayLargeEmphasized and contentColor →
    /// selectedContentColor with FastSpatial. The pages are a pool of slots; "wrapping" is a long
    /// strip of repeated pages started in the middle. Events: _Scroll, _BeginDrag, _EndDrag, _I0 … _I5.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3ScrollField : UdonSharpBehaviour
#else
    public class M3ScrollField : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public int itemCount = 60;
        [Tooltip("The selected option, 0 … itemCount - 1 (state.selectedOption).")]
        public int selectedOption;
        [Tooltip("Added to the index for display (hours of a 12-hour clock show index + 1).")]
        public int labelOffset;
        public int pageCount = 2400;          // the strip length (a multiple of itemCount)
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif
        public string changeEvent = "_M3ScrollFieldChanged";

        [Header("Parts")]
        public ScrollRect scroll;
        public RectTransform content;
        public float fieldHeight = 200f;
        public float pageHeight = 200f / 3f;
        public RectTransform[] slots;
        public TextMeshProUGUI[] baseTexts;      // displayMedium
        public TextMeshProUGUI[] emphasisTexts;  // displayLargeEmphasized weight, faded in with the selection
        public RectTransform[] baseRects;        // the texts' RectTransforms (TMP_Text.rectTransform is not exposed to Udon)
        public RectTransform[] emphasisRects;
        public float largeScale = 57f / 45f;     // displayLargeEmphasized / displayMedium font size

        [Header("Motion")]
        public float fastSpatialDamping = 0.6f;
        public float fastSpatialStiffness = 800f;
        public float snapStiffness = 400f;       // Spring.StiffnessMediumLow
        public float minFlingVelocity = 400f;    // MinFlingVelocityDp

        [Header("Colors")]
        public Graphic[] colored;
        public int[] colorIndices;
        [Tooltip("Palette indices: content, selected content.")]
        public int[] extraColors;

        float y;            // content offset (scroll position)
        float targetY;
        float v;
        bool animating;
        bool dragging;
        int dragStartPage;
        int currentPage = -1;
        float[] fractions;
        float[] fractionV;
        int[] slotPages;
        float lastTime;
        bool ticking;

        void Start() { Snap(); }
        public void _M3Refresh() { Snap(); }
        public void _M3ThemeChanged() { currentPage = -1; Layout(); }

        float PageY(int page) { return page * pageHeight + pageHeight / 2f - fieldHeight / 2f; }
        float PagePosition() { return (y + fieldHeight / 2f - pageHeight / 2f) / pageHeight; }

        // the page nearest the middle of the strip that shows option
        int PageFor(int option)
        {
            int mid = pageCount / 2;
            return mid - mid % itemCount + option;
        }

        void EnsureArrays()
        {
            int n = slots != null ? slots.Length : 0;
            if (fractions == null || fractions.Length != n)
            {
                fractions = new float[n];
                fractionV = new float[n];
                slotPages = new int[n];
                for (int i = 0; i < n; i++) slotPages[i] = -1;
            }
        }

        void Snap()
        {
            EnsureArrays();
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, pageCount * pageHeight);
            selectedOption = Mathf.Clamp(selectedOption, 0, itemCount - 1);
            y = PageY(PageFor(selectedOption));
            targetY = y; v = 0f; animating = false;
            currentPage = -1;
            for (int i = 0; i < slotPages.Length; i++) slotPages[i] = -1;
            Apply();
        }

        /// <summary>Scrolls to selectedOption (state.animateScrollToOption: the nearest matching page).</summary>
        public void _ScrollToSelected()
        {
            EnsureArrays();
            int cur = Mathf.RoundToInt(PagePosition());
            int curOption = ((cur % itemCount) + itemCount) % itemCount;
            int diff = selectedOption - curOption;
            // calculateTargetPage: the shortest way round
            if (diff > itemCount / 2) diff -= itemCount;
            else if (diff < -itemCount / 2) diff += itemCount;
            AnimateTo(cur + diff);
        }

        void AnimateTo(int page)
        {
            targetY = PageY(page);
            animating = true;
            if (scroll != null) scroll.velocity = Vector2.zero;
            Kick();
        }

        public void _Scroll()
        {
            if (content == null) return;
            if (!animating) y = content.anchoredPosition.y;
            Layout();
        }

        public void _BeginDrag()
        {
            animating = false;
            dragging = true;
            dragStartPage = Mathf.RoundToInt(PagePosition());
        }

        public void _EndDrag()
        {
            dragging = false;
            if (content != null) y = content.anchoredPosition.y;
            float velocity = scroll != null ? scroll.velocity.y : 0f; // > 0: towards later pages
            float pos = PagePosition();
            int target;
            if (Mathf.Abs(velocity) < minFlingVelocity) target = Mathf.RoundToInt(pos);
            else target = velocity > 0f ? Mathf.FloorToInt(pos) + 1 : Mathf.CeilToInt(pos) - 1;
            target = Mathf.Clamp(target, dragStartPage - 1, dragStartPage + 1);
            v = velocity;
            AnimateTo(target);
        }

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
            bool busy = false;
            if (animating && !dragging)
            {
                // snap spring: damping 1 (no bounce), StiffnessMediumLow, visibility threshold 1 px
                float x0 = y - targetY;
                if (Mathf.Abs(x0) > 0.5f || Mathf.Abs(v) > 1f)
                {
                    float w = Mathf.Sqrt(snapStiffness);
                    float cb = v + w * x0;
                    float e = Mathf.Exp(-w * dt);
                    float x = (x0 + cb * dt) * e;
                    v = (cb - w * (x0 + cb * dt)) * e;
                    y = targetY + x;
                    busy = true;
                }
                else { y = targetY; v = 0f; animating = false; }
                if (content != null) content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
                if (scroll != null) scroll.velocity = Vector2.zero;
            }
            if (StepFractions(dt)) busy = true;
            Layout();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        bool StepFractions(float dt)
        {
            bool busy = false;
            if (fractions == null) return false;
            float w = Mathf.Sqrt(fastSpatialStiffness);
            float z = fastSpatialDamping;
            float wd = w * Mathf.Sqrt(Mathf.Max(1e-4f, 1f - z * z));
            for (int i = 0; i < fractions.Length; i++)
            {
                float t = slotPages[i] == currentPage ? 1f : 0f;
                float x0 = fractions[i] - t;
                if (Mathf.Abs(x0) < 0.001f && Mathf.Abs(fractionV[i]) < 0.01f) { fractions[i] = t; fractionV[i] = 0f; continue; }
                float sc = (z * w * x0 + fractionV[i]) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                float x = e * (x0 * cs + sc * sn);
                fractionV[i] = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
                fractions[i] = t + x;
                busy = true;
            }
            return busy;
        }

        void Apply()
        {
            if (content != null) content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            Layout();
            // settled: the selected item shows fully selected
            if (fractions != null)
                for (int i = 0; i < fractions.Length; i++) { fractions[i] = slotPages[i] == currentPage ? 1f : 0f; fractionV[i] = 0f; }
            Style();
        }

        void Layout()
        {
            if (slots == null) return;
            EnsureArrays();
            int page = Mathf.RoundToInt(PagePosition());
            if (page != currentPage)
            {
                currentPage = page;
                int option = ((page % itemCount) + itemCount) % itemCount;
                if (option != selectedOption)
                {
                    selectedOption = option;
                    if (changeListener != null) changeListener.SendCustomEvent(changeEvent);
                }
                Kick();
            }
            int first = Mathf.FloorToInt(y / pageHeight);
            for (int i = 0; i < slots.Length; i++)
            {
                int p = first + i;
                RectTransform rt = slots[i];
                if (rt == null) continue;
                if (slotPages[i] != p)
                {
                    // a recycled slot starts at its target fraction
                    slotPages[i] = p;
                    fractions[i] = p == currentPage ? 1f : 0f;
                    fractionV[i] = 0f;
                    int index = ((p % itemCount) + itemCount) % itemCount + labelOffset;
                    string label = index < 10 ? "0" + index : index.ToString();
                    if (baseTexts[i] != null) baseTexts[i].text = label;
                    if (emphasisTexts[i] != null) emphasisTexts[i].text = label;
                }
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -p * pageHeight);
            }
            Style();
        }

        void Style()
        {
            if (slots == null || fractions == null) return;
            bool hasColors = theme != null && extraColors != null && extraColors.Length >= 2;
            Color c0 = hasColors && extraColors[0] >= 0 ? theme.Get(extraColors[0]) : Color.gray;
            Color c1 = hasColors && extraColors[1] >= 0 ? theme.Get(extraColors[1]) : Color.black;
            for (int i = 0; i < slots.Length; i++)
            {
                float f = Mathf.Clamp01(fractions[i]);
                float s = Mathf.LerpUnclamped(1f, largeScale, f);
                Color c = Color.Lerp(c0, c1, f);
                if (baseTexts[i] != null)
                {
                    if (baseRects[i] != null) baseRects[i].localScale = new Vector3(s, s, 1f);
                    Color cb = c; cb.a = c.a * (1f - f);
                    baseTexts[i].color = cb;
                }
                if (emphasisTexts[i] != null)
                {
                    if (emphasisRects[i] != null) emphasisRects[i].localScale = new Vector3(s, s, 1f);
                    Color ce = c; ce.a = c.a * f;
                    emphasisTexts[i].color = ce;
                }
            }
        }

        void Tap(int slot)
        {
            if (slotPages == null || slot >= slotPages.Length) return;
            AnimateTo(slotPages[slot]);
        }

        public void _I0() { Tap(0); } public void _I1() { Tap(1); } public void _I2() { Tap(2); }
        public void _I3() { Tap(3); } public void _I4() { Tap(4); } public void _I5() { Tap(5); }
    }
}
