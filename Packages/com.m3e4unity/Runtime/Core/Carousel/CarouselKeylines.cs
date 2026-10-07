// Port of androidx.compose.material3.carousel (Keylines.kt, KeylineList.kt, Arrangement.kt, Strategy.kt).
// Copyright 2024 The Android Open Source Project, Apache License 2.0. Sizes are in dp.
using System;
using System.Collections.Generic;
using System.Linq;

namespace M3E4Unity.Carousel
{
    /// <summary>CarouselAlignment</summary>
    public enum CarouselAlignment { Start = -1, Center = 0, End = 1 }

    /// <summary>Keyline (KeylineList.kt).</summary>
    public readonly struct Keyline : IEquatable<Keyline>
    {
        public readonly float Size, Offset, UnadjustedOffset, Cutoff;
        public readonly bool IsFocal, IsAnchor, IsPivot;

        public Keyline(float size, float offset, float unadjustedOffset, bool isFocal, bool isAnchor, bool isPivot, float cutoff)
        {
            Size = size; Offset = offset; UnadjustedOffset = unadjustedOffset;
            IsFocal = isFocal; IsAnchor = isAnchor; IsPivot = isPivot; Cutoff = cutoff;
        }

        public Keyline WithUnadjustedOffset(float u) => new Keyline(Size, Offset, u, IsFocal, IsAnchor, IsPivot, Cutoff);

        public bool Equals(Keyline o) => Size == o.Size && Offset == o.Offset && UnadjustedOffset == o.UnadjustedOffset &&
            IsFocal == o.IsFocal && IsAnchor == o.IsAnchor && IsPivot == o.IsPivot && Cutoff == o.Cutoff;
        public override bool Equals(object obj) => obj is Keyline k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(Size, Offset, UnadjustedOffset, IsFocal, IsAnchor, IsPivot, Cutoff);

        static float Lerp(float a, float b, float t) => (1 - t) * a + t * b;

        /// <summary>lerp(start: Keyline, end: Keyline, fraction)</summary>
        public static Keyline Lerp(Keyline start, Keyline end, float fraction) => new Keyline(
            Lerp(start.Size, end.Size, fraction),
            Lerp(start.Offset, end.Offset, fraction),
            Lerp(start.UnadjustedOffset, end.UnadjustedOffset, fraction),
            fraction < .5f ? start.IsFocal : end.IsFocal,
            fraction < .5f ? start.IsAnchor : end.IsAnchor,
            fraction < .5f ? start.IsPivot : end.IsPivot,
            Lerp(start.Cutoff, end.Cutoff, fraction));
    }

    /// <summary>KeylineList</summary>
    public sealed class KeylineList
    {
        public readonly IReadOnlyList<Keyline> Keylines;
        public readonly float MinSize, MaxSize;
        public readonly int PivotIndex, FirstNonAnchorIndex, LastNonAnchorIndex, FirstFocalIndex, LastFocalIndex;

        public static readonly KeylineList Empty = new KeylineList(new List<Keyline>());

        public KeylineList(List<Keyline> keylines)
        {
            Keylines = keylines;
            float min = float.MaxValue, max = 0f;
            foreach (var k in keylines) { if (k.Size < min) min = k.Size; if (k.Size > max) max = k.Size; }
            MinSize = min; MaxSize = max;
            PivotIndex = keylines.FindIndex(k => k.IsPivot);
            FirstNonAnchorIndex = keylines.FindIndex(k => !k.IsAnchor);
            LastNonAnchorIndex = keylines.FindLastIndex(k => !k.IsAnchor);
            FirstFocalIndex = keylines.FindIndex(k => k.IsFocal);
            LastFocalIndex = keylines.FindLastIndex(k => k.IsFocal);
        }

        public int Count => Keylines.Count;
        public Keyline this[int i] => Keylines[i];
        public int LastIndex => Keylines.Count - 1;
        public bool IsEmpty => Keylines.Count == 0;
        public Keyline First => Keylines[0];
        public Keyline Last => Keylines[Keylines.Count - 1];
        public Keyline Pivot => Keylines[PivotIndex];
        public Keyline FirstNonAnchor => Keylines[FirstNonAnchorIndex];
        public Keyline LastNonAnchor => Keylines[LastNonAnchorIndex];
        public Keyline FirstFocal => FirstFocalIndex >= 0 ? Keylines[FirstFocalIndex] : throw new InvalidOperationException("All KeylineLists must have at least one focal keyline");
        public Keyline LastFocal => LastFocalIndex >= 0 ? Keylines[LastFocalIndex] : throw new InvalidOperationException("All KeylineLists must have at least one focal keyline");

