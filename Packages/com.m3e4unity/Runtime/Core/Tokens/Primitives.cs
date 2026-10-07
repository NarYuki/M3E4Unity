// Value types the generated token tables refer to, ported from Compose:
// RoundedCornerShape / CircleShape, CubicBezierEasing and spring() physics.
using System;

namespace M3E4Unity.Tokens
{
    /// <summary>
    /// A rounded-corner shape in dp. Corner order follows Compose: topStart, topEnd,
    /// bottomEnd, bottomStart. <see cref="IsFull"/> means 50% of the shorter side (CircleShape).
    /// </summary>
    [Serializable]
    public struct CornerShape : IEquatable<CornerShape>
    {
        public float TopStart, TopEnd, BottomEnd, BottomStart;
        public bool IsFull;

        public CornerShape(float all) : this(all, all, all, all) { }

        public CornerShape(float topStart, float topEnd, float bottomEnd, float bottomStart)
        {
            TopStart = topStart;
            TopEnd = topEnd;
            BottomEnd = bottomEnd;
            BottomStart = bottomStart;
            IsFull = false;
        }

        public static readonly CornerShape None = new CornerShape(0f);
        public static readonly CornerShape Full = new CornerShape(0f) { IsFull = true };

        /// <summary>Per-corner radii in dp for a box of the given size (resolves "full").</summary>
        public void Resolve(float width, float height, out float ts, out float te, out float be, out float bs)
        {
            if (IsFull)
            {
                float r = Math.Min(width, height) * 0.5f;
                ts = te = be = bs = r;
                return;
            }
            // Compose clamps each corner so that opposite corners never overlap.
            float limit = Math.Min(width, height) * 0.5f;
            ts = Math.Min(TopStart, limit);
            te = Math.Min(TopEnd, limit);
            be = Math.Min(BottomEnd, limit);
            bs = Math.Min(BottomStart, limit);
        }

        public CornerShape WithTop(float r) => new CornerShape(r, r, BottomEnd, BottomStart);
        public CornerShape WithBottom(float r) => new CornerShape(TopStart, TopEnd, r, r);
        public CornerShape WithStart(float r) => new CornerShape(r, TopEnd, BottomEnd, r);
        public CornerShape WithEnd(float r) => new CornerShape(TopStart, r, r, BottomStart);

        public bool Equals(CornerShape o) =>
            IsFull == o.IsFull && TopStart == o.TopStart && TopEnd == o.TopEnd && BottomEnd == o.BottomEnd && BottomStart == o.BottomStart;

        public override bool Equals(object obj) => obj is CornerShape o && Equals(o);
        public override int GetHashCode() => IsFull ? -1 : HashCode.Combine(TopStart, TopEnd, BottomEnd, BottomStart);
        public override string ToString() => IsFull ? "Full" : $"({TopStart}, {TopEnd}, {BottomEnd}, {BottomStart})";
    }

    /// <summary>Compose CubicBezierEasing: control points (a, b) and (c, d).</summary>
    [Serializable]
    public struct CubicBezier
    {
        public float A, B, C, D;

        public CubicBezier(float a, float b, float c, float d)
        {
            A = a; B = b; C = c; D = d;
        }

        static double Evaluate(double p1, double p2, double t)
        {
            // cubic with P0 = 0, P3 = 1
            double u = 1 - t;
            return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
        }

        static double Derivative(double p1, double p2, double t)
        {
            double u = 1 - t;
            return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
        }

        /// <summary>Maps a linear fraction in [0, 1] to the eased fraction.</summary>
        public float Transform(float fraction)
        {
            if (fraction <= 0f || fraction >= 1f) return fraction;
            // Solve x(t) = fraction: Newton with bisection fallback.
            double lo = 0, hi = 1, t = fraction;
            for (int i = 0; i < 32; i++)
            {
                double x = Evaluate(A, C, t) - fraction;
                if (Math.Abs(x) < 1e-7) break;
                if (x > 0) hi = t; else lo = t;
                double dx = Derivative(A, C, t);
                double next = dx > 1e-9 ? t - x / dx : (lo + hi) * 0.5;
                t = next <= lo || next >= hi ? (lo + hi) * 0.5 : next;
            }
            return (float)Evaluate(B, D, t);
        }
    }

    /// <summary>
    /// Compose spring() with unit mass: closed-form damped harmonic oscillator,
    /// identical to androidx.compose.animation.core.SpringSimulation.
    /// </summary>
    [Serializable]
    public struct Spring
    {
        public float DampingRatio;
        public float Stiffness;

        public Spring(float dampingRatio, float stiffness)
        {
            DampingRatio = dampingRatio;
            Stiffness = stiffness;
        }

