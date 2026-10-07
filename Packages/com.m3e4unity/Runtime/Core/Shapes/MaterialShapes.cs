// C# port of androidx.compose.material3.MaterialShapes (the 35 M3 Expressive shapes).
// Copyright 2024-2025 The Android Open Source Project, Apache License 2.0.
using System;
using System.Collections.Generic;
using System.Linq;

namespace M3E4Unity.Shapes
{
    public enum MaterialShape
    {
        Circle, Square, Slanted, Arch, Fan, Arrow, SemiCircle, Oval, Pill, Triangle, Diamond,
        ClamShell, Pentagon, Gem, Sunny, VerySunny, Cookie4Sided, Cookie6Sided, Cookie7Sided,
        Cookie9Sided, Cookie12Sided, Ghostish, Clover4Leaf, Clover8Leaf, Burst, SoftBurst, Boom,
        SoftBoom, Flower, Puffy, PuffyDiamond, PixelCircle, PixelTriangle, Bun, Heart,
    }

    public static class MaterialShapes
    {
        static readonly CornerRounding CornerRound15 = new CornerRounding(.15f);
        static readonly CornerRounding CornerRound20 = new CornerRounding(.2f);
        static readonly CornerRounding CornerRound30 = new CornerRounding(.3f);
        static readonly CornerRounding CornerRound50 = new CornerRounding(.5f);
        static readonly CornerRounding CornerRound100 = new CornerRounding(1f);

        static readonly Dictionary<MaterialShape, RoundedPolygon> Cache = new Dictionary<MaterialShape, RoundedPolygon>();