        public bool IsFirstFocalItemAtStartOfContainer()
        {
            float firstFocalLeft = FirstFocal.Offset - (FirstFocal.Size / 2);
            return firstFocalLeft >= 0 && FirstFocal.Equals(FirstNonAnchor);
        }

        public bool IsLastFocalItemAtEndOfContainer(float carouselMainAxisSize)
        {
            float lastFocalRight = LastFocal.Offset + (LastFocal.Size / 2);
            return lastFocalRight <= carouselMainAxisSize && LastFocal.Equals(LastNonAnchor);
        }

        public int FirstIndexAfterFocalRangeWithSize(float size)
        {
            for (int i = LastFocalIndex; i <= LastIndex; i++) if (Keylines[i].Size == size) return i;
            return LastIndex;
        }

        public int LastIndexBeforeFocalRangeWithSize(float size)
        {
            for (int i = FirstFocalIndex - 1; i >= 0; i--) if (Keylines[i].Size == size) return i;
            return 0;
        }

        public Keyline GetKeylineBefore(float unadjustedOffset)
        {
            for (int i = LastIndex; i >= 0; i--) if (Keylines[i].UnadjustedOffset < unadjustedOffset) return Keylines[i];
            return First;
        }

        public Keyline GetKeylineAfter(float unadjustedOffset)
        {
            foreach (var k in Keylines) if (k.UnadjustedOffset >= unadjustedOffset) return k;
            return Last;
        }

        /// <summary>lerp(from: KeylineList, to: KeylineList, fraction)</summary>
        public static KeylineList Lerp(KeylineList from, KeylineList to, float fraction)
        {
            var list = new List<Keyline>(from.Count);
            for (int i = 0; i < from.Count; i++) list.Add(Keyline.Lerp(from[i], to[i], fraction));
            return new KeylineList(list);
        }

        // ---- keylineListOf / KeylineListScopeImpl ----

        public sealed class Builder
        {
            readonly List<(float size, bool isAnchor)> tmp = new List<(float, bool)>();
            int firstFocalIndex = -1;
            float focalItemSize;

            public Builder Add(float size, bool isAnchor = false)
            {
                tmp.Add((size, isAnchor));
                if (!isAnchor && size > focalItemSize) { firstFocalIndex = tmp.Count - 1; focalItemSize = size; }
                return this;
            }

            int FindLastFocalIndex()
            {
                if (firstFocalIndex < 0) return -1;
                int last = firstFocalIndex;
                while (last < tmp.Count - 1 && tmp[last + 1].size == focalItemSize) last++;
                return last;
            }

            public KeylineList CreateWithPivot(float carouselMainAxisSize, float itemSpacing, int pivotIndex, float pivotOffset) =>
                new KeylineList(CreateKeylinesWithPivot(pivotIndex, pivotOffset, firstFocalIndex, FindLastFocalIndex(), focalItemSize, carouselMainAxisSize, itemSpacing));

            public KeylineList CreateWithAlignment(float carouselMainAxisSize, float itemSpacing, CarouselAlignment alignment)
            {
                int lastFocalIndex = FindLastFocalIndex();
                int focalItemCount = lastFocalIndex - firstFocalIndex;
                int pivotIndex = firstFocalIndex;
                float pivotOffset;
                switch (alignment)
                {
                    case CarouselAlignment.Center:
                        float itemSpacingSplit = itemSpacing == 0f || Mod(focalItemCount, 2) == 0 ? 0f : itemSpacing / 2f;
                        float itemSpaceCounts = (focalItemCount / 2) * itemSpacing;
                        pivotOffset = (carouselMainAxisSize / 2) - ((focalItemSize / 2) * focalItemCount) - itemSpacingSplit - itemSpaceCounts;
                        break;
                    case CarouselAlignment.End:
                        pivotOffset = carouselMainAxisSize - (focalItemSize / 2);
                        break;
                    default:
                        pivotOffset = focalItemSize / 2;
                        break;
                }
                return new KeylineList(CreateKeylinesWithPivot(pivotIndex, pivotOffset, firstFocalIndex, lastFocalIndex, focalItemSize, carouselMainAxisSize, itemSpacing));
            }