        /// <summary>
        /// Displacement and velocity at time t (seconds) for a spring released from
        /// displacement x0 (value - target) with velocity v0.
        /// </summary>
        public void Evaluate(double x0, double v0, double t, out double x, out double v)
        {
            double w = Math.Sqrt(Stiffness);
            double z = DampingRatio;
            if (z > 1)
            {
                double s = w * Math.Sqrt(z * z - 1);
                double gp = -z * w + s, gm = -z * w - s;
                double cb = (gm * x0 - v0) / (gm - gp);
                double ca = x0 - cb;
                double ea = Math.Exp(gm * t), eb = Math.Exp(gp * t);
                x = ca * ea + cb * eb;
                v = ca * gm * ea + cb * gp * eb;
            }
            else if (z == 1)
            {
                double ca = x0, cb = v0 + w * x0;
                double e = Math.Exp(-w * t);
                x = (ca + cb * t) * e;
                v = (cb - w * (ca + cb * t)) * e;
            }
            else
            {
                double wd = w * Math.Sqrt(1 - z * z);
                double cc = x0, sc = (z * w * x0 + v0) / wd;
                double e = Math.Exp(-z * w * t);
                double cos = Math.Cos(wd * t), sin = Math.Sin(wd * t);
                x = e * (cc * cos + sc * sin);
                v = -z * w * x + e * (-cc * wd * sin + sc * wd * cos);
            }
        }

        /// <summary>Progress of a 0 -> 1 animation at time t (may overshoot for damping &lt; 1).</summary>
        public float Progress(double t)
        {
            Evaluate(-1, 0, t, out double x, out _);
            return (float)(1 + x);
        }

        /// <summary>
        /// Duration of an animation with this spring, exactly as Compose's FloatSpringSpec computes
        /// it: estimateAnimationDurationMillis(stiffness, damping, v0 / threshold, (from - to) / threshold, 1).
        /// The default threshold 0.01 is Spring.DefaultDisplacementThreshold.
        /// </summary>
        public long DurationMillis(double from, double to, double initialVelocity = 0, double visibilityThreshold = 0.01) =>
            SpringEstimation.EstimateAnimationDurationMillis(Stiffness, DampingRatio,
                initialVelocity / visibilityThreshold, (from - to) / visibilityThreshold, 1.0);
    }

    /// <summary>Port of androidx.compose.animation.core.SpringEstimation (Apache 2.0).</summary>
    public static class SpringEstimation
    {
        public static long EstimateAnimationDurationMillis(double stiffness, double dampingRatio,
            double initialVelocity, double initialDisplacement, double delta)
        {
            if (dampingRatio == 0) return long.MaxValue;
            double dampingCoefficient = 2.0 * dampingRatio * Math.Sqrt(stiffness);
            double partialRoot = dampingCoefficient * dampingCoefficient - 4.0 * stiffness;
            double partialRootReal = partialRoot < 0.0 ? 0.0 : Math.Sqrt(partialRoot);
            double partialRootImaginary = partialRoot < 0.0 ? Math.Sqrt(Math.Abs(partialRoot)) : 0.0;
            double firstRootReal = (-dampingCoefficient + partialRootReal) * 0.5;
            double firstRootImaginary = partialRootImaginary * 0.5;
            double secondRootReal = (-dampingCoefficient - partialRootReal) * 0.5;
            return EstimateDurationInternal(firstRootReal, firstRootImaginary, secondRootReal, dampingRatio,
                initialVelocity, initialDisplacement, delta);
        }

        static double EstimateUnderDamped(double firstRootReal, double firstRootImaginary, double p0, double v0, double delta)
        {
            double r = firstRootReal;
            double c1 = p0;
            double c2 = (v0 - r * c1) / firstRootImaginary;
            double c = Math.Sqrt(c1 * c1 + c2 * c2);
            return Math.Log(delta / c) / r;
        }

        static bool NotFinite(double x) => double.IsNaN(x) || double.IsInfinity(x);

