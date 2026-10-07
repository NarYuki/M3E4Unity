// Motion and elevation schemes (MotionScheme.kt, ElevationTokens, StateTokens).
using System;

namespace M3E4Unity.Tokens
{
    public enum MotionSchemeKind
    {
        /// <summary>MotionScheme.standard(): no bounce, for utilitarian UI.</summary>
        Standard,
        /// <summary>MotionScheme.expressive(): the M3 Expressive default, with overshoot on spatial springs.</summary>
        Expressive,
    }

    /// <summary>MotionScheme.standard() / expressive(), mapped from MotionSchemeKeyTokens.</summary>
    public static class MotionScheme
    {
        public static Spring Get(MotionSchemeKind kind, MotionRole role)
        {
            if (kind == MotionSchemeKind.Expressive)
            {
                switch (role)
                {
                    case MotionRole.DefaultSpatial: return new Spring(ExpressiveMotionTokens.SpringDefaultSpatialDamping, ExpressiveMotionTokens.SpringDefaultSpatialStiffness);
                    case MotionRole.FastSpatial: return new Spring(ExpressiveMotionTokens.SpringFastSpatialDamping, ExpressiveMotionTokens.SpringFastSpatialStiffness);
                    case MotionRole.SlowSpatial: return new Spring(ExpressiveMotionTokens.SpringSlowSpatialDamping, ExpressiveMotionTokens.SpringSlowSpatialStiffness);
                    case MotionRole.DefaultEffects: return new Spring(ExpressiveMotionTokens.SpringDefaultEffectsDamping, ExpressiveMotionTokens.SpringDefaultEffectsStiffness);
                    case MotionRole.FastEffects: return new Spring(ExpressiveMotionTokens.SpringFastEffectsDamping, ExpressiveMotionTokens.SpringFastEffectsStiffness);
                    case MotionRole.SlowEffects: return new Spring(ExpressiveMotionTokens.SpringSlowEffectsDamping, ExpressiveMotionTokens.SpringSlowEffectsStiffness);
                }
            }
            else
            {
                switch (role)
                {
                    case MotionRole.DefaultSpatial: return new Spring(StandardMotionTokens.SpringDefaultSpatialDamping, StandardMotionTokens.SpringDefaultSpatialStiffness);
                    case MotionRole.FastSpatial: return new Spring(StandardMotionTokens.SpringFastSpatialDamping, StandardMotionTokens.SpringFastSpatialStiffness);
                    case MotionRole.SlowSpatial: return new Spring(StandardMotionTokens.SpringSlowSpatialDamping, StandardMotionTokens.SpringSlowSpatialStiffness);
                    case MotionRole.DefaultEffects: return new Spring(StandardMotionTokens.SpringDefaultEffectsDamping, StandardMotionTokens.SpringDefaultEffectsStiffness);
                    case MotionRole.FastEffects: return new Spring(StandardMotionTokens.SpringFastEffectsDamping, StandardMotionTokens.SpringFastEffectsStiffness);
                    case MotionRole.SlowEffects: return new Spring(StandardMotionTokens.SpringSlowEffectsDamping, StandardMotionTokens.SpringSlowEffectsStiffness);
                }
            }
            return Get(kind, MotionRole.DefaultSpatial);
        }
    }

    /// <summary>One drop shadow layer in dp.</summary>
    [Serializable]
    public struct ShadowLayer
    {
        public float OffsetY, Blur, Spread, Opacity;

        public ShadowLayer(float offsetY, float blur, float spread, float opacity)
        {
            OffsetY = offsetY; Blur = blur; Spread = spread; Opacity = opacity;
        }
    }

    /// <summary>
    /// Elevation levels (ElevationTokens) and their two-layer shadows, as implemented by
    /// material-web elevation/internal/_elevation.scss: a key shadow at 30% and an ambient
    /// shadow at 15% of the scheme's shadow color. Blur is the CSS blur radius (sigma = blur / 2).
    /// </summary>
    public static class Elevation
    {
        public static readonly float[] Levels =
        {
            ElevationTokens.Level0, ElevationTokens.Level1, ElevationTokens.Level2,
            ElevationTokens.Level3, ElevationTokens.Level4, ElevationTokens.Level5,
        };

        /// <summary>Level (0-5) for an elevation value in dp.</summary>
        public static int LevelOf(float dp)
        {
            int best = 0;
            for (int i = 0; i < Levels.Length; i++)
                if (Math.Abs(Levels[i] - dp) < Math.Abs(Levels[best] - dp)) best = i;
            return best;
        }

        /// <summary>Key and ambient shadow of a level (material-web values).</summary>
        public static void Shadows(int level, out ShadowLayer key, out ShadowLayer ambient)
        {
            switch (level)
            {
                case 1: key = new ShadowLayer(1, 2, 0, 0.3f); ambient = new ShadowLayer(1, 3, 1, 0.15f); return;
                case 2: key = new ShadowLayer(1, 2, 0, 0.3f); ambient = new ShadowLayer(2, 6, 2, 0.15f); return;
                case 3: key = new ShadowLayer(1, 3, 0, 0.3f); ambient = new ShadowLayer(4, 8, 3, 0.15f); return;
                case 4: key = new ShadowLayer(2, 3, 0, 0.3f); ambient = new ShadowLayer(6, 10, 4, 0.15f); return;
                case 5: key = new ShadowLayer(4, 4, 0, 0.3f); ambient = new ShadowLayer(8, 12, 6, 0.15f); return;
                default: key = new ShadowLayer(0, 0, 0, 0); ambient = new ShadowLayer(0, 0, 0, 0); return;
            }
        }
    }

    /// <summary>State layer opacities (StateTokens) and the disabled-state conventions.</summary>
    public static class StateLayer
    {
        public const float Hover = StateTokens.HoverStateLayerOpacity;
        public const float Focus = StateTokens.FocusStateLayerOpacity;
        public const float Pressed = StateTokens.PressedStateLayerOpacity;
        public const float Dragged = StateTokens.DraggedStateLayerOpacity;
        /// <summary>Disabled content: onSurface at 38%.</summary>
        public const float DisabledContent = 0.38f;
        /// <summary>Disabled container: onSurface at 12%.</summary>
        public const float DisabledContainer = 0.12f;
    }
}