            static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

            List<Keyline> CreateKeylinesWithPivot(int pivotIndex, float pivotOffset, int firstFocal, int lastFocal, float itemMainAxisSize,
                float carouselMainAxisSize, float itemSpacing)
            {
                var keylines = new List<Keyline>();
                if (tmp.Count == 0 || pivotIndex < 0 || pivotIndex >= tmp.Count) return keylines;
                var pivot = tmp[pivotIndex];
                float pivotCutoff = IsCutoffLeft(pivot.size, pivotOffset) ? pivotOffset - (pivot.size / 2)
                    : IsCutoffRight(pivot.size, pivotOffset, carouselMainAxisSize) ? (pivotOffset + (pivot.size / 2)) - carouselMainAxisSize : 0f;
                keylines.Add(new Keyline(pivot.size, pivotOffset, pivotOffset, pivotIndex >= firstFocal && pivotIndex <= lastFocal, pivot.isAnchor, true, pivotCutoff));

                float offset = pivotOffset - (itemMainAxisSize / 2) - itemSpacing;
                float unadjusted = pivotOffset - (itemMainAxisSize / 2) - itemSpacing;
                for (int i = pivotIndex - 1; i >= 0; i--)
                {
                    var t = tmp[i];
                    float tOffset = offset - (t.size / 2);
                    float tUnadjusted = unadjusted - (itemMainAxisSize / 2);
                    float cutoff = IsCutoffLeft(t.size, tOffset) ? Math.Abs(tOffset - (t.size / 2)) : 0f;
                    keylines.Insert(0, new Keyline(t.size, tOffset, tUnadjusted, i >= firstFocal && i <= lastFocal, t.isAnchor, false, cutoff));
                    offset -= t.size + itemSpacing;
                    unadjusted -= itemMainAxisSize + itemSpacing;
                }

                offset = pivotOffset + (itemMainAxisSize / 2) + itemSpacing;
                unadjusted = pivotOffset + (itemMainAxisSize / 2) + itemSpacing;
                for (int i = pivotIndex + 1; i < tmp.Count; i++)
                {
                    var t = tmp[i];
                    float tOffset = offset + (t.size / 2);
                    float tUnadjusted = unadjusted + (itemMainAxisSize / 2);
                    float cutoff = IsCutoffRight(t.size, tOffset, carouselMainAxisSize) ? (tOffset + (t.size / 2)) - carouselMainAxisSize : 0f;
                    keylines.Add(new Keyline(t.size, tOffset, tUnadjusted, i >= firstFocal && i <= lastFocal, t.isAnchor, false, cutoff));
                    offset += t.size + itemSpacing;
                    unadjusted += itemMainAxisSize + itemSpacing;
                }
                return keylines;
            }

            static bool IsCutoffLeft(float size, float offset) => offset - (size / 2) < 0f && offset + (size / 2) > 0f;
            static bool IsCutoffRight(float size, float offset, float main) => offset - (size / 2) < main && offset + (size / 2) > main;
        }
    }

    /// <summary>Arrangement</summary>
    public sealed class Arrangement
    {
        readonly int priority;
        public readonly float SmallSize, MediumSize, LargeSize;
        public readonly int SmallCount, MediumCount, LargeCount;
        const float MediumItemFlexPercentage = .1f;

        public Arrangement(int priority, float smallSize, int smallCount, float mediumSize, int mediumCount, float largeSize, int largeCount)
        {
            this.priority = priority; SmallSize = smallSize; SmallCount = smallCount; MediumSize = mediumSize;
            MediumCount = mediumCount; LargeSize = largeSize; LargeCount = largeCount;
        }

        bool IsValid()
        {
            if (LargeCount > 0 && SmallCount > 0 && MediumCount > 0) return LargeSize > MediumSize && MediumSize > SmallSize;
            if (LargeCount > 0 && SmallCount > 0) return LargeSize > SmallSize;
            return true;
        }

        float Cost(float targetLargeSize) => !IsValid() ? float.MaxValue : Math.Abs(targetLargeSize - LargeSize) * priority;

