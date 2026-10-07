using System;
using System.Collections.Generic;
using System.Linq;
using M3E4Unity.Shapes;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.UI;
using Point = M3E4Unity.Shapes.Point;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Progress indicators (ProgressIndicator.kt, WavyProgressIndicator.kt) and loading indicators
    /// (LoadingIndicator.kt).
    /// </summary>
    public static class M3ProgressIndicators
    {
        const int ProgressFrames = 101;

        static RectTransform Root(Transform parent, string name, float w, float h)
        {
            var root = M3Build.Rect(name, parent);
            M3Build.Size(root, w, h);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w;
            le.minHeight = le.preferredHeight = h;
            return root;
        }

        static M3Progress Animate(RectTransform root, Image[] parts, Func<int, float, ShapeCell> cell, float initial)
        {
            var frames = new Sprite[parts.Length * ProgressFrames];
            for (int p = 0; p < parts.Length; p++)
            for (int i = 0; i < ProgressFrames; i++)
                frames[p * ProgressFrames + i] = M3ShapeAtlas.Get(cell(p, i / (float)(ProgressFrames - 1)));
            var prog = M3UdonBridge.Add<M3Progress>(root.gameObject);
            prog.parts = parts;
            prog.frames = frames;
            prog.frameCount = ProgressFrames;
            prog.target = initial;
            int f = Mathf.RoundToInt(initial * (ProgressFrames - 1));
            for (int p = 0; p < parts.Length; p++) parts[p].sprite = frames[p * ProgressFrames + f];
            M3UdonBridge.Sync(prog);
            return prog;
        }

        /// <summary>WavyProgressIndicatorDefaults.indicatorAmplitude: 0 below 10% and from 95%.</summary>
        public static float IndicatorAmplitude(float progress) => progress <= 0.1f || progress >= 0.95f ? 0f : 1f;

        /// <param name="progress">null = indeterminate.</param>
        public static RectTransform Linear(Transform parent, M3Context ctx, float? progress = 0.5f, bool wavy = false, float width = 240f)
        {
            // LinearProgressIndicator: height = LinearProgressIndicatorTokens.Height, stroke = height
            // LinearWavyProgressIndicator: container WaveHeight, strokes Active/TrackThickness
            float h = wavy ? LinearProgressIndicatorTokens.WaveHeight : LinearProgressIndicatorTokens.Height;
            float stroke = wavy ? LinearProgressIndicatorTokens.ActiveThickness : h;
            float gap = LinearProgressIndicatorTokens.TrackActiveSpace;
            float stop = LinearProgressIndicatorTokens.StopSize;
            float wavelength = progress.HasValue ? LinearProgressIndicatorTokens.ActiveWaveWavelength : LinearProgressIndicatorTokens.IndeterminateActiveWaveWavelength;
            float maxAmp = (h - stroke) / 2f;
            var root = Root(parent, wavy ? "LinearWavyProgressIndicator" : "LinearProgressIndicator", width, h);

            ShapeCell Cell(int part, float p)
            {
                float amp = wavy ? maxAmp * (progress.HasValue ? IndicatorAmplitude(p) : 1f) : 0f;
                return ShapeCell.Create(ShapeKind.LinearProgress)
                    // waves travel toward the end: one wavelength per second (waveSpeed = wavelength)
                    .WithExtra(p, gap, stroke, part, amp, wavelength, -wavelength, progress.HasValue ? 0 : 1)
                    .WithExtra2(progress.HasValue ? stop : 0);
            }

            var track = M3Build.Shape("Track", root, Cell(1, progress ?? 0), ColorRef.Role(ProgressIndicatorTokens.TrackColor));
            var active = M3Build.Shape("Active", root, Cell(0, progress ?? 0), ColorRef.Role(ProgressIndicatorTokens.ActiveIndicatorColor));
            if (progress.HasValue) Animate(root, new[] { active, track }, (part, p) => Cell(part == 0 ? 0 : 1, p), progress.Value);
            return root;
        }

        public static RectTransform Circular(Transform parent, M3Context ctx, float? progress = 0.5f, bool wavy = false)
        {
            float size = wavy ? CircularProgressIndicatorTokens.WaveSize : CircularProgressIndicatorTokens.Size;
            float stroke = wavy ? CircularProgressIndicatorTokens.ActiveThickness : CircularProgressIndicatorTokens.TrackThickness;
            float gap = CircularProgressIndicatorTokens.TrackActiveSpace;
            var root = Root(parent, wavy ? "CircularWavyProgressIndicator" : "CircularProgressIndicator", size, size);

            int firstTile = 0, tileCount = 0;
            float waveDegPerSec = 0f;
            if (wavy)
            {
                // CircularShapes.update: vertices = max(5, round(2 * PI * r / wavelength)), r = minDimension / 2 - stroke / 2
                float r = size / 2f - stroke / 2f;
                int n = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * r / CircularProgressIndicatorTokens.ActiveWaveWavelength));
                (firstTile, tileCount) = WavyCircleTiles(n);
                // one wavelength per second: a full turn every n seconds
                waveDegPerSec = 360f / n;
            }

            ShapeCell Cell(int part, float p)
            {
                float amp = wavy ? (progress.HasValue ? IndicatorAmplitude(p) : 1f) : 0f;
                return ShapeCell.Create(ShapeKind.CircularProgress)
                    .WithExtra(p, gap, stroke, part, firstTile, tileCount, waveDegPerSec, progress.HasValue ? 0 : 1)
                    .WithExtra2(amp);
            }

            var track = M3Build.Shape("Track", root, Cell(1, progress ?? 0), ColorRef.Role(ProgressIndicatorTokens.TrackColor));
            var active = M3Build.Shape("Active", root, Cell(0, progress ?? 0), ColorRef.Role(ProgressIndicatorTokens.ActiveIndicatorColor));
            if (progress.HasValue) Animate(root, new[] { active, track }, (part, p) => Cell(part, p), progress.Value);
            return root;
        }

        /// <summary>Path tiles morphing a circle into the wavy star of CircularShapes (amplitude 0..1).</summary>
        static (int first, int count) WavyCircleTiles(int n)
        {
            const int frames = 8;
            var circle = RoundedPolygon.Circle(n).Normalized();
            var star = RoundedPolygon.Star(n, innerRadius: 0.75f, rounding: new CornerRounding(0.35f, 0.4f), innerRounding: new CornerRounding(0.5f)).Normalized();
            var morph = new Morph(circle, star);
            int first = -1;
            for (int i = 0; i < frames; i++)
            {
                float a = i / (float)(frames - 1);
                int tile = M3ShapeTiles.GetPath($"wavycircle:{n}:{i}/{frames}", morph.AsCubics(a));
                if (first < 0) first = tile;
            }
            return (first, frames);
        }

        // ---------------- Loading indicator ----------------

        static readonly MaterialShape[] Indeterminate =
        {
            MaterialShape.SoftBurst, MaterialShape.Cookie9Sided, MaterialShape.Pentagon, MaterialShape.Pill,
            MaterialShape.Sunny, MaterialShape.Cookie4Sided, MaterialShape.Oval,
        };

        const int MorphFrames = 24;
        const float MorphMin = 0f, MorphMax = 1.1f;

        /// <summary>calculateScaleFactor(polygons) * ActiveIndicatorScale (LoadingIndicator.kt).</summary>
        static float ScaleFactor(IEnumerable<RoundedPolygon> polygons)
        {
            float scale = 1f;
            foreach (var p in polygons)
            {
                var b = p.CalculateBounds();
                var m = p.CalculateMaxBounds();
                float sx = (b[2] - b[0]) / (m[2] - m[0]);
                float sy = (b[3] - b[1]) / (m[3] - m[1]);
                scale = Mathf.Min(scale, Mathf.Max(sx, sy));
            }
            float active = LoadingIndicatorTokens.ActiveSize / Mathf.Min(LoadingIndicatorTokens.ContainerWidth, LoadingIndicatorTokens.ContainerHeight);
            return scale * active;
        }

        /// <summary>processPath: scale by size * scaleFactor and center the path bounds in the box.</summary>
        static List<Cubic> Fit(List<Cubic> cubics, float scale)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in cubics)
            {
                var pts = c.Points;
                for (int i = 0; i < 8; i += 2)
                {
                    float x = pts[i] * scale, y = pts[i + 1] * scale;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }
            float dx = 0.5f - (minX + maxX) / 2f, dy = 0.5f - (minY + maxY) / 2f;
            return cubics.Select(c => c.Transformed((x, y) => new Point(x * scale + dx, y * scale + dy))).ToList();
        }

        /// <summary>Morph frames for the indeterminate sequence; returns the first tile.</summary>
        static int IndeterminateTiles()
        {
            var polys = Indeterminate.Select(MaterialShapes.Get).ToList();
            float scale = ScaleFactor(polys);
            int first = -1;
            for (int m = 0; m < polys.Count; m++)
            {
                var morph = new Morph(polys[m].Normalized(), polys[(m + 1) % polys.Count].Normalized());
                for (int f = 0; f < MorphFrames; f++)
                {
                    float p = Mathf.Lerp(MorphMin, MorphMax, f / (float)(MorphFrames - 1));
                    int tile = M3ShapeTiles.GetFilled($"loading:{m}:{f}/{MorphFrames}", Fit(morph.AsCubics(p), scale));
                    if (first < 0) first = tile;
                }
            }
            return first;
        }

        public static RectTransform Loading(Transform parent, M3Context ctx, bool contained = false, float? progress = null)
        {
            float w = LoadingIndicatorTokens.ContainerWidth, h = LoadingIndicatorTokens.ContainerHeight;
            var root = Root(parent, contained ? "ContainedLoadingIndicator" : "LoadingIndicator", w, h);
            if (contained)
                M3Build.Shape("Container", root, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(LoadingIndicatorTokens.ContainerShape)),
                    ColorRef.Role(LoadingIndicatorTokens.ContainedContainerColor));
            var color = contained ? LoadingIndicatorTokens.ContainedActiveColor : LoadingIndicatorTokens.ActiveIndicatorColor;

            if (!progress.HasValue)
            {
                int first = IndeterminateTiles();
                // morphAnimationSpec = spring(dampingRatio = 0.6, stiffness = 200, visibilityThreshold = 0.1)
                var spring = new Spring(0.6f, 200f);
                float end = spring.DurationMillis(0, 1, 0, 0.1) / 1000f;
                var cell = ShapeCell.Create(ShapeKind.LoadingIndeterminate)
                    .WithExtra(first, Indeterminate.Length, MorphFrames, end, MorphMin, MorphMax, spring.DampingRatio, spring.Stiffness);
                M3Build.Shape("Indicator", root, cell, ColorRef.Role(color));
                return root;
            }

            // Determinate: Morph(Circle rotated 360/20, SoftBurst), rotation -progress * 180
            var circle = MaterialShapes.Get(MaterialShape.Circle);
            float a = 360f / 20f * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            var rotated = circle.Transformed((x, y) => new Point(c * x - s * y, s * x + c * y));
            var polys = new List<RoundedPolygon> { rotated, MaterialShapes.Get(MaterialShape.SoftBurst) };
            float scale = ScaleFactor(polys);
            var morph = new Morph(polys[0].Normalized(), polys[1].Normalized());
            var image = M3Build.Shape("Indicator", root, ShapeCell.Create(ShapeKind.SdfTile), ColorRef.Role(color));
            Animate(root, new[] { image }, (part, p) =>
            {
                // tiles every 2 %: the rotation (exact per frame) carries most of the motion
                float pq = Mathf.Round(p * 50f) / 50f;
                int tile = M3ShapeTiles.GetFilled($"loadingdet:{Mathf.RoundToInt(pq * 50)}", Fit(morph.AsCubics(pq), scale));
                // rotate(-progress * 180): counter-clockwise
                return ShapeCell.Create(ShapeKind.SdfTile).WithExtra(tile, -p * 180f, 1f);
            }, progress.Value);
            return root;
        }
    }
}