        static double EstimateCriticallyDamped(double firstRootReal, double p0, double v0, double delta)
        {
            double r = firstRootReal;
            double c1 = p0;
            double c2 = v0 - r * c1;
            double t1 = Math.Log(Math.Abs(delta / c1)) / r;
            double guess = Math.Log(Math.Abs(delta / c2));
            double tt = guess;
            for (int i = 0; i <= 5; i++) tt = guess - Math.Log(Math.Abs(tt / r));
            double t2 = tt / r;
            double tCurr = NotFinite(t1) ? t2 : NotFinite(t2) ? t1 : Math.Max(t1, t2);
            double tInflection = -(r * c1 + c2) / (r * c2);
            double xInflection = c1 * Math.Exp(r * tInflection) + c2 * tInflection * Math.Exp(r * tInflection);
            double signedDelta;
            if (double.IsNaN(tInflection) || tInflection <= 0.0) signedDelta = -delta;
            else if (tInflection > 0.0 && -xInflection < delta)
            {
                if (c2 < 0 && c1 > 0) tCurr = 0.0;
                signedDelta = -delta;
            }
            else
            {
                tCurr = -(2.0 / r) - (c1 / c2);
                signedDelta = delta;
            }
            double tDelta = double.MaxValue;
            int iterations = 0;
            while (tDelta > 0.001 && iterations < 100)
            {
                iterations++;
                double tLast = tCurr;
                double f = (c1 + c2 * tCurr) * Math.Exp(r * tCurr) + signedDelta;
                double fp = (c2 * (r * tCurr + 1) + c1 * r) * Math.Exp(r * tCurr);
                tCurr = tCurr - f / fp;
                tDelta = Math.Abs(tLast - tCurr);
            }
            return tCurr;
        }

        static double EstimateOverDamped(double r1, double r2, double p0, double v0, double delta)
        {
            double c2 = (r1 * p0 - v0) / (r1 - r2);
            double c1 = p0 - c2;
            double t1 = Math.Log(Math.Abs(delta / c1)) / r1;
            double t2 = Math.Log(Math.Abs(delta / c2)) / r2;
            double tCurr = NotFinite(t1) ? t2 : NotFinite(t2) ? t1 : Math.Max(t1, t2);
            double tInflection = Math.Log((c1 * r1) / (-c2 * r2)) / (r2 - r1);
            double XInflection() => c1 * Math.Exp(r1 * tInflection) + c2 * Math.Exp(r2 * tInflection);
            double signedDelta;
            if (double.IsNaN(tInflection) || tInflection <= 0.0) signedDelta = -delta;
            else if (tInflection > 0.0 && -XInflection() < delta)
            {
                if (c2 > 0.0 && c1 < 0.0) tCurr = 0.0;
                signedDelta = -delta;
            }
            else
            {
                tCurr = Math.Log(-(c2 * r2 * r2) / (c1 * r1 * r1)) / (r1 - r2);
                signedDelta = delta;
            }
            if (Math.Abs(c1 * r1 * Math.Exp(r1 * tCurr) + c2 * r2 * Math.Exp(r2 * tCurr)) < 0.0001) return tCurr;
            double tDelta = double.MaxValue;
            int iterations = 0;
            while (tDelta > 0.001 && iterations < 100)
            {
                iterations++;
                double tLast = tCurr;
                double f = c1 * Math.Exp(r1 * tCurr) + c2 * Math.Exp(r2 * tCurr) + signedDelta;
                double fp = c1 * r1 * Math.Exp(r1 * tCurr) + c2 * r2 * Math.Exp(r2 * tCurr);
                tCurr = tCurr - f / fp;
                tDelta = Math.Abs(tLast - tCurr);
            }
            return tCurr;
        }

        static long EstimateDurationInternal(double firstRootReal, double firstRootImaginary, double secondRootReal,
            double dampingRatio, double initialVelocity, double initialPosition, double delta)
        {
            if (initialPosition == 0.0 && initialVelocity == 0.0) return 0L;
            double v0 = initialPosition < 0 ? -initialVelocity : initialVelocity;
            double p0 = Math.Abs(initialPosition);
            double seconds = dampingRatio > 1.0 ? EstimateOverDamped(firstRootReal, secondRootReal, p0, v0, delta)
                : dampingRatio < 1.0 ? EstimateUnderDamped(firstRootReal, firstRootImaginary, p0, v0, delta)
                : EstimateCriticallyDamped(firstRootReal, p0, v0, delta);
            return (long)(seconds * 1000.0);
        }
    }

    /// <summary>A type scale entry (TypeScaleTokens).</summary>
    [Serializable]
    public struct TypeStyle
    {
        public FontRole Font;
        public int Weight;
        public float Size;        // sp
        public float LineHeight;  // sp
        public float Tracking;    // sp

        public TypeStyle(FontRole font, int weight, float size, float lineHeight, float tracking)
        {
            Font = font; Weight = weight; Size = size; LineHeight = lineHeight; Tracking = tracking;
        }

        /// <summary>Letter spacing in TextMesh Pro units (1/100 em).</summary>
        public float TmpCharacterSpacing => Size > 0 ? Tracking / Size * 100f : 0f;
    }
}