        public int ItemCount() => LargeCount + MediumCount + SmallCount;

        public static Arrangement FindLowestCostArrangement(float availableSpace, float itemSpacing, float targetSmallSize, float minSmallSize,
            float maxSmallSize, int[] smallCounts, float targetMediumSize, int[] mediumCounts, float targetLargeSize, int[] largeCounts)
        {
            Arrangement lowest = null;
            int priority = 1;
            foreach (int largeCount in largeCounts)
                foreach (int mediumCount in mediumCounts)
                    foreach (int smallCount in smallCounts)
                    {
                        var a = Fit(priority, availableSpace, itemSpacing, smallCount, targetSmallSize, minSmallSize, maxSmallSize,
                            mediumCount, targetMediumSize, largeCount, targetLargeSize);
                        if (lowest == null || a.Cost(targetLargeSize) < lowest.Cost(targetLargeSize))
                        {
                            lowest = a;
                            if (lowest.Cost(targetLargeSize) == 0f) return lowest;
                        }
                        priority++;
                    }
            return lowest;
        }

        static Arrangement Fit(int priority, float availableSpace, float itemSpacing, int smallCount, float smallSize, float minSmallSize,
            float maxSmallSize, int mediumCount, float mediumSize, int largeCount, float largeSize)
        {
            int total = largeCount + mediumCount + smallCount;
            float space = availableSpace - ((total - 1) * itemSpacing);
            float s = Math.Min(Math.Max(smallSize, minSmallSize), maxSmallSize);
            float m = mediumSize;
            float l = largeSize;
            float taken = l * largeCount + m * mediumCount + s * smallCount;
            float delta = space - taken;
            if (smallCount > 0 && delta > 0) s += Math.Min(delta / smallCount, maxSmallSize - s);
            else if (smallCount > 0 && delta < 0) s += Math.Max(delta / smallCount, minSmallSize - s);
            s = smallCount > 0 ? s : 0f;
            l = CalculateLargeSize(space, smallCount, s, mediumCount, largeCount);
            m = (l + s) / 2f;
            if (mediumCount > 0 && l != largeSize)
            {
                float targetAdjustment = (largeSize - l) * largeCount;
                float availableMediumFlex = m * MediumItemFlexPercentage * mediumCount;
                float distribute = Math.Min(Math.Abs(targetAdjustment), availableMediumFlex);
                if (targetAdjustment > 0f) { m -= distribute / mediumCount; l += distribute / largeCount; }
                else { m += distribute / mediumCount; l -= distribute / largeCount; }
            }
            return new Arrangement(priority, s, smallCount, m, mediumCount, l, largeCount);
        }

        static float CalculateLargeSize(float availableSpace, int smallCount, float smallSize, int mediumCount, int largeCount) =>
            (availableSpace - (smallCount + mediumCount / 2f) * smallSize) / (largeCount + mediumCount / 2f);
    }

    /// <summary>Keylines.kt + CarouselDefaults</summary>
    public static class CarouselKeylines
    {
        public const float MinSmallItemSize = 40f;   // CarouselDefaults.MinSmallItemSize
        public const float MaxSmallItemSize = 56f;   // CarouselDefaults.MaxSmallItemSize
        public const float AnchorSize = 10f;         // CarouselDefaults.AnchorSize
        public const float MediumLargeItemDiffThreshold = 0.85f;

