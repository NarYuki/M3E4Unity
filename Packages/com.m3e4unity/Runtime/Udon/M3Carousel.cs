#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Carousel (androidx carousel: Strategy.getKeylineListForScrollOffset + Modifier.carouselItem).
    /// The strategy's keyline lists are computed in the editor (M3E4Unity.Carousel.CarouselStrategy)
    /// and flattened into arrays; this behaviour interpolates them for the current scroll offset and
    /// masks / translates each item. A ScrollRect provides the drag; when it ends the carousel snaps to
    /// an item (singleAdvanceFlingBehavior: at most one item, spring StiffnessMediumLow).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Carousel : UdonSharpBehaviour
#else
    public class M3Carousel : M3BehaviourBase
#endif
    {
        public ScrollRect scrollRect;
        public RectTransform content;
        public RectTransform[] items;        // mask rects (centered on the item)
        public RectTransform[] itemContents; // full-size content inside each mask

        [Header("Strategy")]
        public float itemSize;               // strategy.itemMainAxisSize (float)
        public float pageSize;               // the content size (itemMainAxisSize.roundToInt())
        public float itemSpacing;
        public float availableSpace;
        public float maxScroll;
        public int keylineCount;
        public float[] defaultSize;
        public float[] defaultOffset;
        public float[] defaultUnadjusted;
        public int startStepCount;
        public float[] startSize;
        public float[] startOffset;
        public float[] startUnadjusted;
        public float[] startPoints;
        public int endStepCount;
        public float[] endSize;
        public float[] endOffset;
        public float[] endUnadjusted;
        public float[] endPoints;
        public float startShiftDistance;
        public float endShiftDistance;
        public float[] snapScroll;
        public bool snap = true;
        public float snapStiffness = 400f;   // Spring.StiffnessMediumLow, dampingRatio 1

        float[] kSize;
        float[] kOffset;
        float[] kUnadj;
        bool snapping;
        float snapTarget;
        float snapV;
        float lastTime;
        int dragStartItem;

        void Start()
        {
            Layout();
        }

        public void _M3Refresh() { Layout(); }

        float Scroll()
        {
            return content != null ? Mathf.Clamp(-content.anchoredPosition.x, 0f, maxScroll) : 0f;
        }

        void SetScroll(float s)
        {
            if (content != null) content.anchoredPosition = new Vector2(-s, content.anchoredPosition.y);
        }

        public void _Scrolled()
        {
            if (!snapping) Layout();
        }

        public void _BeginDrag()
        {
            snapping = false;
            dragStartItem = NearestItem(Scroll());
        }

        int NearestItem(float s)
        {
            int best = 0;
            float bestD = float.MaxValue;
            if (snapScroll == null) return 0;
            for (int i = 0; i < snapScroll.Length; i++)
            {
                float d = Mathf.Abs(snapScroll[i] - s);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public void _EndDrag()
        {
            if (!snap || snapScroll == null || snapScroll.Length == 0) return;
            float s = Scroll();
            int target = NearestItem(s);
            // PagerSnapDistance.atMost(1): never move more than one item from where the drag started
            float v = scrollRect != null ? -scrollRect.velocity.x : 0f;
            if (Mathf.Abs(v) > 400f) target = dragStartItem + (v > 0f ? 1 : -1);
            target = Mathf.Clamp(target, Mathf.Max(0, dragStartItem - 1), Mathf.Min(snapScroll.Length - 1, dragStartItem + 1));
            snapTarget = snapScroll[target];
            snapV = 0f;
            if (scrollRect != null) scrollRect.velocity = Vector2.zero;
            if (snapping) return;
            snapping = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Tick()
        {
            if (!snapping) return;
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            float w = Mathf.Sqrt(snapStiffness);
            float x0 = Scroll() - snapTarget;
            float cb = snapV + w * x0;
            float e = Mathf.Exp(-w * dt);
            float x = (x0 + cb * dt) * e;
            snapV = (cb - w * (x0 + cb * dt)) * e;
            bool busy = Mathf.Abs(x) > 0.1f || Mathf.Abs(snapV) > 1f;
            SetScroll(busy ? snapTarget + x : snapTarget);
            if (scrollRect != null) scrollRect.velocity = Vector2.zero;
            Layout();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else snapping = false;
        }

        static float LerpRange(float outMin, float outMax, float inMin, float inMax, float value)
        {
            if (value <= inMin) return outMin;
            if (value >= inMax) return outMax;
            float f = (value - inMin) / (inMax - inMin);
            return (1f - f) * outMin + f * outMax;
        }

        // Strategy.getKeylineListForScrollOffsetInternal
        void ComputeKeylines(float scrollOffset)
        {
            int K = keylineCount;
            if (kSize == null || kSize.Length != K) { kSize = new float[K]; kOffset = new float[K]; kUnadj = new float[K]; }
            float positive = Mathf.Max(0f, scrollOffset);
            float startShiftOffset = startShiftDistance;
            float endShiftOffset = Mathf.Max(0f, maxScroll - endShiftDistance);
            if (positive >= startShiftOffset && positive <= endShiftOffset)
            {
                for (int i = 0; i < K; i++) { kSize[i] = defaultSize[i]; kOffset[i] = defaultOffset[i]; kUnadj[i] = defaultUnadjusted[i]; }
                return;
            }
            float interpolation = LerpRange(1f, 0f, 0f, startShiftOffset, positive);
            bool useEnd = positive > endShiftOffset;
            bool special = false;
            if (useEnd)
            {
                interpolation = LerpRange(0f, 1f, endShiftOffset, maxScroll, positive);
                special = endShiftOffset < 0.01f && startStepCount == 2 && endStepCount == 2;
            }
            int steps = useEnd ? endStepCount : startStepCount;
            int fromStep = 0;
            int toStep = 0;
            float stepped = 0f;
            float lower = useEnd ? endPoints[0] : startPoints[0];
            for (int i = 1; i < steps; i++)
            {
                float upper = useEnd ? endPoints[i] : startPoints[i];
                if (interpolation <= upper)
                {
                    fromStep = i - 1;
                    toStep = i;
                    stepped = LerpRange(0f, 1f, lower, upper, interpolation);
                    break;
                }
                lower = upper;
            }
            for (int k = 0; k < K; k++)
            {
                float fs, fo, fu, ts, to, tu;
                if (special)
                {
                    // listOf(startKeylineSteps.last(), endKeylineSteps.last())
                    int a = (startStepCount - 1) * K + k;
                    int b = (endStepCount - 1) * K + k;
                    if (fromStep == 0) { fs = startSize[a]; fo = startOffset[a]; fu = startUnadjusted[a]; } else { fs = endSize[b]; fo = endOffset[b]; fu = endUnadjusted[b]; }
                    if (toStep == 0) { ts = startSize[a]; to = startOffset[a]; tu = startUnadjusted[a]; } else { ts = endSize[b]; to = endOffset[b]; tu = endUnadjusted[b]; }
                }
                else if (useEnd)
                {
                    int a = fromStep * K + k;
                    int b = toStep * K + k;
                    fs = endSize[a]; fo = endOffset[a]; fu = endUnadjusted[a];
                    ts = endSize[b]; to = endOffset[b]; tu = endUnadjusted[b];
                }
                else
                {
                    int a = fromStep * K + k;
                    int b = toStep * K + k;
                    fs = startSize[a]; fo = startOffset[a]; fu = startUnadjusted[a];
                    ts = startSize[b]; to = startOffset[b]; tu = startUnadjusted[b];
                }
                kSize[k] = fs + (ts - fs) * stepped;
                kOffset[k] = fo + (to - fo) * stepped;
                kUnadj[k] = fu + (tu - fu) * stepped;
            }
        }

        // Modifier.carouselItem
        public void Layout()
        {
            if (items == null || keylineCount <= 0) return;
            float scroll = Scroll();
            ComputeKeylines(scroll);
            int K = keylineCount;
            float step = itemSize + itemSpacing;
            for (int i = 0; i < items.Length; i++)
            {
                RectTransform rt = items[i];
                if (rt == null) continue;
                float unadjustedCenter = i * step + itemSize / 2f - scroll;
                // getKeylineBefore / getKeylineAfter
                int before = 0;
                for (int k = K - 1; k >= 0; k--) { if (kUnadj[k] < unadjustedCenter) { before = k; break; } }
                int after = K - 1;
                for (int k = 0; k < K; k++) { if (kUnadj[k] >= unadjustedCenter) { after = k; break; } }
                bool same = before == after;
                float progress = same ? 1f : (unadjustedCenter - kUnadj[before]) / (kUnadj[after] - kUnadj[before]);
                float size = kSize[before] + (kSize[after] - kSize[before]) * progress;
                float offset = kOffset[before] + (kOffset[after] - kOffset[before]) * progress;
                float unadj = kUnadj[before] + (kUnadj[after] - kUnadj[before]) * progress;
                float translation = offset - unadjustedCenter;
                if (same) translation += (unadjustedCenter - unadj) / size;
                rt.anchoredPosition = new Vector2(unadjustedCenter + translation, rt.anchoredPosition.y);
                rt.sizeDelta = new Vector2(size, rt.sizeDelta.y);
                if (itemContents != null && itemContents[i] != null)
                    itemContents[i].sizeDelta = new Vector2(pageSize, itemContents[i].sizeDelta.y);
            }
        }
    }
}
