using System;
using System.Globalization;
using System.Text;
using M3E4Unity.Tokens;

namespace M3E4Unity.Editor
{
    /// <summary>Shape kinds understood by M3Shape.shader (P(0,0).x).</summary>
    public enum ShapeKind
    {
        Fill = 0,
        Stroke = 1,
        Shadow = 2,
        SdfTile = 3,
        LoadingIndeterminate = 4,
        Ripple = 5,
        WavyLine = 6,
        Arc = 7,
        CheckMark = 8,
        Ellipse = 9,
        LinearProgress = 10,
        CircularProgress = 11,
    }

    /// <summary>
    /// The 24 parameters of one atlas cell (see M3Shape.shader for the layout).
    /// Radii use dp; a negative radius means "full" (half the shorter side).
    /// </summary>
    public struct ShapeCell : IEquatable<ShapeCell>
    {
        public const float Full = -1f;
        public float[] V;

        public static ShapeCell Create(ShapeKind kind)
        {
            var c = new ShapeCell { V = new float[24] };
            c.V[0] = (int)kind;
            return c;
        }

        public ShapeKind Kind => (ShapeKind)(int)V[0];

        public ShapeCell WithRadii(float tl, float tr, float br, float bl)
        {
            var c = Clone();
            c.V[1] = tl; c.V[2] = tr; c.V[3] = br; c.V[4] = bl;
            return c;
        }

        public ShapeCell WithRadius(float r) => WithRadii(r, r, r, r);

        /// <summary>Corner radii from a Compose CornerShape (topStart = top-left in LTR).</summary>
        public ShapeCell WithShape(CornerShape s) =>
            s.IsFull ? WithRadius(Full) : WithRadii(s.TopStart, s.TopEnd, s.BottomEnd, s.BottomStart);

        public ShapeCell WithStroke(float width) { var c = Clone(); c.V[5] = width; return c; }
        public ShapeCell WithBlur(float sigma, float spread) { var c = Clone(); c.V[6] = sigma; c.V[7] = spread; return c; }

        /// <summary>Shape rect insets inside the quad, dp: left, bottom, right, top.</summary>
        public ShapeCell WithInsets(float l, float b, float r, float t)
        {
            var c = Clone();
            c.V[8] = l; c.V[9] = b; c.V[10] = r; c.V[11] = t;
            return c;
        }

        /// <summary>P(1,1): kind-specific (progress stop size...).</summary>
        public ShapeCell WithExtra2(float a, float b = 0, float cc = 0, float d = 0)
        {
            var c = Clone();
            c.V[20] = a; c.V[21] = b; c.V[22] = cc; c.V[23] = d;
            return c;
        }

        /// <summary>Kind-specific values P(3,0) and P(0,1).</summary>
        public ShapeCell WithExtra(float a, float b = 0, float cc = 0, float d = 0, float e = 0, float f = 0, float g = 0, float h = 0)
        {
            var c = Clone();
            c.V[12] = a; c.V[13] = b; c.V[14] = cc; c.V[15] = d;
            c.V[16] = e; c.V[17] = f; c.V[18] = g; c.V[19] = h;
            return c;
        }

        ShapeCell Clone() => new ShapeCell { V = (float[])V.Clone() };

        /// <summary>Stable key used to deduplicate cells (values rounded to 1/1000).</summary>
        public string Key()
        {
            var sb = new StringBuilder();
            int last = V.Length - 1;
            while (last > 0 && V[last] == 0) last--;
            for (int i = 0; i <= last; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Math.Round(V[i], 3).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public bool Equals(ShapeCell o) => Key() == o.Key();
        public override bool Equals(object obj) => obj is ShapeCell o && Equals(o);
        public override int GetHashCode() => Key().GetHashCode();
        public override string ToString() => $"{Kind}[{Key()}]";
    }
}