        public static KeylineList MultiBrowse(float carouselMainAxisSize, float preferredItemSize, float itemSpacing, int itemCount,
            float minSmallItemSize = MinSmallItemSize, float maxSmallItemSize = MaxSmallItemSize)
        {
            if (carouselMainAxisSize == 0f || preferredItemSize == 0f) return KeylineList.Empty;
            int[] smallCounts = { 1 };
            int[] mediumCounts = { 1, 0 };
            float targetLargeSize = Math.Min(preferredItemSize, carouselMainAxisSize);
            float targetSmallSize = Math.Min(Math.Max(targetLargeSize / 3f, minSmallItemSize), maxSmallItemSize);
            float targetMediumSize = (targetLargeSize + targetSmallSize) / 2f;
            if (carouselMainAxisSize < minSmallItemSize * 2) smallCounts = new[] { 0 };
            float minAvailableLargeSpace = carouselMainAxisSize - targetMediumSize * mediumCounts.Max() - maxSmallItemSize * smallCounts.Max();
            int minLargeCount = Math.Max(1, (int)Math.Floor(minAvailableLargeSpace / targetLargeSize));
            int maxLargeCount = (int)Math.Ceiling(carouselMainAxisSize / targetLargeSize);
            int[] largeCounts = Enumerable.Range(0, maxLargeCount - minLargeCount + 1).Select(i => maxLargeCount - i).ToArray();
            var arrangement = Arrangement.FindLowestCostArrangement(carouselMainAxisSize, itemSpacing, targetSmallSize, minSmallItemSize,
                maxSmallItemSize, smallCounts, targetMediumSize, mediumCounts, targetLargeSize, largeCounts);
            if (arrangement != null && arrangement.ItemCount() > itemCount)
            {
                int surplus = arrangement.ItemCount() - itemCount;
                int smallCount = arrangement.SmallCount, mediumCount = arrangement.MediumCount;
                while (surplus > 0)
                {
                    if (smallCount > 0) smallCount -= 1;
                    else if (mediumCount > 1) mediumCount -= 1;
                    surplus -= 1;
                }
                arrangement = Arrangement.FindLowestCostArrangement(carouselMainAxisSize, itemSpacing, targetSmallSize, minSmallItemSize,
                    maxSmallItemSize, new[] { smallCount }, targetMediumSize, new[] { mediumCount }, targetLargeSize, largeCounts);
            }
            if (arrangement == null) return KeylineList.Empty;
            return LeftAligned(carouselMainAxisSize, itemSpacing, AnchorSize, AnchorSize, arrangement);
        }

        public static KeylineList Uncontained(float carouselMainAxisSize, float itemSize, float itemSpacing)
        {
            if (carouselMainAxisSize == 0f || itemSize == 0f) return KeylineList.Empty;
            float largeItemSize = Math.Min(itemSize + itemSpacing, carouselMainAxisSize);
            int largeCount = Math.Max(1, (int)Math.Floor(carouselMainAxisSize / largeItemSize));
            float remainingSpace = carouselMainAxisSize - largeCount * largeItemSize;
            int mediumCount = remainingSpace > 0 ? 1 : 0;
            float mediumItemSize = CalculateMediumChildSize(AnchorSize, largeItemSize, remainingSpace);
            var arrangement = new Arrangement(0, 0f, 0, mediumItemSize, mediumCount, largeItemSize, largeCount);
            float xSmallSize = Math.Min(AnchorSize, itemSize);
            float leftAnchorSize = Math.Max(xSmallSize, mediumItemSize * 0.5f);
            return LeftAligned(carouselMainAxisSize, itemSpacing, leftAnchorSize, AnchorSize, arrangement);
        }

        public static KeylineList Hero(float carouselMainAxisSize, float? preferredItemSize, float itemSpacing, int itemCount, bool isCentered = false,
            float minSmallItemSize = MinSmallItemSize, float maxSmallItemSize = MaxSmallItemSize)
        {
            if (carouselMainAxisSize == 0f) return KeylineList.Empty;
            bool shouldCenter = isCentered && itemCount >= 3;
            int[] smallCounts = itemCount <= 1 ? new[] { 0 } : shouldCenter ? new[] { 2 } : new[] { 1 };
            float targetLargeSize = Math.Min(preferredItemSize ?? carouselMainAxisSize, carouselMainAxisSize);
            float targetSmallSize = Math.Min(Math.Max(targetLargeSize / 3f, minSmallItemSize), maxSmallItemSize);
            float fullscreenThreshold = (minSmallItemSize * smallCounts.Max()) + (minSmallItemSize * 1.25f);
            if (carouselMainAxisSize < fullscreenThreshold) smallCounts = new[] { 0 };
            float minAvailableLargeSpace = carouselMainAxisSize - (minSmallItemSize * smallCounts.Max());
            int minLargeCount = Math.Max(1, (int)Math.Floor(minAvailableLargeSpace / targetLargeSize));
            int maxLargeCount = (int)Math.Ceiling(carouselMainAxisSize / targetLargeSize);
            int[] largeCounts = Enumerable.Range(0, maxLargeCount - minLargeCount + 1).Select(i => maxLargeCount - i).ToArray();
            var arrangement = Arrangement.FindLowestCostArrangement(carouselMainAxisSize, itemSpacing, targetSmallSize, minSmallItemSize,
                maxSmallItemSize, smallCounts, 0f, new[] { 0 }, targetLargeSize, largeCounts);
            if (arrangement == null) return KeylineList.Empty;
            return shouldCenter && itemCount >= arrangement.ItemCount()
                ? CenterAligned(carouselMainAxisSize, itemSpacing, AnchorSize, AnchorSize, arrangement)
                : LeftAligned(carouselMainAxisSize, itemSpacing, AnchorSize, AnchorSize, arrangement);
        }