        /// <summary>The shape, normalized to the unit square like the Compose getters.</summary>
        public static RoundedPolygon Get(MaterialShape shape)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(shape, out var p))
                {
                    p = Build(shape).Normalized();
                    Cache[shape] = p;
                }
                return p;
            }
        }

        static RoundedPolygon Build(MaterialShape shape)
        {
            switch (shape)
            {
                case MaterialShape.Circle: return RoundedPolygon.Circle(10);
                case MaterialShape.Square: return RoundedPolygon.Rectangle(1f, 1f, CornerRound30);
                case MaterialShape.Slanted:
                    return CustomPolygon(new[] { P(0.926f, 0.970f, 0.189f, 0.811f), P(-0.021f, 0.967f, 0.187f, 0.057f) }, 2);
                case MaterialShape.Arch:
                    return Rotate(RoundedPolygon.FromVertexCount(4, perVertexRounding: new[] { CornerRound100, CornerRound100, CornerRound20, CornerRound20 }), -135f);
                case MaterialShape.Fan:
                    return CustomPolygon(new[] { P(1.004f, 1.000f, 0.148f, 0.417f), P(0.000f, 1.000f, 0.151f), P(0.000f, -0.003f, 0.148f), P(0.978f, 0.020f, 0.803f) }, 1);
                case MaterialShape.Arrow:
                    return CustomPolygon(new[] { P(0.500f, 0.892f, 0.313f), P(-0.216f, 1.050f, 0.207f), P(0.499f, -0.160f, 0.215f, 1.000f), P(1.225f, 1.060f, 0.211f) }, 1);
                case MaterialShape.SemiCircle:
                    return RoundedPolygon.Rectangle(1.6f, 1f, perVertexRounding: new[] { CornerRound20, CornerRound20, CornerRound100, CornerRound100 });
                case MaterialShape.Oval:
                    return Rotate(RoundedPolygon.Circle().Transformed((x, y) => new Point(x * 1f, y * 0.64f)), -45f);
                case MaterialShape.Pill:
                    return CustomPolygon(new[] { P(0.961f, 0.039f, 0.426f), P(1.001f, 0.428f), P(1.000f, 0.609f, 1.000f) }, 2, mirroring: true);
                case MaterialShape.Triangle:
                    return Rotate(RoundedPolygon.FromVertexCount(3, rounding: CornerRound20), -90f);
                case MaterialShape.Diamond:
                    return CustomPolygon(new[] { P(0.500f, 1.096f, 0.151f, 0.524f), P(0.040f, 0.500f, 0.159f) }, 2);
                case MaterialShape.ClamShell:
                    return CustomPolygon(new[] { P(0.171f, 0.841f, 0.159f), P(-0.020f, 0.500f, 0.140f), P(0.170f, 0.159f, 0.159f) }, 2);
                case MaterialShape.Pentagon:
                    return CustomPolygon(new[] { P(0.500f, -0.009f, 0.172f), P(1.030f, 0.365f, 0.164f), P(0.828f, 0.970f, 0.169f) }, 1, mirroring: true);
                case MaterialShape.Gem:
                    return CustomPolygon(new[] { P(0.499f, 1.023f, 0.241f, 0.778f), P(-0.005f, 0.792f, 0.208f), P(0.073f, 0.258f, 0.228f), P(0.433f, -0.000f, 0.491f) }, 1, mirroring: true);
                case MaterialShape.Sunny:
                    return RoundedPolygon.Star(8, innerRadius: .8f, rounding: CornerRound15);
                case MaterialShape.VerySunny:
                    return CustomPolygon(new[] { P(0.500f, 1.080f, 0.085f), P(0.358f, 0.843f, 0.085f) }, 8);
                case MaterialShape.Cookie4Sided:
                    return CustomPolygon(new[] { P(1.237f, 1.236f, 0.258f), P(0.500f, 0.918f, 0.233f) }, 4);
                case MaterialShape.Cookie6Sided:
                    return CustomPolygon(new[] { P(0.723f, 0.884f, 0.394f), P(0.500f, 1.099f, 0.398f) }, 6);
                case MaterialShape.Cookie7Sided:
                    return Rotate(RoundedPolygon.Star(7, innerRadius: .75f, rounding: CornerRound50), -90f);
                case MaterialShape.Cookie9Sided:
                    return Rotate(RoundedPolygon.Star(9, innerRadius: .8f, rounding: CornerRound50), -90f);
                case MaterialShape.Cookie12Sided:
                    return Rotate(RoundedPolygon.Star(12, innerRadius: .8f, rounding: CornerRound50), -90f);
                case MaterialShape.Ghostish:
                    return CustomPolygon(new[] { P(0.500f, 0f, 1.000f), P(1f, 0f, 1.000f), P(1f, 1.140f, 0.254f, 0.106f), P(0.575f, 0.906f, 0.253f) }, 1, mirroring: true);
                case MaterialShape.Clover4Leaf:
                    return CustomPolygon(new[] { P(0.500f, 0.074f), P(0.725f, -0.099f, 0.476f) }, 4, mirroring: true);
                case MaterialShape.Clover8Leaf:
                    return CustomPolygon(new[] { P(0.500f, 0.036f), P(0.758f, -0.101f, 0.209f) }, 8);
                case MaterialShape.Burst:
                    return CustomPolygon(new[] { P(0.500f, -0.006f, 0.006f), P(0.592f, 0.158f, 0.006f) }, 12);
                case MaterialShape.SoftBurst:
                    return CustomPolygon(new[] { P(0.193f, 0.277f, 0.053f), P(0.176f, 0.055f, 0.053f) }, 10);
                case MaterialShape.Boom:
                    return CustomPolygon(new[] { P(0.457f, 0.296f, 0.007f), P(0.500f, -0.051f, 0.007f) }, 15);
                case MaterialShape.SoftBoom:
                    return CustomPolygon(new[] { P(0.733f, 0.454f), P(0.839f, 0.437f, 0.532f), P(0.949f, 0.449f, 0.439f, 1.000f), P(0.998f, 0.478f, 0.174f) }, 16, mirroring: true);
                case MaterialShape.Flower:
                    return CustomPolygon(new[] { P(0.370f, 0.187f), P(0.416f, 0.049f, 0.381f), P(0.479f, 0.001f, 0.095f) }, 8, mirroring: true);
                case MaterialShape.Puffy:
                    return CustomPolygon(new[]
                    {
                        P(0.500f, 0.053f), P(0.545f, -0.040f, 0.405f), P(0.670f, -0.035f, 0.426f), P(0.717f, 0.066f, 0.574f),
                        P(0.722f, 0.128f), P(0.777f, 0.002f, 0.360f), P(0.914f, 0.149f, 0.660f), P(0.926f, 0.289f, 0.660f),
                        P(0.881f, 0.346f), P(0.940f, 0.344f, 0.126f), P(1.003f, 0.437f, 0.255f),
                    }, 2, mirroring: true).Transformed((x, y) => new Point(x * 1f, y * 0.742f));
                case MaterialShape.PuffyDiamond:
                    return CustomPolygon(new[] { P(0.870f, 0.130f, 0.146f), P(0.818f, 0.357f), P(1.000f, 0.332f, 0.853f) }, 4, mirroring: true);
                case MaterialShape.PixelCircle:
                    return CustomPolygon(new[]
                    {
                        P(0.500f, 0.000f), P(0.704f, 0.000f), P(0.704f, 0.065f), P(0.843f, 0.065f),
                        P(0.843f, 0.148f), P(0.926f, 0.148f), P(0.926f, 0.296f), P(1.000f, 0.296f),
                    }, 2, mirroring: true);
                case MaterialShape.PixelTriangle:
                    return CustomPolygon(new[]
                    {
                        P(0.110f, 0.500f), P(0.113f, 0.000f), P(0.287f, 0.000f), P(0.287f, 0.087f), P(0.421f, 0.087f),
                        P(0.421f, 0.170f), P(0.560f, 0.170f), P(0.560f, 0.265f), P(0.674f, 0.265f), P(0.675f, 0.344f),
                        P(0.789f, 0.344f), P(0.789f, 0.439f), P(0.888f, 0.439f),
                    }, 1, mirroring: true);
                case MaterialShape.Bun:
                    return CustomPolygon(new[] { P(0.796f, 0.500f), P(0.853f, 0.518f, 1f), P(0.992f, 0.631f, 1f), P(0.968f, 1.000f, 1f) }, 2, mirroring: true);
                case MaterialShape.Heart:
                    return CustomPolygon(new[] { P(0.500f, 0.268f, 0.016f), P(0.792f, -0.066f, 0.958f), P(1.064f, 0.276f, 1.000f), P(0.501f, 0.946f, 0.129f) }, 1, mirroring: true);
            }
            throw new ArgumentOutOfRangeException(nameof(shape));
        }

        readonly struct PointNRound
        {
            public readonly Point O;
            public readonly CornerRounding R;
            public PointNRound(Point o, CornerRounding r) { O = o; R = r ?? CornerRounding.Unrounded; }
        }

        static PointNRound P(float x, float y) => new PointNRound(new Point(x, y), CornerRounding.Unrounded);
        static PointNRound P(float x, float y, float radius, float smoothing = 0f) => new PointNRound(new Point(x, y), new CornerRounding(radius, smoothing));

        /// <summary>Compose Matrix().rotateZ(degrees) applied with Matrix.map.</summary>
        static RoundedPolygon Rotate(RoundedPolygon p, float degrees)
        {
            double r = degrees * (Math.PI / 180.0);
            float s = (float)Math.Sin(r), c = (float)Math.Cos(r);
            return p.Transformed((x, y) => new Point(c * x + -s * y, s * x + c * y));
        }

        static float ToRadians(float deg) => deg / 360f * 2 * (float)Math.PI;
        static float AngleDegrees(Point p) => (float)Math.Atan2(p.Y, p.X) * 180f / (float)Math.PI;

        static Point RotateDegrees(Point p, float angle, Point center)
        {
            float a = ToRadians(angle);
            var off = p - center;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Point(off.X * c - off.Y * s, off.X * s + off.Y * c) + center;
        }

        static List<PointNRound> DoRepeat(PointNRound[] points, int reps, Point center, bool mirroring)
        {
            var result = new List<PointNRound>();
            if (mirroring)
            {
                var angles = points.Select(pt => AngleDegrees(pt.O - center)).ToArray();
                var distances = points.Select(pt => (pt.O - center).GetDistance()).ToArray();
                int actualReps = reps * 2;
                float sectionAngle = 360f / actualReps;
                for (int it = 0; it < actualReps; it++)
                {
                    for (int index = 0; index < points.Length; index++)
                    {
                        int i = it % 2 == 0 ? index : points.Length - 1 - index;
                        if (i > 0 || it % 2 == 0)
                        {
                            float a = ToRadians(sectionAngle * it + (it % 2 == 0 ? angles[i] : sectionAngle - angles[i] + 2 * angles[0]));
                            var finalPoint = new Point((float)Math.Cos(a), (float)Math.Sin(a)) * distances[i] + center;
                            result.Add(new PointNRound(finalPoint, points[i].R));
                        }
                    }
                }
            }
            else
            {
                int np = points.Length;
                for (int it = 0; it < np * reps; it++)
                {
                    var point = RotateDegrees(points[it % np].O, (it / np) * 360f / reps, center);
                    result.Add(new PointNRound(point, points[it % np].R));
                }
            }
            return result;
        }

        static RoundedPolygon CustomPolygon(PointNRound[] pnr, int reps, bool mirroring = false)
        {
            var center = new Point(0.5f, 0.5f);
            var actual = DoRepeat(pnr, reps, center, mirroring);
            var vertices = new float[actual.Count * 2];
            for (int ix = 0; ix < vertices.Length; ix++) vertices[ix] = ix % 2 == 0 ? actual[ix / 2].O.X : actual[ix / 2].O.Y;
            return RoundedPolygon.FromVertices(vertices, null, actual.Select(p => p.R).ToList(), center.X, center.Y);
        }
    }
}
