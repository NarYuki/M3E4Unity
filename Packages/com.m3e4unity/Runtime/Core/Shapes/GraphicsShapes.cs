// C# port of androidx.graphics.shapes (graphics/graphics-shapes/src/commonMain).
// Copyright 2022-2025 The Android Open Source Project, Apache License 2.0.
// Arithmetic is kept in single precision like the Kotlin original (Float), and
// member names follow it so the two can be compared side by side.
using System;
using System.Collections.Generic;
using System.Linq;

namespace M3E4Unity.Shapes
{
    /// <summary>FloatFloatPair used as a 2D point / vector.</summary>
    public readonly struct Point : IEquatable<Point>
    {
        public readonly float X, Y;
        public Point(float x, float y) { X = x; Y = y; }

        public float GetDistance() => (float)Math.Sqrt(X * X + Y * Y);
        public float GetDistanceSquared() => X * X + Y * Y;
        public float DotProduct(Point o) => X * o.X + Y * o.Y;
        public float DotProduct(float ox, float oy) => X * ox + Y * oy;
        public bool Clockwise(Point o) => X * o.Y - Y * o.X > 0;

        public Point GetDirection()
        {
            float d = GetDistance();
            if (!(d > 0f)) throw new ArgumentException("Can't get the direction of a 0-length vector");
            return this / d;
        }

        public Point Rotate90() => new Point(-Y, X);
        public static Point operator -(Point a) => new Point(-a.X, -a.Y);
        public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
        public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
        public static Point operator *(Point a, float s) => new Point(a.X * s, a.Y * s);
        public static Point operator /(Point a, float s) => new Point(a.X / s, a.Y / s);
        public Point Transformed(PointTransformer f) => f(X, Y);
        public bool Equals(Point o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Point p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>Transforms a point; returns the transformed point (TransformResult).</summary>
    public delegate Point PointTransformer(float x, float y);

    internal static class ShapeUtils
    {
        public const float DistanceEpsilon = 1e-4f;
        public const float AngleEpsilon = 1e-6f;
        public const float RelaxedDistanceEpsilon = 5e-3f;
        public const float FloatPi = (float)Math.PI;
        public const float TwoPi = 2 * (float)Math.PI;
        public static readonly Point Zero = new Point(0f, 0f);

        public static float Sqrt(float x) => (float)Math.Sqrt(x);
        public static float Cos(float x) => (float)Math.Cos(x);
        public static float Sin(float x) => (float)Math.Sin(x);
        public static float Distance(float x, float y) => Sqrt(x * x + y * y);
        public static float DistanceSquared(float x, float y) => x * x + y * y;

        public static Point DirectionVector(float x, float y)
        {
            float d = Distance(x, y);
            if (!(d > 0f)) throw new ArgumentException("Required distance greater than zero");
            return new Point(x / d, y / d);
        }

        public static Point DirectionVector(float angleRadians) => new Point(Cos(angleRadians), Sin(angleRadians));

        public static Point RadialToCartesian(float radius, float angleRadians) => DirectionVector(angleRadians) * radius;
        public static Point RadialToCartesian(float radius, float angleRadians, Point center) => DirectionVector(angleRadians) * radius + center;

        public static float Square(float x) => x * x;
        public static float Interpolate(float start, float stop, float fraction) => (1 - fraction) * start + fraction * stop;

        public static Point Interpolate(Point start, Point stop, float fraction) =>
            new Point(Interpolate(start.X, stop.X, fraction), Interpolate(start.Y, stop.Y, fraction));

        public static float PositiveModulo(float num, float mod) => (num % mod + mod) % mod;

        public static bool Convex(Point previous, Point current, Point next) => (current - previous).Clockwise(next - current);

        public static float ProgressDistance(float p1, float p2)
        {
            float d = Math.Abs(p1 - p2);
            return Math.Min(d, 1f - d);
        }

        public static bool ProgressInRange(float progress, float progressFrom, float progressTo) =>
            progressTo >= progressFrom
                ? progress >= progressFrom && progress <= progressTo
                : progress >= progressFrom || progress <= progressTo;
    }

    /// <summary>A cubic Bézier: anchor0, control0, control1, anchor1.</summary>
    public class Cubic : IEquatable<Cubic>
    {
        internal readonly float[] points;

        internal Cubic(float[] points)
        {
            if (points.Length != 8) throw new ArgumentException("Points array size should be 8");
            this.points = points;
        }

        public Cubic() : this(new float[8]) { }

        public Cubic(float anchor0X, float anchor0Y, float control0X, float control0Y,
            float control1X, float control1Y, float anchor1X, float anchor1Y)
            : this(new[] { anchor0X, anchor0Y, control0X, control0Y, control1X, control1Y, anchor1X, anchor1Y }) { }

        internal Cubic(Point anchor0, Point control0, Point control1, Point anchor1)
            : this(anchor0.X, anchor0.Y, control0.X, control0.Y, control1.X, control1.Y, anchor1.X, anchor1.Y) { }

        public float Anchor0X => points[0];
        public float Anchor0Y => points[1];
        public float Control0X => points[2];
        public float Control0Y => points[3];
        public float Control1X => points[4];
        public float Control1Y => points[5];
        public float Anchor1X => points[6];
        public float Anchor1Y => points[7];
        public IReadOnlyList<float> Points => points;

        internal Point PointOnCurve(float t)
        {
            float u = 1 - t;
            return new Point(
                Anchor0X * (u * u * u) + Control0X * (3 * t * u * u) + Control1X * (3 * t * t * u) + Anchor1X * (t * t * t),
                Anchor0Y * (u * u * u) + Control0Y * (3 * t * u * u) + Control1Y * (3 * t * t * u) + Anchor1Y * (t * t * t));
        }

        internal bool ZeroLength() =>
            Math.Abs(Anchor0X - Anchor1X) < ShapeUtils.DistanceEpsilon && Math.Abs(Anchor0Y - Anchor1Y) < ShapeUtils.DistanceEpsilon;

        internal bool ConvexTo(Cubic next) =>
            ShapeUtils.Convex(new Point(Anchor0X, Anchor0Y), new Point(Anchor1X, Anchor1Y), new Point(next.Anchor1X, next.Anchor1Y));

        static bool ZeroIsh(float value) => Math.Abs(value) < ShapeUtils.DistanceEpsilon;

        internal void CalculateBounds(float[] bounds, bool approximate = false)
        {
            if (ZeroLength())
            {
                bounds[0] = Anchor0X; bounds[1] = Anchor0Y; bounds[2] = Anchor0X; bounds[3] = Anchor0Y;
                return;
            }
            float minX = Math.Min(Anchor0X, Anchor1X), minY = Math.Min(Anchor0Y, Anchor1Y);
            float maxX = Math.Max(Anchor0X, Anchor1X), maxY = Math.Max(Anchor0Y, Anchor1Y);
            if (approximate)
            {
                bounds[0] = Math.Min(minX, Math.Min(Control0X, Control1X));
                bounds[1] = Math.Min(minY, Math.Min(Control0Y, Control1Y));
                bounds[2] = Math.Max(maxX, Math.Max(Control0X, Control1X));
                bounds[3] = Math.Max(maxY, Math.Max(Control0Y, Control1Y));
                return;
            }
            Extend(-Anchor0X + 3 * Control0X - 3 * Control1X + Anchor1X, 2 * Anchor0X - 4 * Control0X + 2 * Control1X, -Anchor0X + Control0X, true, ref minX, ref maxX);
            Extend(-Anchor0Y + 3 * Control0Y - 3 * Control1Y + Anchor1Y, 2 * Anchor0Y - 4 * Control0Y + 2 * Control1Y, -Anchor0Y + Control0Y, false, ref minY, ref maxY);
            bounds[0] = minX; bounds[1] = minY; bounds[2] = maxX; bounds[3] = maxY;
        }

        void Extend(float a, float b, float c, bool isX, ref float min, ref float max)
        {
            void Visit(float t, ref float mn, ref float mx)
            {
                if (t >= 0f && t <= 1f)
                {
                    var p = PointOnCurve(t);
                    float v = isX ? p.X : p.Y;
                    if (v < mn) mn = v;
                    if (v > mx) mx = v;
                }
            }
            if (ZeroIsh(a))
            {
                if (b != 0f) Visit(2 * c / (-2 * b), ref min, ref max);
            }
            else
            {
                float s = b * b - 4 * a * c;
                if (s >= 0)
                {
                    Visit((-b + ShapeUtils.Sqrt(s)) / (2 * a), ref min, ref max);
                    Visit((-b - ShapeUtils.Sqrt(s)) / (2 * a), ref min, ref max);
                }
            }
        }

        public (Cubic, Cubic) Split(float t)
        {
            float u = 1 - t;
            var p = PointOnCurve(t);
            return (
                new Cubic(Anchor0X, Anchor0Y,
                    Anchor0X * u + Control0X * t, Anchor0Y * u + Control0Y * t,
                    Anchor0X * (u * u) + Control0X * (2 * u * t) + Control1X * (t * t),
                    Anchor0Y * (u * u) + Control0Y * (2 * u * t) + Control1Y * (t * t),
                    p.X, p.Y),
                new Cubic(p.X, p.Y,
                    Control0X * (u * u) + Control1X * (2 * u * t) + Anchor1X * (t * t),
                    Control0Y * (u * u) + Control1Y * (2 * u * t) + Anchor1Y * (t * t),
                    Control1X * u + Anchor1X * t, Control1Y * u + Anchor1Y * t,
                    Anchor1X, Anchor1Y));
        }

        public Cubic Reverse() => new Cubic(Anchor1X, Anchor1Y, Control1X, Control1Y, Control0X, Control0Y, Anchor0X, Anchor0Y);

        public static Cubic operator +(Cubic a, Cubic b)
        {
            var r = new float[8];
            for (int i = 0; i < 8; i++) r[i] = a.points[i] + b.points[i];
            return new Cubic(r);
        }

        public static Cubic operator *(Cubic a, float x)
        {
            var r = new float[8];
            for (int i = 0; i < 8; i++) r[i] = a.points[i] * x;
            return new Cubic(r);
        }

        public static Cubic operator /(Cubic a, float x) => a * (1f / x);

        public Cubic Transformed(PointTransformer f)
        {
            var c = new MutableCubic();
            Array.Copy(points, c.points, 8);
            c.Transform(f);
            return c;
        }

        public bool Equals(Cubic other) => other != null && points.SequenceEqual(other.points);
        public override bool Equals(object obj) => obj is Cubic c && Equals(c);

        public override int GetHashCode()
        {
            int h = 1;
            foreach (float p in points) h = 31 * h + p.GetHashCode();
            return h;
        }

        public override string ToString() =>
            $"anchor0: ({Anchor0X}, {Anchor0Y}) control0: ({Control0X}, {Control0Y}), control1: ({Control1X}, {Control1Y}), anchor1: ({Anchor1X}, {Anchor1Y})";

        public static Cubic StraightLine(float x0, float y0, float x1, float y1) =>
            new Cubic(x0, y0,
                ShapeUtils.Interpolate(x0, x1, 1f / 3f), ShapeUtils.Interpolate(y0, y1, 1f / 3f),
                ShapeUtils.Interpolate(x0, x1, 2f / 3f), ShapeUtils.Interpolate(y0, y1, 2f / 3f),
                x1, y1);

        public static Cubic CircularArc(float centerX, float centerY, float x0, float y0, float x1, float y1)
        {
            var p0d = ShapeUtils.DirectionVector(x0 - centerX, y0 - centerY);
            var p1d = ShapeUtils.DirectionVector(x1 - centerX, y1 - centerY);
            var rotatedP0 = p0d.Rotate90();
            var rotatedP1 = p1d.Rotate90();
            bool clockwise = rotatedP0.DotProduct(x1 - centerX, y1 - centerY) >= 0;
            float cosa = p0d.DotProduct(p1d);
            if (cosa > 0.999f) return StraightLine(x0, y0, x1, y1);
            float k = ShapeUtils.Distance(x0 - centerX, y0 - centerY) * 4f / 3f *
                      (ShapeUtils.Sqrt(2 * (1 - cosa)) - ShapeUtils.Sqrt(1 - cosa * cosa)) / (1 - cosa) *
                      (clockwise ? 1f : -1f);
            return new Cubic(x0, y0,
                x0 + rotatedP0.X * k, y0 + rotatedP0.Y * k,
                x1 - rotatedP1.X * k, y1 - rotatedP1.Y * k,
                x1, y1);
        }

        internal static Cubic Empty(float x0, float y0) => new Cubic(x0, y0, x0, y0, x0, y0, x0, y0);
    }

    public sealed class MutableCubic : Cubic
    {
        void TransformOnePoint(PointTransformer f, int ix)
        {
            var r = f(points[ix], points[ix + 1]);
            points[ix] = r.X;
            points[ix + 1] = r.Y;
        }

        public void Transform(PointTransformer f)
        {
            TransformOnePoint(f, 0);
            TransformOnePoint(f, 2);
            TransformOnePoint(f, 4);
            TransformOnePoint(f, 6);
        }

        public void Interpolate(Cubic c1, Cubic c2, float progress)
        {
            for (int i = 0; i < 8; i++) points[i] = ShapeUtils.Interpolate(c1.points[i], c2.points[i], progress);
        }
    }

    /// <summary>Corner rounding: radius and smoothing (0..1).</summary>
    public sealed class CornerRounding
    {
        public readonly float Radius;
        public readonly float Smoothing;

        public CornerRounding(float radius = 0f, float smoothing = 0f)
        {
            Radius = radius;
            Smoothing = smoothing;
        }

        public static readonly CornerRounding Unrounded = new CornerRounding();
    }

    public abstract class Feature
    {
        public readonly IReadOnlyList<Cubic> Cubics;
        protected Feature(IReadOnlyList<Cubic> cubics) { Cubics = cubics; }

        public static Feature BuildIgnorableFeature(List<Cubic> cubics) => Validated(new Edge(cubics));
        public static Feature BuildEdge(Cubic cubic) => new Edge(new List<Cubic> { cubic });
        public static Feature BuildConvexCorner(List<Cubic> cubics) => Validated(new Corner(cubics, true));
        public static Feature BuildConcaveCorner(List<Cubic> cubics) => Validated(new Corner(cubics, false));

        static Feature Validated(Feature feature)
        {
            if (feature.Cubics.Count == 0) throw new ArgumentException("Features need at least one cubic.");
            var prev = feature.Cubics[0];
            for (int i = 1; i < feature.Cubics.Count; i++)
            {
                var c = feature.Cubics[i];
                if (Math.Abs(c.Anchor0X - prev.Anchor1X) > ShapeUtils.DistanceEpsilon ||
                    Math.Abs(c.Anchor0Y - prev.Anchor1Y) > ShapeUtils.DistanceEpsilon)
                    throw new ArgumentException("Feature must be continuous");
                prev = c;
            }
            return feature;
        }

        public abstract Feature Transformed(PointTransformer f);
        public abstract Feature Reversed();
        public abstract bool IsIgnorableFeature { get; }
        public abstract bool IsEdge { get; }
        public abstract bool IsConvexCorner { get; }
        public abstract bool IsConcaveCorner { get; }

        internal sealed class Edge : Feature
        {
            public Edge(IReadOnlyList<Cubic> cubics) : base(cubics) { }
            public override Feature Transformed(PointTransformer f) => new Edge(Cubics.Select(c => c.Transformed(f)).ToList());

            public override Feature Reversed()
            {
                var r = new List<Cubic>();
                for (int i = Cubics.Count - 1; i >= 0; i--) r.Add(Cubics[i].Reverse());
                return new Edge(r);
            }

            public override string ToString() => "Edge";
            public override bool IsIgnorableFeature => true;
            public override bool IsEdge => true;
            public override bool IsConvexCorner => false;
            public override bool IsConcaveCorner => false;
        }

        internal sealed class Corner : Feature
        {
            public readonly bool Convex;
            public Corner(IReadOnlyList<Cubic> cubics, bool convex = true) : base(cubics) { Convex = convex; }
            public override Feature Transformed(PointTransformer f) => new Corner(Cubics.Select(c => c.Transformed(f)).ToList(), Convex);

            public override Feature Reversed()
            {
                var r = new List<Cubic>();
                for (int i = Cubics.Count - 1; i >= 0; i--) r.Add(Cubics[i].Reverse());
                return new Corner(r, !Convex);
            }

            public override string ToString() => $"Corner: convex={Convex}";
            public override bool IsIgnorableFeature => false;
            public override bool IsEdge => false;
            public override bool IsConvexCorner => Convex;
            public override bool IsConcaveCorner => !Convex;
        }
    }

    /// <summary>A polygon with optionally rounded / smoothed corners, made of cubics.</summary>
    public sealed class RoundedPolygon
    {
        public readonly IReadOnlyList<Feature> Features;
        internal readonly Point Center;
        public readonly IReadOnlyList<Cubic> Cubics;
        public float CenterX => Center.X;
        public float CenterY => Center.Y;

        internal RoundedPolygon(IReadOnlyList<Feature> features, Point center)
        {
            Features = features;
            Center = center;
            Cubics = BuildCubics(features, center);
            var prev = Cubics[Cubics.Count - 1];
            foreach (var cubic in Cubics)
            {
                if (Math.Abs(cubic.Anchor0X - prev.Anchor1X) > ShapeUtils.DistanceEpsilon ||
                    Math.Abs(cubic.Anchor0Y - prev.Anchor1Y) > ShapeUtils.DistanceEpsilon)
                    throw new ArgumentException("RoundedPolygon must be contiguous, with the anchor points of all curves matching the anchor points of the preceding and succeeding cubics");
                prev = cubic;
            }
        }

        static List<Cubic> BuildCubics(IReadOnlyList<Feature> features, Point center)
        {
            var result = new List<Cubic>();
            Cubic firstCubic = null, lastCubic = null;
            List<Cubic> firstFeatureSplitStart = null, firstFeatureSplitEnd = null;
            if (features.Count > 0 && features[0].Cubics.Count == 3)
            {
                var (start, end) = features[0].Cubics[1].Split(.5f);
                firstFeatureSplitStart = new List<Cubic> { features[0].Cubics[0], start };
                firstFeatureSplitEnd = new List<Cubic> { end, features[0].Cubics[2] };
            }
            for (int i = 0; i <= features.Count; i++)
            {
                IReadOnlyList<Cubic> featureCubics;
                if (i == 0 && firstFeatureSplitEnd != null) featureCubics = firstFeatureSplitEnd;
                else if (i == features.Count)
                {
                    if (firstFeatureSplitStart != null) featureCubics = firstFeatureSplitStart;
                    else break;
                }
                else featureCubics = features[i].Cubics;
                foreach (var cubic in featureCubics)
                {
                    if (!cubic.ZeroLength())
                    {
                        if (lastCubic != null) result.Add(lastCubic);
                        lastCubic = cubic;
                        if (firstCubic == null) firstCubic = cubic;
                    }
                    else if (lastCubic != null)
                    {
                        var copy = (float[])lastCubic.points.Clone();
                        copy[6] = cubic.Anchor1X;
                        copy[7] = cubic.Anchor1Y;
                        lastCubic = new Cubic(copy);
                    }
                }
            }
            if (lastCubic != null && firstCubic != null)
            {
                result.Add(new Cubic(lastCubic.Anchor0X, lastCubic.Anchor0Y, lastCubic.Control0X, lastCubic.Control0Y,
                    lastCubic.Control1X, lastCubic.Control1Y, firstCubic.Anchor0X, firstCubic.Anchor0Y));
            }
            else
            {
                result.Add(new Cubic(center.X, center.Y, center.X, center.Y, center.X, center.Y, center.X, center.Y));
            }
            return result;
        }

        public RoundedPolygon Transformed(PointTransformer f) =>
            new RoundedPolygon(Features.Select(x => x.Transformed(f)).ToList(), Center.Transformed(f));

        public RoundedPolygon Normalized()
        {
            var bounds = CalculateBounds();
            float width = bounds[2] - bounds[0], height = bounds[3] - bounds[1];
            float side = Math.Max(width, height);
            float offsetX = (side - width) / 2 - bounds[0];
            float offsetY = (side - height) / 2 - bounds[1];
            return Transformed((x, y) => new Point((x + offsetX) / side, (y + offsetY) / side));
        }

        public float[] CalculateMaxBounds(float[] bounds = null)
        {
            bounds = bounds ?? new float[4];
            float maxDistSquared = 0f;
            foreach (var cubic in Cubics)
            {
                float anchorDistance = ShapeUtils.DistanceSquared(cubic.Anchor0X - CenterX, cubic.Anchor0Y - CenterY);
                var middle = cubic.PointOnCurve(.5f);
                float middleDistance = ShapeUtils.DistanceSquared(middle.X - CenterX, middle.Y - CenterY);
                maxDistSquared = Math.Max(maxDistSquared, Math.Max(anchorDistance, middleDistance));
            }
            float distance = ShapeUtils.Sqrt(maxDistSquared);
            bounds[0] = CenterX - distance; bounds[1] = CenterY - distance;
            bounds[2] = CenterX + distance; bounds[3] = CenterY + distance;
            return bounds;
        }

        public float[] CalculateBounds(float[] bounds = null, bool approximate = true)
        {
            bounds = bounds ?? new float[4];
            // Kotlin Float.MIN_VALUE is the smallest positive float (float.Epsilon), kept as-is.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.Epsilon, maxY = float.Epsilon;
            foreach (var cubic in Cubics)
            {
                cubic.CalculateBounds(bounds, approximate);
                minX = Math.Min(minX, bounds[0]);
                minY = Math.Min(minY, bounds[1]);
                maxX = Math.Max(maxX, bounds[2]);
                maxY = Math.Max(maxY, bounds[3]);
            }
            bounds[0] = minX; bounds[1] = minY; bounds[2] = maxX; bounds[3] = maxY;
            return bounds;
        }

        // ---- constructors (top-level RoundedPolygon(...) functions in Kotlin) ----

        public static RoundedPolygon FromVertexCount(int numVertices, float radius = 1f, float centerX = 0f, float centerY = 0f,
            CornerRounding rounding = null, IList<CornerRounding> perVertexRounding = null) =>
            FromVertices(VerticesFromNumVerts(numVertices, radius, centerX, centerY), rounding, perVertexRounding, centerX, centerY);

        public static RoundedPolygon Copy(RoundedPolygon source) => new RoundedPolygon(source.Features, source.Center);

        public static RoundedPolygon FromVertices(float[] vertices, CornerRounding rounding = null,
            IList<CornerRounding> perVertexRounding = null, float centerX = float.Epsilon, float centerY = float.Epsilon)
        {
            rounding = rounding ?? CornerRounding.Unrounded;
            if (vertices.Length < 6) throw new ArgumentException("Polygons must have at least 3 vertices");
            if (vertices.Length % 2 == 1) throw new ArgumentException("The vertices array should have even size");
            if (perVertexRounding != null && perVertexRounding.Count * 2 != vertices.Length)
                throw new ArgumentException("perVertexRounding list should be either null or the same size as the number of vertices (vertices.size / 2)");
            var corners = new List<List<Cubic>>();
            int n = vertices.Length / 2;
            var roundedCorners = new List<RoundedCorner>();
            for (int i = 0; i < n; i++)
            {
                var vtxRounding = perVertexRounding?[i] ?? rounding;
                int prevIndex = ((i + n - 1) % n) * 2;
                int nextIndex = ((i + 1) % n) * 2;
                roundedCorners.Add(new RoundedCorner(
                    new Point(vertices[prevIndex], vertices[prevIndex + 1]),
                    new Point(vertices[i * 2], vertices[i * 2 + 1]),
                    new Point(vertices[nextIndex], vertices[nextIndex + 1]),
                    vtxRounding));
            }
            var cutAdjusts = new (float, float)[n];
            for (int ix = 0; ix < n; ix++)
            {
                float expectedRoundCut = roundedCorners[ix].ExpectedRoundCut + roundedCorners[(ix + 1) % n].ExpectedRoundCut;
                float expectedCut = roundedCorners[ix].ExpectedCut + roundedCorners[(ix + 1) % n].ExpectedCut;
                float vtxX = vertices[ix * 2], vtxY = vertices[ix * 2 + 1];
                float nextVtxX = vertices[((ix + 1) % n) * 2], nextVtxY = vertices[((ix + 1) % n) * 2 + 1];
                float sideSize = ShapeUtils.Distance(vtxX - nextVtxX, vtxY - nextVtxY);
                if (expectedRoundCut > sideSize) cutAdjusts[ix] = (sideSize / expectedRoundCut, 0f);
                else if (expectedCut > sideSize) cutAdjusts[ix] = (1f, (sideSize - expectedRoundCut) / (expectedCut - expectedRoundCut));
                else cutAdjusts[ix] = (1f, 1f);
            }
            for (int i = 0; i < n; i++)
            {
                var allowedCuts = new float[2];
                for (int delta = 0; delta <= 1; delta++)
                {
                    var (roundCutRatio, cutRatio) = cutAdjusts[(i + n - 1 + delta) % n];
                    allowedCuts[delta] = roundedCorners[i].ExpectedRoundCut * roundCutRatio +
                                         (roundedCorners[i].ExpectedCut - roundedCorners[i].ExpectedRoundCut) * cutRatio;
                }
                corners.Add(roundedCorners[i].GetCubics(allowedCuts[0], allowedCuts[1]));
            }
            var tempFeatures = new List<Feature>();
            for (int i = 0; i < n; i++)
            {
                int prevVtxIndex = (i + n - 1) % n, nextVtxIndex = (i + 1) % n;
                var currVertex = new Point(vertices[i * 2], vertices[i * 2 + 1]);
                var prevVertex = new Point(vertices[prevVtxIndex * 2], vertices[prevVtxIndex * 2 + 1]);
                var nextVertex = new Point(vertices[nextVtxIndex * 2], vertices[nextVtxIndex * 2 + 1]);
                bool convex = ShapeUtils.Convex(prevVertex, currVertex, nextVertex);
                tempFeatures.Add(new Feature.Corner(corners[i], convex));
                var last = corners[i][corners[i].Count - 1];
                var nextFirst = corners[(i + 1) % n][0];
                tempFeatures.Add(new Feature.Edge(new List<Cubic>
                {
                    Cubic.StraightLine(last.Anchor1X, last.Anchor1Y, nextFirst.Anchor0X, nextFirst.Anchor0Y),
                }));
            }
            Point c = centerX == float.Epsilon || centerY == float.Epsilon ? CalculateCenter(vertices) : new Point(centerX, centerY);
            return FromFeatures(tempFeatures, c.X, c.Y);
        }

        public static RoundedPolygon FromFeatures(IReadOnlyList<Feature> features, float centerX = float.NaN, float centerY = float.NaN)
        {
            if (features.Count < 2) throw new ArgumentException("Polygons must have at least 2 features");
            var vertices = new List<float>();
            foreach (var feature in features)
            foreach (var cubic in feature.Cubics)
            {
                vertices.Add(cubic.Anchor0X);
                vertices.Add(cubic.Anchor0Y);
            }
            var arr = vertices.ToArray();
            float cX = float.IsNaN(centerX) ? CalculateCenter(arr).X : centerX;
            float cY = float.IsNaN(centerY) ? CalculateCenter(arr).Y : centerY;
            return new RoundedPolygon(features, new Point(cX, cY));
        }

        internal static Point CalculateCenter(float[] vertices)
        {
            float cx = 0f, cy = 0f;
            for (int i = 0; i < vertices.Length;)
            {
                cx += vertices[i++];
                cy += vertices[i++];
            }
            return new Point(cx / (vertices.Length / 2), cy / (vertices.Length / 2));
        }

        static float[] VerticesFromNumVerts(int numVertices, float radius, float centerX, float centerY)
        {
            var result = new float[numVertices * 2];
            int k = 0;
            for (int i = 0; i < numVertices; i++)
            {
                var v = ShapeUtils.RadialToCartesian(radius, ShapeUtils.FloatPi / numVertices * 2 * i) + new Point(centerX, centerY);
                result[k++] = v.X;
                result[k++] = v.Y;
            }
            return result;
        }

        // ---- Shapes.kt ----

        public static RoundedPolygon Circle(int numVertices = 8, float radius = 1f, float centerX = 0f, float centerY = 0f)
        {
            if (numVertices < 3) throw new ArgumentException("Circle must have at least three vertices");
            float theta = ShapeUtils.FloatPi / numVertices;
            float polygonRadius = radius / ShapeUtils.Cos(theta);
            return FromVertexCount(numVertices, polygonRadius, centerX, centerY, new CornerRounding(radius));
        }

        public static RoundedPolygon Rectangle(float width = 2f, float height = 2f, CornerRounding rounding = null,
            IList<CornerRounding> perVertexRounding = null, float centerX = 0f, float centerY = 0f)
        {
            float left = centerX - width / 2, top = centerY - height / 2;
            float right = centerX + width / 2, bottom = centerY + height / 2;
            return FromVertices(new[] { right, bottom, left, bottom, left, top, right, top }, rounding, perVertexRounding, centerX, centerY);
        }

        public static RoundedPolygon Star(int numVerticesPerRadius, float radius = 1f, float innerRadius = .5f,
            CornerRounding rounding = null, CornerRounding innerRounding = null, IList<CornerRounding> perVertexRounding = null,
            float centerX = 0f, float centerY = 0f)
        {
            rounding = rounding ?? CornerRounding.Unrounded;
            if (radius <= 0f || innerRadius <= 0f) throw new ArgumentException("Star radii must both be greater than 0");
            if (innerRadius >= radius) throw new ArgumentException("innerRadius must be less than radius");
            var pv = perVertexRounding;
            if (pv == null && innerRounding != null)
            {
                var l = new List<CornerRounding>();
                for (int i = 0; i < numVerticesPerRadius; i++) { l.Add(rounding); l.Add(innerRounding); }
                pv = l;
            }
            return FromVertices(StarVerticesFromNumVerts(numVerticesPerRadius, radius, innerRadius, centerX, centerY), rounding, pv, centerX, centerY);
        }

        public static RoundedPolygon Pill(float width = 2f, float height = 1f, float smoothing = 0f, float centerX = 0f, float centerY = 0f)
        {
            if (!(width > 0f && height > 0f)) throw new ArgumentException("Pill shapes must have positive width and height");
            float wHalf = width / 2, hHalf = height / 2;
            return FromVertices(new[]
            {
                wHalf + centerX, hHalf + centerY, -wHalf + centerX, hHalf + centerY,
                -wHalf + centerX, -hHalf + centerY, wHalf + centerX, -hHalf + centerY,
            }, new CornerRounding(Math.Min(wHalf, hHalf), smoothing), null, centerX, centerY);
        }

        public static RoundedPolygon PillStar(float width = 2f, float height = 1f, int numVerticesPerRadius = 8,
            float innerRadiusRatio = .5f, CornerRounding rounding = null, CornerRounding innerRounding = null,
            IList<CornerRounding> perVertexRounding = null, float vertexSpacing = 0.5f, float startLocation = 0f,
            float centerX = 0f, float centerY = 0f)
        {
            rounding = rounding ?? CornerRounding.Unrounded;
            if (!(width > 0f && height > 0f)) throw new ArgumentException("Pill shapes must have positive width and height");
            if (!(innerRadiusRatio > 0 && innerRadiusRatio <= 1)) throw new ArgumentException("innerRadius must be between 0 and 1");
            var pv = perVertexRounding;
            if (pv == null && innerRounding != null)
            {
                var l = new List<CornerRounding>();
                for (int i = 0; i < numVerticesPerRadius; i++) { l.Add(rounding); l.Add(innerRounding); }
                pv = l;
            }
            return FromVertices(PillStarVerticesFromNumVerts(numVerticesPerRadius, width, height, innerRadiusRatio, vertexSpacing,
                startLocation, centerX, centerY), rounding, pv, centerX, centerY);
        }

        static float[] PillStarVerticesFromNumVerts(int numVerticesPerRadius, float width, float height, float innerRadius,
            float vertexSpacing, float startLocation, float centerX, float centerY)
        {
            float endcapRadius = Math.Min(width, height);
            float vSegLen = Math.Max(height - width, 0f);
            float hSegLen = Math.Max(width - height, 0f);
            float vSegHalf = vSegLen / 2, hSegHalf = hSegLen / 2;
            float circlePerimeter = ShapeUtils.TwoPi * endcapRadius * ShapeUtils.Interpolate(innerRadius, 1f, vertexSpacing);
            float perimeter = 2 * hSegLen + 2 * vSegLen + circlePerimeter;
            var sections = new float[11];
            sections[0] = 0f;
            sections[1] = vSegLen / 2;
            sections[2] = sections[1] + circlePerimeter / 4;
            sections[3] = sections[2] + hSegLen;
            sections[4] = sections[3] + circlePerimeter / 4;
            sections[5] = sections[4] + vSegLen;
            sections[6] = sections[5] + circlePerimeter / 4;
            sections[7] = sections[6] + hSegLen;
            sections[8] = sections[7] + circlePerimeter / 4;
            sections[9] = sections[8] + vSegLen / 2;
            sections[10] = perimeter;
            float tPerVertex = perimeter / (2 * numVerticesPerRadius);
            bool inner = false;
            int currSecIndex = 0;
            float secStart = 0f, secEnd = sections[1];
            float t = startLocation * perimeter;
            var result = new float[numVerticesPerRadius * 4];
            int k = 0;
            var rectBR = new Point(hSegHalf, vSegHalf);
            var rectBL = new Point(-hSegHalf, vSegHalf);
            var rectTL = new Point(-hSegHalf, -vSegHalf);
            var rectTR = new Point(hSegHalf, -vSegHalf);
            for (int i = 0; i < numVerticesPerRadius * 2; i++)
            {
                float boundedT = t % perimeter;
                if (boundedT < secStart) currSecIndex = 0;
                while (boundedT >= sections[(currSecIndex + 1) % sections.Length])
                {
                    currSecIndex = (currSecIndex + 1) % sections.Length;
                    secStart = sections[currSecIndex];
                    secEnd = sections[(currSecIndex + 1) % sections.Length];
                }
                float tInSection = boundedT - secStart;
                float tProportion = tInSection / (secEnd - secStart);
                float currRadius = inner ? endcapRadius * innerRadius : endcapRadius;
                Point vertex;
                switch (currSecIndex)
                {
                    case 0: vertex = new Point(currRadius, tProportion * vSegHalf); break;
                    case 1: vertex = ShapeUtils.RadialToCartesian(currRadius, tProportion * ShapeUtils.FloatPi / 2) + rectBR; break;
                    case 2: vertex = new Point(hSegHalf - tProportion * hSegLen, currRadius); break;
                    case 3: vertex = ShapeUtils.RadialToCartesian(currRadius, ShapeUtils.FloatPi / 2 + tProportion * ShapeUtils.FloatPi / 2) + rectBL; break;
                    case 4: vertex = new Point(-currRadius, vSegHalf - tProportion * vSegLen); break;
                    case 5: vertex = ShapeUtils.RadialToCartesian(currRadius, ShapeUtils.FloatPi + tProportion * ShapeUtils.FloatPi / 2) + rectTL; break;
                    case 6: vertex = new Point(-hSegHalf + tProportion * hSegLen, -currRadius); break;
                    case 7: vertex = ShapeUtils.RadialToCartesian(currRadius, ShapeUtils.FloatPi * 1.5f + tProportion * ShapeUtils.FloatPi / 2) + rectTR; break;
                    default: vertex = new Point(currRadius, -vSegHalf + tProportion * vSegHalf); break;
                }
                result[k++] = vertex.X + centerX;
                result[k++] = vertex.Y + centerY;
                t += tPerVertex;
                inner = !inner;
            }
            return result;
        }

        static float[] StarVerticesFromNumVerts(int numVerticesPerRadius, float radius, float innerRadius, float centerX, float centerY)
        {
            var result = new float[numVerticesPerRadius * 4];
            int k = 0;
            for (int i = 0; i < numVerticesPerRadius; i++)
            {
                var v = ShapeUtils.RadialToCartesian(radius, ShapeUtils.FloatPi / numVerticesPerRadius * 2 * i);
                result[k++] = v.X + centerX;
                result[k++] = v.Y + centerY;
                v = ShapeUtils.RadialToCartesian(innerRadius, ShapeUtils.FloatPi / numVerticesPerRadius * (2 * i + 1));
                result[k++] = v.X + centerX;
                result[k++] = v.Y + centerY;
            }
            return result;
        }

        sealed class RoundedCorner
        {
            readonly Point p0, p1, p2;
            readonly Point d1, d2;
            readonly float cornerRadius, smoothing, cosAngle, sinAngle;
            public readonly float ExpectedRoundCut;
            public float ExpectedCut => (1 + smoothing) * ExpectedRoundCut;
            public Point CenterPoint;

            public RoundedCorner(Point p0, Point p1, Point p2, CornerRounding rounding)
            {
                this.p0 = p0; this.p1 = p1; this.p2 = p2;
                var v01 = p0 - p1;
                var v21 = p2 - p1;
                float d01 = v01.GetDistance(), d21 = v21.GetDistance();
                if (d01 > 0f && d21 > 0f)
                {
                    d1 = v01 / d01;
                    d2 = v21 / d21;
                    cornerRadius = rounding?.Radius ?? 0f;
                    smoothing = rounding?.Smoothing ?? 0f;
                    cosAngle = d1.DotProduct(d2);
                    sinAngle = ShapeUtils.Sqrt(1 - ShapeUtils.Square(cosAngle));
                    ExpectedRoundCut = sinAngle > 1e-3 ? cornerRadius * (cosAngle + 1) / sinAngle : 0f;
                }
                else
                {
                    d1 = new Point(0f, 0f);
                    d2 = new Point(0f, 0f);
                }
            }

            public List<Cubic> GetCubics(float allowedCut0, float allowedCut1)
            {
                float allowedCut = Math.Min(allowedCut0, allowedCut1);
                if (ExpectedRoundCut < ShapeUtils.DistanceEpsilon || allowedCut < ShapeUtils.DistanceEpsilon ||
                    cornerRadius < ShapeUtils.DistanceEpsilon)
                {
                    CenterPoint = p1;
                    return new List<Cubic> { Cubic.StraightLine(p1.X, p1.Y, p1.X, p1.Y) };
                }
                float actualRoundCut = Math.Min(allowedCut, ExpectedRoundCut);
                float actualSmoothing0 = CalculateActualSmoothingValue(allowedCut0);
                float actualSmoothing1 = CalculateActualSmoothingValue(allowedCut1);
                float actualR = cornerRadius * actualRoundCut / ExpectedRoundCut;
                float centerDistance = ShapeUtils.Sqrt(ShapeUtils.Square(actualR) + ShapeUtils.Square(actualRoundCut));
                CenterPoint = p1 + ((d1 + d2) / 2f).GetDirection() * centerDistance;
                var circleIntersection0 = p1 + d1 * actualRoundCut;
                var circleIntersection2 = p1 + d2 * actualRoundCut;
                var flanking0 = ComputeFlankingCurve(actualRoundCut, actualSmoothing0, p1, p0, circleIntersection0, circleIntersection2, CenterPoint, actualR);
                var flanking2 = ComputeFlankingCurve(actualRoundCut, actualSmoothing1, p1, p2, circleIntersection2, circleIntersection0, CenterPoint, actualR).Reverse();
                return new List<Cubic>
                {
                    flanking0,
                    Cubic.CircularArc(CenterPoint.X, CenterPoint.Y, flanking0.Anchor1X, flanking0.Anchor1Y, flanking2.Anchor0X, flanking2.Anchor0Y),
                    flanking2,
                };
            }

            float CalculateActualSmoothingValue(float allowedCut)
            {
                if (allowedCut > ExpectedCut) return smoothing;
                if (allowedCut > ExpectedRoundCut) return smoothing * (allowedCut - ExpectedRoundCut) / (ExpectedCut - ExpectedRoundCut);
                return 0f;
            }

            static Cubic ComputeFlankingCurve(float actualRoundCut, float actualSmoothingValues, Point corner, Point sideStart,
                Point circleSegmentIntersection, Point otherCircleSegmentIntersection, Point circleCenter, float actualR)
            {
                var sideDirection = (sideStart - corner).GetDirection();
                var curveStart = corner + sideDirection * actualRoundCut * (1 + actualSmoothingValues);
                var p = ShapeUtils.Interpolate(circleSegmentIntersection, (circleSegmentIntersection + otherCircleSegmentIntersection) / 2f, actualSmoothingValues);
                var curveEnd = circleCenter + ShapeUtils.DirectionVector(p.X - circleCenter.X, p.Y - circleCenter.Y) * actualR;
                var circleTangent = (curveEnd - circleCenter).Rotate90();
                var anchorEnd = LineIntersection(sideStart, sideDirection, curveEnd, circleTangent) ?? circleSegmentIntersection;
                var anchorStart = (curveStart + anchorEnd * 2f) / 3f;
                return new Cubic(curveStart, anchorStart, anchorEnd, curveEnd);
            }

            static Point? LineIntersection(Point p0, Point d0, Point p1, Point d1)
            {
                var rotatedD1 = d1.Rotate90();
                float den = d0.DotProduct(rotatedD1);
                if (Math.Abs(den) < ShapeUtils.DistanceEpsilon) return null;
                float num = (p1 - p0).DotProduct(rotatedD1);
                if (Math.Abs(den) < ShapeUtils.DistanceEpsilon * Math.Abs(num)) return null;
                float k = num / den;
                return p0 + d0 * k;
            }
        }
    }

    // ---- PolygonMeasure.kt / FeatureMapping.kt / FloatMapping.kt ----

    internal readonly struct ProgressableFeature : IEquatable<ProgressableFeature>
    {
        public readonly float Progress;
        public readonly Feature Feature;
        public ProgressableFeature(float progress, Feature feature) { Progress = progress; Feature = feature; }
        public bool Equals(ProgressableFeature o) => Progress == o.Progress && ReferenceEquals(Feature, o.Feature);
        public override bool Equals(object obj) => obj is ProgressableFeature o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Progress, Feature);
    }

    internal sealed class LengthMeasurer
    {
        const int Segments = 3;

        public float MeasureCubic(Cubic c) => ClosestProgressTo(c, float.PositiveInfinity).Item2;
        public float FindCubicCutPoint(Cubic c, float m) => ClosestProgressTo(c, m).Item1;

        static (float, float) ClosestProgressTo(Cubic cubic, float threshold)
        {
            float total = 0f, remainder = threshold;
            var prev = new Point(cubic.Anchor0X, cubic.Anchor0Y);
            for (int i = 1; i <= Segments; i++)
            {
                float progress = (float)i / Segments;
                var point = cubic.PointOnCurve(progress);
                float segment = (point - prev).GetDistance();
                if (segment >= remainder) return (progress - (1.0f - remainder / segment) / Segments, threshold);
                remainder -= segment;
                total += segment;
                prev = point;
            }
            return (1.0f, total);
        }
    }

    internal sealed class MeasuredPolygon
    {
        readonly LengthMeasurer measurer;
        readonly List<MeasuredCubic> cubics;
        public readonly List<ProgressableFeature> Features;
        public int Count => cubics.Count;
        public MeasuredCubic this[int i] => cubics[i];
        public MeasuredCubic GetOrNull(int i) => i >= 0 && i < cubics.Count ? cubics[i] : null;

        MeasuredPolygon(LengthMeasurer measurer, List<ProgressableFeature> features, List<Cubic> cubicList, List<float> outlineProgress)
        {
            if (outlineProgress.Count != cubicList.Count + 1) throw new ArgumentException("Outline progress size is expected to be the cubics size + 1");
            if (outlineProgress[0] != 0f) throw new ArgumentException("First outline progress value is expected to be zero");
            if (outlineProgress[outlineProgress.Count - 1] != 1f) throw new ArgumentException("Last outline progress value is expected to be one");
            this.measurer = measurer;
            Features = features;
            var measured = new List<MeasuredCubic>();
            float startOutlineProgress = 0f;
            for (int index = 0; index < cubicList.Count; index++)
            {
                if (outlineProgress[index + 1] - outlineProgress[index] > ShapeUtils.DistanceEpsilon)
                {
                    measured.Add(new MeasuredCubic(this, cubicList[index], startOutlineProgress, outlineProgress[index + 1]));
                    startOutlineProgress = outlineProgress[index + 1];
                }
            }
            measured[measured.Count - 1].UpdateProgressRange(measured[measured.Count - 1].StartOutlineProgress, 1f);
            cubics = measured;
        }

        internal sealed class MeasuredCubic
        {
            readonly MeasuredPolygon owner;
            public readonly Cubic Cubic;
            public readonly float MeasuredSize;
            public float StartOutlineProgress { get; private set; }
            public float EndOutlineProgress { get; private set; }

            public MeasuredCubic(MeasuredPolygon owner, Cubic cubic, float start, float end)
            {
                if (end < start) throw new ArgumentException("endOutlineProgress is expected to be equal or greater than startOutlineProgress");
                this.owner = owner;
                Cubic = cubic;
                MeasuredSize = owner.measurer.MeasureCubic(cubic);
                StartOutlineProgress = start;
                EndOutlineProgress = end;
            }

            internal void UpdateProgressRange(float start, float end)
            {
                if (end < start) throw new ArgumentException("endOutlineProgress is expected to be equal or greater than startOutlineProgress");
                StartOutlineProgress = start;
                EndOutlineProgress = end;
            }

            public (MeasuredCubic, MeasuredCubic) CutAtProgress(float cutOutlineProgress)
            {
                float bounded = Math.Min(Math.Max(cutOutlineProgress, StartOutlineProgress), EndOutlineProgress);
                float size = EndOutlineProgress - StartOutlineProgress;
                float fromStart = bounded - StartOutlineProgress;
                float relativeProgress = fromStart / size;
                float t = owner.measurer.FindCubicCutPoint(Cubic, relativeProgress * MeasuredSize);
                if (!(t >= 0f && t <= 1f)) throw new ArgumentException("Cubic cut point is expected to be between 0 and 1");
                var (c1, c2) = Cubic.Split(t);
                return (new MeasuredCubic(owner, c1, StartOutlineProgress, bounded), new MeasuredCubic(owner, c2, bounded, EndOutlineProgress));
            }
        }

        public MeasuredPolygon CutAndShift(float cuttingPoint)
        {
            if (!(cuttingPoint >= 0f && cuttingPoint <= 1f)) throw new ArgumentException("Cutting point is expected to be between 0 and 1");
            if (cuttingPoint < ShapeUtils.DistanceEpsilon) return this;
            int targetIndex = cubics.FindIndex(c => cuttingPoint >= c.StartOutlineProgress && cuttingPoint <= c.EndOutlineProgress);
            var target = cubics[targetIndex];
            var (b1, b2) = target.CutAtProgress(cuttingPoint);
            var retCubics = new List<Cubic> { b2.Cubic };
            for (int i = 1; i < cubics.Count; i++) retCubics.Add(cubics[(i + targetIndex) % cubics.Count].Cubic);
            retCubics.Add(b1.Cubic);
            var retOutlineProgress = new List<float>(cubics.Count + 2);
            for (int index = 0; index < cubics.Count + 2; index++)
            {
                if (index == 0) retOutlineProgress.Add(0f);
                else if (index == cubics.Count + 1) retOutlineProgress.Add(1f);
                else
                {
                    int cubicIndex = (targetIndex + index - 1) % cubics.Count;
                    retOutlineProgress.Add(ShapeUtils.PositiveModulo(cubics[cubicIndex].EndOutlineProgress - cuttingPoint, 1f));
                }
            }
            var newFeatures = Features.Select(f => new ProgressableFeature(ShapeUtils.PositiveModulo(f.Progress - cuttingPoint, 1f), f.Feature)).ToList();
            return new MeasuredPolygon(measurer, newFeatures, retCubics, retOutlineProgress);
        }

        public static MeasuredPolygon MeasurePolygon(LengthMeasurer measurer, RoundedPolygon polygon)
        {
            var cubicList = new List<Cubic>();
            var featureToCubic = new List<(Feature, int)>();
            foreach (var feature in polygon.Features)
            {
                for (int ci = 0; ci < feature.Cubics.Count; ci++)
                {
                    if (feature is Feature.Corner && ci == feature.Cubics.Count / 2) featureToCubic.Add((feature, cubicList.Count));
                    cubicList.Add(feature.Cubics[ci]);
                }
            }
            var measures = new List<float> { 0f };
            float acc = 0f;
            foreach (var cubic in cubicList)
            {
                float m = measurer.MeasureCubic(cubic);
                if (m < 0f) throw new ArgumentException("Measured cubic is expected to be greater or equal to zero");
                acc += m;
                measures.Add(acc);
            }
            float totalMeasure = measures[measures.Count - 1];
            var outlineProgress = measures.Select(m => m / totalMeasure).ToList();
            var features = featureToCubic.Select(ft => new ProgressableFeature(
                ShapeUtils.PositiveModulo((outlineProgress[ft.Item2] + outlineProgress[ft.Item2 + 1]) / 2, 1f), ft.Item1)).ToList();
            return new MeasuredPolygon(measurer, features, cubicList, outlineProgress);
        }
    }

    internal sealed class DoubleMapper
    {
        readonly float[] sourceValues, targetValues;

        public DoubleMapper(IList<(float, float)> mappings)
        {
            sourceValues = mappings.Select(m => m.Item1).ToArray();
            targetValues = mappings.Select(m => m.Item2).ToArray();
            ValidateProgress(sourceValues);
            ValidateProgress(targetValues);
        }

        public float Map(float x) => LinearMap(sourceValues, targetValues, x);
        public float MapBack(float x) => LinearMap(targetValues, sourceValues, x);

        static float LinearMap(float[] xValues, float[] yValues, float x)
        {
            if (!(x >= 0f && x <= 1f)) throw new ArgumentException($"Invalid progress: {x}");
            int segmentStartIndex = -1;
            for (int i = 0; i < xValues.Length; i++)
            {
                if (ShapeUtils.ProgressInRange(x, xValues[i], xValues[(i + 1) % xValues.Length])) { segmentStartIndex = i; break; }
            }
            if (segmentStartIndex < 0) throw new InvalidOperationException("No segment");
            int segmentEndIndex = (segmentStartIndex + 1) % xValues.Length;
            float segmentSizeX = ShapeUtils.PositiveModulo(xValues[segmentEndIndex] - xValues[segmentStartIndex], 1f);
            float segmentSizeY = ShapeUtils.PositiveModulo(yValues[segmentEndIndex] - yValues[segmentStartIndex], 1f);
            float positionInSegment = segmentSizeX < 0.001f ? 0.5f : ShapeUtils.PositiveModulo(x - xValues[segmentStartIndex], 1f) / segmentSizeX;
            return ShapeUtils.PositiveModulo(yValues[segmentStartIndex] + segmentSizeY * positionInSegment, 1f);
        }

        static void ValidateProgress(float[] p)
        {
            float prev = p[p.Length - 1];
            int wraps = 0;
            foreach (float curr in p)
            {
                if (!(curr >= 0f && curr < 1f)) throw new ArgumentException("FloatMapping - Progress outside of range");
                if (!(ShapeUtils.ProgressDistance(curr, prev) > ShapeUtils.DistanceEpsilon)) throw new ArgumentException("FloatMapping - Progress repeats a value");
                if (curr < prev)
                {
                    wraps++;
                    if (wraps > 1) throw new ArgumentException("FloatMapping - Progress wraps more than once");
                }
                prev = curr;
            }
        }
    }

    internal static class FeatureMapping
    {
        static readonly List<(float, float)> IdentityMapping = new List<(float, float)> { (0f, 0f), (0.5f, 0.5f) };

        public static DoubleMapper FeatureMapper(List<ProgressableFeature> features1, List<ProgressableFeature> features2)
        {
            var f1 = features1.Where(f => f.Feature is Feature.Corner).ToList();
            var f2 = features2.Where(f => f.Feature is Feature.Corner).ToList();
            return new DoubleMapper(DoMapping(f1, f2));
        }

        static List<(float, float)> DoMapping(List<ProgressableFeature> features1, List<ProgressableFeature> features2)
        {
            var list = new List<(float d, ProgressableFeature a, ProgressableFeature b)>();
            foreach (var a in features1)
            foreach (var b in features2)
            {
                float d = FeatureDistSquared(a.Feature, b.Feature);
                if (d != float.MaxValue) list.Add((d, a, b));
            }
            // Kotlin sortedBy is stable; OrderBy is stable too.
            list = list.OrderBy(x => x.d).ToList();
            if (list.Count == 0) return IdentityMapping;
            if (list.Count == 1)
            {
                float p1 = list[0].a.Progress, p2 = list[0].b.Progress;
                return new List<(float, float)> { (p1, p2), ((p1 + 0.5f) % 1f, (p2 + 0.5f) % 1f) };
            }
            var helper = new MappingHelper();
            foreach (var x in list) helper.AddMapping(x.a, x.b);
            return helper.Mapping;
        }

        sealed class MappingHelper
        {
            public readonly List<(float, float)> Mapping = new List<(float, float)>();
            readonly HashSet<ProgressableFeature> usedF1 = new HashSet<ProgressableFeature>();
            readonly HashSet<ProgressableFeature> usedF2 = new HashSet<ProgressableFeature>();

            public void AddMapping(ProgressableFeature f1, ProgressableFeature f2)
            {
                if (usedF1.Contains(f1) || usedF2.Contains(f2)) return;
                int index = BinarySearch(f1.Progress);
                if (index >= 0) throw new ArgumentException("There can't be two features with the same progress");
                int insertionIndex = -index - 1;
                int n = Mapping.Count;
                if (n >= 1)
                {
                    var (before1, before2) = Mapping[(insertionIndex + n - 1) % n];
                    var (after1, after2) = Mapping[insertionIndex % n];
                    if (ShapeUtils.ProgressDistance(f1.Progress, before1) < ShapeUtils.DistanceEpsilon ||
                        ShapeUtils.ProgressDistance(f1.Progress, after1) < ShapeUtils.DistanceEpsilon ||
                        ShapeUtils.ProgressDistance(f2.Progress, before2) < ShapeUtils.DistanceEpsilon ||
                        ShapeUtils.ProgressDistance(f2.Progress, after2) < ShapeUtils.DistanceEpsilon)
                        return;
                    if (n > 1 && !ShapeUtils.ProgressInRange(f2.Progress, before2, after2)) return;
                }
                Mapping.Insert(insertionIndex, (f1.Progress, f2.Progress));
                usedF1.Add(f1);
                usedF2.Add(f2);
            }

            // Kotlin List.binarySearchBy semantics: index if found, else -(insertionPoint) - 1
            int BinarySearch(float key)
            {
                int low = 0, high = Mapping.Count - 1;
                while (low <= high)
                {
                    int mid = (low + high) >> 1;
                    int cmp = Mapping[mid].Item1.CompareTo(key);
                    if (cmp < 0) low = mid + 1;
                    else if (cmp > 0) high = mid - 1;
                    else return mid;
                }
                return -(low + 1);
            }
        }

        static float FeatureDistSquared(Feature f1, Feature f2)
        {
            if (f1 is Feature.Corner c1 && f2 is Feature.Corner c2 && c1.Convex != c2.Convex) return float.MaxValue;
            return (RepresentativePoint(f1) - RepresentativePoint(f2)).GetDistanceSquared();
        }

        static Point RepresentativePoint(Feature feature)
        {
            var first = feature.Cubics[0];
            var last = feature.Cubics[feature.Cubics.Count - 1];
            return new Point((first.Anchor0X + last.Anchor1X) / 2f, (first.Anchor0Y + last.Anchor1Y) / 2f);
        }
    }

    /// <summary>Morph between two RoundedPolygons (Morph.kt).</summary>
    public sealed class Morph
    {
        readonly RoundedPolygon start, end;
        readonly List<(Cubic, Cubic)> morphMatch;

        public Morph(RoundedPolygon start, RoundedPolygon end)
        {
            this.start = start;
            this.end = end;
            morphMatch = Match(start, end);
        }

        public float[] CalculateBounds(float[] bounds = null, bool approximate = true)
        {
            bounds = bounds ?? new float[4];
            start.CalculateBounds(bounds, approximate);
            float minX = bounds[0], minY = bounds[1], maxX = bounds[2], maxY = bounds[3];
            end.CalculateBounds(bounds, approximate);
            bounds[0] = Math.Min(minX, bounds[0]); bounds[1] = Math.Min(minY, bounds[1]);
            bounds[2] = Math.Max(maxX, bounds[2]); bounds[3] = Math.Max(maxY, bounds[3]);
            return bounds;
        }

        public List<Cubic> AsCubics(float progress)
        {
            var result = new List<Cubic>();
            Cubic firstCubic = null, lastCubic = null;
            foreach (var (a, b) in morphMatch)
            {
                var p = new float[8];
                for (int i = 0; i < 8; i++) p[i] = ShapeUtils.Interpolate(a.points[i], b.points[i], progress);
                var cubic = new Cubic(p);
                if (firstCubic == null) firstCubic = cubic;
                if (lastCubic != null) result.Add(lastCubic);
                lastCubic = cubic;
            }
            if (lastCubic != null && firstCubic != null)
                result.Add(new Cubic(lastCubic.Anchor0X, lastCubic.Anchor0Y, lastCubic.Control0X, lastCubic.Control0Y,
                    lastCubic.Control1X, lastCubic.Control1Y, firstCubic.Anchor0X, firstCubic.Anchor0Y));
            return result;
        }

        static List<(Cubic, Cubic)> Match(RoundedPolygon p1, RoundedPolygon p2)
        {
            var measurer = new LengthMeasurer();
            var measured1 = MeasuredPolygon.MeasurePolygon(measurer, p1);
            var measured2 = MeasuredPolygon.MeasurePolygon(measurer, p2);
            var doubleMapper = FeatureMapping.FeatureMapper(measured1.Features, measured2.Features);
            float polygon2CutPoint = doubleMapper.Map(0f);
            var bs1 = measured1;
            var bs2 = measured2.CutAndShift(polygon2CutPoint);
            var ret = new List<(Cubic, Cubic)>();
            int i1 = 0, i2 = 0;
            var b1 = bs1.GetOrNull(i1++);
            var b2 = bs2.GetOrNull(i2++);
            while (b1 != null && b2 != null)
            {
                float b1a = i1 == bs1.Count ? 1f : b1.EndOutlineProgress;
                float b2a = i2 == bs2.Count ? 1f : doubleMapper.MapBack(ShapeUtils.PositiveModulo(b2.EndOutlineProgress + polygon2CutPoint, 1f));
                float minb = Math.Min(b1a, b2a);
                MeasuredPolygon.MeasuredCubic seg1, newb1, seg2, newb2;
                if (b1a > minb + ShapeUtils.AngleEpsilon) (seg1, newb1) = b1.CutAtProgress(minb);
                else { seg1 = b1; newb1 = bs1.GetOrNull(i1++); }
                if (b2a > minb + ShapeUtils.AngleEpsilon)
                    (seg2, newb2) = b2.CutAtProgress(ShapeUtils.PositiveModulo(doubleMapper.Map(minb) - polygon2CutPoint, 1f));
                else { seg2 = b2; newb2 = bs2.GetOrNull(i2++); }
                ret.Add((seg1.Cubic, seg2.Cubic));
                b1 = newb1;
                b2 = newb2;
            }
            if (b1 != null || b2 != null) throw new InvalidOperationException("Expected both Polygon's Cubic to be fully matched");
            return ret;
        }
    }
}