        public static KeylineList LeftAligned(float carouselMainAxisSize, float itemSpacing, float leftAnchorSize, float rightAnchorSize, Arrangement a)
        {
            var b = new KeylineList.Builder().Add(leftAnchorSize, true);
            for (int i = 0; i < a.LargeCount; i++) b.Add(a.LargeSize);
            for (int i = 0; i < a.MediumCount; i++) b.Add(a.MediumSize);
            for (int i = 0; i < a.SmallCount; i++) b.Add(a.SmallSize);
            b.Add(rightAnchorSize, true);
            return b.CreateWithAlignment(carouselMainAxisSize, itemSpacing, CarouselAlignment.Start);
        }

        public static KeylineList CenterAligned(float carouselMainAxisSize, float itemSpacing, float leftAnchorSize, float rightAnchorSize, Arrangement a)
        {
            var b = new KeylineList.Builder().Add(leftAnchorSize, true);
            for (int i = 0; i < a.SmallCount / 2; i++) b.Add(a.SmallSize);
            for (int i = 0; i < a.MediumCount / 2; i++) b.Add(a.MediumSize);
            for (int i = 0; i < a.LargeCount; i++) b.Add(a.LargeSize);
            for (int i = 0; i < a.MediumCount / 2; i++) b.Add(a.MediumSize);
            for (int i = 0; i < a.SmallCount / 2; i++) b.Add(a.SmallSize);
            b.Add(rightAnchorSize, true);
            return b.CreateWithAlignment(carouselMainAxisSize, itemSpacing, CarouselAlignment.Center);
        }

        static float CalculateMediumChildSize(float minimumMediumSize, float largeItemSize, float remainingSpace)
        {
            float medium = Math.Max(remainingSpace * 1.5f, minimumMediumSize);
            float largeItemThreshold = largeItemSize * MediumLargeItemDiffThreshold;
            if (medium > largeItemThreshold)
            {
                float sizeWithFifthCutOff = remainingSpace * 1.2f;
                medium = Math.Min(Math.Max(largeItemThreshold, sizeWithFifthCutOff), largeItemSize);
            }
            return medium;
        }
    }

    /// <summary>Strategy</summary>
    public sealed class CarouselStrategy
    {
        public readonly KeylineList DefaultKeylines;
        public readonly List<KeylineList> StartKeylineSteps, EndKeylineSteps;
        public readonly float AvailableSpace, ItemSpacing, BeforeContentPadding, AfterContentPadding;
        public readonly float StartShiftDistance, EndShiftDistance;
        public readonly List<float> StartShiftPoints, EndShiftPoints;
        public readonly float MinItemSize, MaxItemSize;

        public CarouselStrategy(KeylineList defaultKeylines, float availableSpace, float itemSpacing, float beforeContentPadding, float afterContentPadding)
        {
            DefaultKeylines = defaultKeylines;
            AvailableSpace = availableSpace; ItemSpacing = itemSpacing;
            BeforeContentPadding = beforeContentPadding; AfterContentPadding = afterContentPadding;
            StartKeylineSteps = GetStartKeylineSteps(defaultKeylines, availableSpace, itemSpacing, beforeContentPadding);
            EndKeylineSteps = GetEndKeylineSteps(defaultKeylines, availableSpace, itemSpacing, afterContentPadding);
            float min = defaultKeylines.MinSize, max = defaultKeylines.MaxSize;
            foreach (var s in StartKeylineSteps.Concat(EndKeylineSteps)) { if (s.MinSize < min) min = s.MinSize; if (s.MaxSize > max) max = s.MaxSize; }
            MinItemSize = min; MaxItemSize = max;
            StartShiftDistance = StartKeylineSteps.Count == 0 ? 0f
                : Math.Max(StartKeylineSteps[StartKeylineSteps.Count - 1].First.UnadjustedOffset - StartKeylineSteps[0].First.UnadjustedOffset, beforeContentPadding);
            EndShiftDistance = EndKeylineSteps.Count == 0 ? 0f
                : Math.Max(EndKeylineSteps[0].Last.UnadjustedOffset - EndKeylineSteps[EndKeylineSteps.Count - 1].Last.UnadjustedOffset, afterContentPadding);
            StartShiftPoints = GetStepInterpolationPoints(StartShiftDistance, StartKeylineSteps, true);
            EndShiftPoints = GetStepInterpolationPoints(EndShiftDistance, EndKeylineSteps, false);
        }

