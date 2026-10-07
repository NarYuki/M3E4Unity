using System;
using M3E4Unity.Tokens;

namespace M3E4Unity
{
    /// <summary>
    /// A theme color: a color role, optionally composited over other roles the way Android
    /// and the web blend translucent layers (in sRGB), then given a final alpha.
    /// Pre-compositing in sRGB keeps state layers and disabled colors exact even when the
    /// Unity project renders in linear color space (as VRChat does).
    /// </summary>
    [Serializable]
    public struct ColorRef : IEquatable<ColorRef>
    {
        /// <summary>Bottom layer, opaque. Ignored when <see cref="Layers"/> is 0 and the color is a plain role.</summary>
        public ColorRole Base;
        /// <summary>Number of translucent layers over Base (0-2).</summary>
        public int Layers;
        public ColorRole Layer1;
        public float Layer1Alpha;
        public ColorRole Layer2;
        public float Layer2Alpha;
        /// <summary>Alpha of the final color (1 = opaque).</summary>
        public float Alpha;
        /// <summary>When set, the color is fully transparent (e.g. a text button's container).</summary>
        public bool Transparent;
        /// <summary>When set, Base is ignored and the scheme's shadow color is used (elevation shadows).</summary>
        public bool IsShadow;

        public static ColorRef Role(ColorRole role) => new ColorRef { Base = role, Alpha = 1f };

        /// <summary>The role at an opacity, left translucent (blended by the GPU).</summary>
        public static ColorRef Translucent(ColorRole role, float alpha) => new ColorRef { Base = role, Alpha = alpha };

        /// <summary>role at alpha composited over background, as an opaque color.</summary>
        public static ColorRef Over(ColorRole background, ColorRole role, float alpha) =>
            new ColorRef { Base = background, Layers = 1, Layer1 = role, Layer1Alpha = alpha, Alpha = 1f };

        /// <summary>Two translucent layers over a background (e.g. hover state layer + ripple).</summary>
        public static ColorRef Over(ColorRole background, ColorRole role1, float alpha1, ColorRole role2, float alpha2) =>
            new ColorRef { Base = background, Layers = 2, Layer1 = role1, Layer1Alpha = alpha1, Layer2 = role2, Layer2Alpha = alpha2, Alpha = 1f };

        public static readonly ColorRef Clear = new ColorRef { Transparent = true };

        /// <summary>The scheme's shadow color at an opacity (left translucent).</summary>
        public static ColorRef Shadow(float alpha) => new ColorRef { IsShadow = true, Alpha = alpha };

        public ColorRef WithAlpha(float alpha)
        {
            var c = this;
            c.Alpha = alpha;
            return c;
        }

        /// <summary>Resolves against a scheme (role -> ARGB) to a straight-alpha sRGB color.</summary>
        public void Resolve(Func<ColorRole, int> scheme, int shadowArgb, out float r, out float g, out float b, out float a)
        {
            if (Transparent)
            {
                r = g = b = a = 0f;
                return;
            }
            Unpack(IsShadow ? shadowArgb : scheme(Base), out r, out g, out b);
            if (Layers >= 1) Blend(scheme(Layer1), Layer1Alpha, ref r, ref g, ref b);
            if (Layers >= 2) Blend(scheme(Layer2), Layer2Alpha, ref r, ref g, ref b);
            a = Alpha;
        }

        static void Unpack(int argb, out float r, out float g, out float b)
        {
            r = ((argb >> 16) & 0xFF) / 255f;
            g = ((argb >> 8) & 0xFF) / 255f;
            b = (argb & 0xFF) / 255f;
        }

        static void Blend(int argb, float alpha, ref float r, ref float g, ref float b)
        {
            Unpack(argb, out float lr, out float lg, out float lb);
            r = r + (lr - r) * alpha;
            g = g + (lg - g) * alpha;
            b = b + (lb - b) * alpha;
        }

        public bool Equals(ColorRef o) =>
            Transparent == o.Transparent && IsShadow == o.IsShadow && (IsShadow || Base == o.Base) && Layers == o.Layers &&
            (Layers < 1 || (Layer1 == o.Layer1 && Layer1Alpha == o.Layer1Alpha)) &&
            (Layers < 2 || (Layer2 == o.Layer2 && Layer2Alpha == o.Layer2Alpha)) &&
            Alpha == o.Alpha;

        public override bool Equals(object obj) => obj is ColorRef o && Equals(o);

        public override int GetHashCode()
        {
            if (Transparent) return -1;
            int h = ((IsShadow ? -7 : (int)Base) * 397) ^ Layers;
            if (Layers >= 1) h = h * 31 + ((int)Layer1 * 397 ^ Layer1Alpha.GetHashCode());
            if (Layers >= 2) h = h * 31 + ((int)Layer2 * 397 ^ Layer2Alpha.GetHashCode());
            return h * 31 + Alpha.GetHashCode();
        }

        public override string ToString()
        {
            if (Transparent) return "transparent";
            string s = IsShadow ? "shadow" : Base.ToString();
            if (Layers >= 1) s += $" + {Layer1}@{Layer1Alpha:0.##}";
            if (Layers >= 2) s += $" + {Layer2}@{Layer2Alpha:0.##}";
            if (Alpha < 1f) s += $" @{Alpha:0.##}";
            return s;
        }
    }
}