        public float ItemMainAxisSize => DefaultKeylines.FirstFocal.Size;
        public bool IsValid => !DefaultKeylines.IsEmpty && AvailableSpace != 0f && ItemMainAxisSize != 0f;

        public KeylineList GetKeylineListForScrollOffset(float scrollOffset, float maxScrollOffset, bool roundToNearestStep = false)
        {
            float positive = Math.Max(0f, scrollOffset);
            float startShiftOffset = StartShiftDistance;
            float endShiftOffset = Math.Max(0f, maxScrollOffset - EndShiftDistance);
            if (positive >= startShiftOffset && positive <= endShiftOffset) return DefaultKeylines;

            float interpolation = Lerp(1f, 0f, 0f, startShiftOffset, positive);
            var shiftPoints = StartShiftPoints;
            var steps = StartKeylineSteps;
            if (positive > endShiftOffset)
            {
                interpolation = Lerp(0f, 1f, endShiftOffset, maxScrollOffset, positive);
                shiftPoints = EndShiftPoints;
                steps = EndKeylineSteps;
                if (endShiftOffset < 0.01f && StartKeylineSteps.Count == 2 && EndKeylineSteps.Count == 2)
                    steps = new List<KeylineList> { StartKeylineSteps[StartKeylineSteps.Count - 1], EndKeylineSteps[EndKeylineSteps.Count - 1] };
            }
            int fromStep = 0, toStep = 0;
            float stepped = 0f;
            float lower = shiftPoints[0];
            for (int i = 1; i < steps.Count; i++)
            {
                float upper = shiftPoints[i];
                if (interpolation <= upper)
                {
                    fromStep = i - 1; toStep = i;
                    stepped = Lerp(0f, 1f, lower, upper, interpolation);
                    break;
                }
                lower = upper;
            }
            if (roundToNearestStep) return steps[(int)Math.Round(stepped, MidpointRounding.AwayFromZero) == 0 ? fromStep : toStep];
            return KeylineList.Lerp(steps[fromStep], steps[toStep], stepped);
        }

        static List<KeylineList> GetStartKeylineSteps(KeylineList d, float main, float spacing, float beforePadding)
        {
            var steps = new List<KeylineList>();
            if (d.IsEmpty) return steps;
            steps.Add(d);
            if (d.IsFirstFocalItemAtStartOfContainer())
            {
                if (beforePadding != 0f) steps.Add(ShiftForContentPadding(d, main, spacing, beforePadding, d.FirstFocal, d.FirstFocalIndex));
                return steps;
            }
            int startIndex = d.FirstNonAnchorIndex, endIndex = d.FirstFocalIndex, n = endIndex - startIndex;
            if (n <= 0 && d.FirstFocal.Cutoff > 0)
            {
                steps.Add(MoveKeyline(d, 0, 0, main, spacing));
                return steps;
            }
            for (int i = 0; i < n; i++)
            {
                var prev = steps[steps.Count - 1];
                int originalItemIndex = startIndex + i;
                int dst = d.LastIndex;
                if (originalItemIndex > 0) dst = prev.FirstIndexAfterFocalRangeWithSize(d[originalItemIndex - 1].Size) - 1;
                steps.Add(MoveKeyline(prev, d.FirstNonAnchorIndex, dst, main, spacing));
            }
            if (beforePadding != 0f)
            {
                var last = steps[steps.Count - 1];
                steps[steps.Count - 1] = ShiftForContentPadding(last, main, spacing, beforePadding, last.FirstFocal, last.FirstFocalIndex);
            }
            return steps;
        }

        static List<KeylineList> GetEndKeylineSteps(KeylineList d, float main, float spacing, float afterPadding)
        {
            var steps = new List<KeylineList>();
            if (d.IsEmpty) return steps;
            steps.Add(d);
            if (d.IsLastFocalItemAtEndOfContainer(main))
            {
                if (afterPadding != 0f) steps.Add(ShiftForContentPadding(d, main, spacing, -afterPadding, d.LastFocal, d.LastFocalIndex));
                return steps;
            }
            int startIndex = d.LastFocalIndex, endIndex = d.LastNonAnchorIndex, n = endIndex - startIndex;
            if (n <= 0 && d.LastFocal.Cutoff > 0)
            {
                steps.Add(MoveKeyline(d, 0, 0, main, spacing));
                return steps;
            }
            for (int i = 0; i < n; i++)
            {
                var prev = steps[steps.Count - 1];
                int originalItemIndex = endIndex - i;
                int dst = 0;
                if (originalItemIndex < d.LastIndex) dst = prev.LastIndexBeforeFocalRangeWithSize(d[originalItemIndex + 1].Size) + 1;
                steps.Add(MoveKeyline(prev, d.LastNonAnchorIndex, dst, main, spacing));
            }
            if (afterPadding != 0f)
            {
                var last = steps[steps.Count - 1];
                steps[steps.Count - 1] = ShiftForContentPadding(last, main, spacing, -afterPadding, last.LastFocal, last.LastFocalIndex);
            }
            return steps;
        }

        static KeylineList ShiftForContentPadding(KeylineList from, float main, float spacing, float contentPadding, Keyline pivot, int pivotIndex)
        {
            int nonAnchor = from.Keylines.Count(k => !k.IsAnchor);
            float sizeReduction = contentPadding / nonAnchor;
            var b = new KeylineList.Builder();
            foreach (var k in from.Keylines) b.Add(k.Size - Math.Abs(sizeReduction), k.IsAnchor);
            var shifted = b.CreateWithPivot(main, spacing, pivotIndex, pivot.Offset - (sizeReduction / 2f) + contentPadding);
            var list = new List<Keyline>();
            for (int i = 0; i < shifted.Count; i++) list.Add(shifted[i].WithUnadjustedOffset(from[i].UnadjustedOffset));
            return new KeylineList(list);
        }

        static KeylineList MoveKeyline(KeylineList from, int src, int dst, float main, float spacing)
        {
            int pivotDir = src > dst ? 1 : -1;
            float pivotDelta = (from[src].Size - from[src].Cutoff + spacing) * pivotDir;
            int newPivotIndex = from.PivotIndex + pivotDir;
            float newPivotOffset = from.Pivot.Offset + pivotDelta;
            var moved = from.Keylines.ToList();
            var k = moved[src];
            moved.RemoveAt(src);
            moved.Insert(dst, k);
            var b = new KeylineList.Builder();
            foreach (var m in moved) b.Add(m.Size, m.IsAnchor);
            return b.CreateWithPivot(main, spacing, newPivotIndex, newPivotOffset);
        }

        static List<float> GetStepInterpolationPoints(float totalShiftDistance, List<KeylineList> steps, bool isShiftingLeft)
        {
            var points = new List<float> { 0f };
            if (totalShiftDistance == 0f || steps.Count == 0) return points;
            for (int i = 1; i < steps.Count; i++)
            {
                var prev = steps[i - 1];
                var curr = steps[i];
                float shifted = isShiftingLeft ? curr.First.UnadjustedOffset - prev.First.UnadjustedOffset : prev.Last.UnadjustedOffset - curr.Last.UnadjustedOffset;
                float pct = shifted / totalShiftDistance;
                points.Add(i == steps.Count - 1 ? 1f : points[i - 1] + pct);
            }
            return points;
        }

        public static float Lerp(float outputMin, float outputMax, float inputMin, float inputMax, float value)
        {
            if (value <= inputMin) return outputMin;
            if (value >= inputMax) return outputMax;
            float f = (value - inputMin) / (inputMax - inputMin);
            return (1 - f) * outputMin + f * outputMax;
        }
    }
}
