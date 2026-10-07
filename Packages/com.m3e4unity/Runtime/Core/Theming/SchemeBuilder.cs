// Builds Material 3 color schemes with the ported material-color-utilities.
using System;
using System.Collections.Generic;
using M3E4Unity.MaterialColor;
using M3E4Unity.Tokens;

namespace M3E4Unity.Theming
{
    /// <summary>Where the colors of a scheme come from.</summary>
    public enum SchemeSource
    {
        /// <summary>Dynamic color from a seed (material-color-utilities DynamicScheme).</summary>
        Dynamic,
        /// <summary>The static baseline scheme of Compose (ColorLightTokens / ColorDarkTokens).</summary>
        Baseline,
    }

    /// <summary>Everything needed to produce one color scheme.</summary>
    [Serializable]
    public struct SchemeSpec
    {
        public SchemeSource Source;
        public int SeedArgb;
        public Variant Variant;
        public bool IsDark;
        public double ContrastLevel;
        public ColorSpec.SpecVersion SpecVersion;
        public DynamicScheme.Platform Platform;

        public static SchemeSpec Default(bool dark) => new SchemeSpec
        {
            Source = SchemeSource.Dynamic,
            SeedArgb = unchecked((int)0xFF6750A4),
            Variant = Variant.TONAL_SPOT,
            IsDark = dark,
            ContrastLevel = 0.0,
            SpecVersion = ColorSpec.SpecVersion.SPEC_2025,
            Platform = DynamicScheme.Platform.PHONE,
        };
    }

    /// <summary>A resolved scheme: every color role as ARGB.</summary>
    public sealed class ResolvedScheme
    {
        /// <summary>Compose ColorScheme roles (ColorSchemeKeyTokens), indexed by (int)ColorRole.</summary>
        public readonly int[] Roles = new int[Enum.GetValues(typeof(ColorRole)).Length];

        /// <summary>Every MaterialDynamicColors color by its upstream name (e.g. "primary_dim"), dynamic schemes only.</summary>
        public readonly Dictionary<string, int> Named = new Dictionary<string, int>();

        /// <summary>The scheme's shadow color (black unless the spec says otherwise).</summary>
        public int Shadow = unchecked((int)0xFF000000);

        public DynamicScheme Dynamic;

        public int this[ColorRole role] => Roles[(int)role];
    }

    public static class SchemeBuilder
    {
        static readonly MaterialDynamicColors Colors = new MaterialDynamicColors();

        public static DynamicScheme CreateDynamic(SchemeSpec spec)
        {
            var hct = Hct.fromInt(spec.SeedArgb);
            double c = spec.ContrastLevel;
            var s = spec.SpecVersion;
            var p = spec.Platform;
            bool d = spec.IsDark;
            switch (spec.Variant)
            {
                case Variant.MONOCHROME: return new SchemeMonochrome(hct, d, c, s, p);
                case Variant.NEUTRAL: return new SchemeNeutral(hct, d, c, s, p);
                case Variant.TONAL_SPOT: return new SchemeTonalSpot(hct, d, c, s, p);
                case Variant.VIBRANT: return new SchemeVibrant(hct, d, c, s, p);
                case Variant.EXPRESSIVE: return new SchemeExpressive(hct, d, c, s, p);
                case Variant.FIDELITY: return new SchemeFidelity(hct, d, c, s, p);
                case Variant.CONTENT: return new SchemeContent(hct, d, c, s, p);
                case Variant.RAINBOW: return new SchemeRainbow(hct, d, c, s, p);
                case Variant.FRUIT_SALAD: return new SchemeFruitSalad(hct, d, c, s, p);
                case Variant.CMF: return new SchemeCmf(hct, d, c, s, p);
            }
            throw new ArgumentOutOfRangeException(nameof(spec));
        }

        public static ResolvedScheme Resolve(SchemeSpec spec)
        {
            var result = new ResolvedScheme();
            if (spec.Source == SchemeSource.Baseline)
            {
                foreach (ColorRole role in Enum.GetValues(typeof(ColorRole)))
                    result.Roles[(int)role] = unchecked((int)(spec.IsDark ? Dark(role) : Light(role)));
                return result;
            }
            var scheme = CreateDynamic(spec);
            result.Dynamic = scheme;
            foreach (ColorRole role in Enum.GetValues(typeof(ColorRole)))
                result.Roles[(int)role] = Role(role).getArgb(scheme);
            foreach (var sup in Colors.allDynamicColors())
            {
                var dc = sup();
                result.Named[dc.name] = dc.getArgb(scheme);
            }
            result.Shadow = Colors.shadow().getArgb(scheme);
            return result;
        }

        /// <summary>The MaterialDynamicColors entry for a Compose color role.</summary>
        public static DynamicColor Role(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.Background: return Colors.background();
                case ColorRole.Error: return Colors.error();
                case ColorRole.ErrorContainer: return Colors.errorContainer();
                case ColorRole.InverseOnSurface: return Colors.inverseOnSurface();
                case ColorRole.InversePrimary: return Colors.inversePrimary();
                case ColorRole.InverseSurface: return Colors.inverseSurface();
                case ColorRole.OnBackground: return Colors.onBackground();
                case ColorRole.OnError: return Colors.onError();
                case ColorRole.OnErrorContainer: return Colors.onErrorContainer();
                case ColorRole.OnPrimary: return Colors.onPrimary();
                case ColorRole.OnPrimaryContainer: return Colors.onPrimaryContainer();
                case ColorRole.OnPrimaryFixed: return Colors.onPrimaryFixed();
                case ColorRole.OnPrimaryFixedVariant: return Colors.onPrimaryFixedVariant();
                case ColorRole.OnSecondary: return Colors.onSecondary();
                case ColorRole.OnSecondaryContainer: return Colors.onSecondaryContainer();
                case ColorRole.OnSecondaryFixed: return Colors.onSecondaryFixed();
                case ColorRole.OnSecondaryFixedVariant: return Colors.onSecondaryFixedVariant();
                case ColorRole.OnSurface: return Colors.onSurface();
                case ColorRole.OnSurfaceVariant: return Colors.onSurfaceVariant();
                case ColorRole.OnTertiary: return Colors.onTertiary();
                case ColorRole.OnTertiaryContainer: return Colors.onTertiaryContainer();
                case ColorRole.OnTertiaryFixed: return Colors.onTertiaryFixed();
                case ColorRole.OnTertiaryFixedVariant: return Colors.onTertiaryFixedVariant();
                case ColorRole.Outline: return Colors.outline();
                case ColorRole.OutlineVariant: return Colors.outlineVariant();
                case ColorRole.Primary: return Colors.primary();
                case ColorRole.PrimaryContainer: return Colors.primaryContainer();
                case ColorRole.PrimaryFixed: return Colors.primaryFixed();
                case ColorRole.PrimaryFixedDim: return Colors.primaryFixedDim();
                case ColorRole.Scrim: return Colors.scrim();
                case ColorRole.Secondary: return Colors.secondary();
                case ColorRole.SecondaryContainer: return Colors.secondaryContainer();
                case ColorRole.SecondaryFixed: return Colors.secondaryFixed();
                case ColorRole.SecondaryFixedDim: return Colors.secondaryFixedDim();
                case ColorRole.Surface: return Colors.surface();
                case ColorRole.SurfaceBright: return Colors.surfaceBright();
                case ColorRole.SurfaceContainer: return Colors.surfaceContainer();
                case ColorRole.SurfaceContainerHigh: return Colors.surfaceContainerHigh();
                case ColorRole.SurfaceContainerHighest: return Colors.surfaceContainerHighest();
                case ColorRole.SurfaceContainerLow: return Colors.surfaceContainerLow();
                case ColorRole.SurfaceContainerLowest: return Colors.surfaceContainerLowest();
                case ColorRole.SurfaceDim: return Colors.surfaceDim();
                case ColorRole.SurfaceTint: return Colors.surfaceTint();
                case ColorRole.SurfaceVariant: return Colors.surfaceVariant();
                case ColorRole.Tertiary: return Colors.tertiary();
                case ColorRole.TertiaryContainer: return Colors.tertiaryContainer();
                case ColorRole.TertiaryFixed: return Colors.tertiaryFixed();
                case ColorRole.TertiaryFixedDim: return Colors.tertiaryFixedDim();
            }
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        static uint Light(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.Background: return ColorLightTokens.Background;
                case ColorRole.Error: return ColorLightTokens.Error;
                case ColorRole.ErrorContainer: return ColorLightTokens.ErrorContainer;
                case ColorRole.InverseOnSurface: return ColorLightTokens.InverseOnSurface;
                case ColorRole.InversePrimary: return ColorLightTokens.InversePrimary;
                case ColorRole.InverseSurface: return ColorLightTokens.InverseSurface;
                case ColorRole.OnBackground: return ColorLightTokens.OnBackground;
                case ColorRole.OnError: return ColorLightTokens.OnError;
                case ColorRole.OnErrorContainer: return ColorLightTokens.OnErrorContainer;
                case ColorRole.OnPrimary: return ColorLightTokens.OnPrimary;
                case ColorRole.OnPrimaryContainer: return ColorLightTokens.OnPrimaryContainer;
                case ColorRole.OnPrimaryFixed: return ColorLightTokens.OnPrimaryFixed;
                case ColorRole.OnPrimaryFixedVariant: return ColorLightTokens.OnPrimaryFixedVariant;
                case ColorRole.OnSecondary: return ColorLightTokens.OnSecondary;
                case ColorRole.OnSecondaryContainer: return ColorLightTokens.OnSecondaryContainer;
                case ColorRole.OnSecondaryFixed: return ColorLightTokens.OnSecondaryFixed;
                case ColorRole.OnSecondaryFixedVariant: return ColorLightTokens.OnSecondaryFixedVariant;
                case ColorRole.OnSurface: return ColorLightTokens.OnSurface;
                case ColorRole.OnSurfaceVariant: return ColorLightTokens.OnSurfaceVariant;
                case ColorRole.OnTertiary: return ColorLightTokens.OnTertiary;
                case ColorRole.OnTertiaryContainer: return ColorLightTokens.OnTertiaryContainer;
                case ColorRole.OnTertiaryFixed: return ColorLightTokens.OnTertiaryFixed;
                case ColorRole.OnTertiaryFixedVariant: return ColorLightTokens.OnTertiaryFixedVariant;
                case ColorRole.Outline: return ColorLightTokens.Outline;
                case ColorRole.OutlineVariant: return ColorLightTokens.OutlineVariant;
                case ColorRole.Primary: return ColorLightTokens.Primary;
                case ColorRole.PrimaryContainer: return ColorLightTokens.PrimaryContainer;
                case ColorRole.PrimaryFixed: return ColorLightTokens.PrimaryFixed;
                case ColorRole.PrimaryFixedDim: return ColorLightTokens.PrimaryFixedDim;
                case ColorRole.Scrim: return ColorLightTokens.Scrim;
                case ColorRole.Secondary: return ColorLightTokens.Secondary;
                case ColorRole.SecondaryContainer: return ColorLightTokens.SecondaryContainer;
                case ColorRole.SecondaryFixed: return ColorLightTokens.SecondaryFixed;
                case ColorRole.SecondaryFixedDim: return ColorLightTokens.SecondaryFixedDim;
                case ColorRole.Surface: return ColorLightTokens.Surface;
                case ColorRole.SurfaceBright: return ColorLightTokens.SurfaceBright;
                case ColorRole.SurfaceContainer: return ColorLightTokens.SurfaceContainer;
                case ColorRole.SurfaceContainerHigh: return ColorLightTokens.SurfaceContainerHigh;
                case ColorRole.SurfaceContainerHighest: return ColorLightTokens.SurfaceContainerHighest;
                case ColorRole.SurfaceContainerLow: return ColorLightTokens.SurfaceContainerLow;
                case ColorRole.SurfaceContainerLowest: return ColorLightTokens.SurfaceContainerLowest;
                case ColorRole.SurfaceDim: return ColorLightTokens.SurfaceDim;
                case ColorRole.SurfaceTint: return ColorLightTokens.SurfaceTint;
                case ColorRole.SurfaceVariant: return ColorLightTokens.SurfaceVariant;
                case ColorRole.Tertiary: return ColorLightTokens.Tertiary;
                case ColorRole.TertiaryContainer: return ColorLightTokens.TertiaryContainer;
                case ColorRole.TertiaryFixed: return ColorLightTokens.TertiaryFixed;
                case ColorRole.TertiaryFixedDim: return ColorLightTokens.TertiaryFixedDim;
            }
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        static uint Dark(ColorRole role)
        {
            switch (role)
            {
                case ColorRole.Background: return ColorDarkTokens.Background;
                case ColorRole.Error: return ColorDarkTokens.Error;
                case ColorRole.ErrorContainer: return ColorDarkTokens.ErrorContainer;
                case ColorRole.InverseOnSurface: return ColorDarkTokens.InverseOnSurface;
                case ColorRole.InversePrimary: return ColorDarkTokens.InversePrimary;
                case ColorRole.InverseSurface: return ColorDarkTokens.InverseSurface;
                case ColorRole.OnBackground: return ColorDarkTokens.OnBackground;
                case ColorRole.OnError: return ColorDarkTokens.OnError;
                case ColorRole.OnErrorContainer: return ColorDarkTokens.OnErrorContainer;
                case ColorRole.OnPrimary: return ColorDarkTokens.OnPrimary;
                case ColorRole.OnPrimaryContainer: return ColorDarkTokens.OnPrimaryContainer;
                case ColorRole.OnPrimaryFixed: return ColorDarkTokens.OnPrimaryFixed;
                case ColorRole.OnPrimaryFixedVariant: return ColorDarkTokens.OnPrimaryFixedVariant;
                case ColorRole.OnSecondary: return ColorDarkTokens.OnSecondary;
                case ColorRole.OnSecondaryContainer: return ColorDarkTokens.OnSecondaryContainer;
                case ColorRole.OnSecondaryFixed: return ColorDarkTokens.OnSecondaryFixed;
                case ColorRole.OnSecondaryFixedVariant: return ColorDarkTokens.OnSecondaryFixedVariant;
                case ColorRole.OnSurface: return ColorDarkTokens.OnSurface;
                case ColorRole.OnSurfaceVariant: return ColorDarkTokens.OnSurfaceVariant;
                case ColorRole.OnTertiary: return ColorDarkTokens.OnTertiary;
                case ColorRole.OnTertiaryContainer: return ColorDarkTokens.OnTertiaryContainer;
                case ColorRole.OnTertiaryFixed: return ColorDarkTokens.OnTertiaryFixed;
                case ColorRole.OnTertiaryFixedVariant: return ColorDarkTokens.OnTertiaryFixedVariant;
                case ColorRole.Outline: return ColorDarkTokens.Outline;
                case ColorRole.OutlineVariant: return ColorDarkTokens.OutlineVariant;
                case ColorRole.Primary: return ColorDarkTokens.Primary;
                case ColorRole.PrimaryContainer: return ColorDarkTokens.PrimaryContainer;
                case ColorRole.PrimaryFixed: return ColorDarkTokens.PrimaryFixed;
                case ColorRole.PrimaryFixedDim: return ColorDarkTokens.PrimaryFixedDim;
                case ColorRole.Scrim: return ColorDarkTokens.Scrim;
                case ColorRole.Secondary: return ColorDarkTokens.Secondary;
                case ColorRole.SecondaryContainer: return ColorDarkTokens.SecondaryContainer;
                case ColorRole.SecondaryFixed: return ColorDarkTokens.SecondaryFixed;
                case ColorRole.SecondaryFixedDim: return ColorDarkTokens.SecondaryFixedDim;
                case ColorRole.Surface: return ColorDarkTokens.Surface;
                case ColorRole.SurfaceBright: return ColorDarkTokens.SurfaceBright;
                case ColorRole.SurfaceContainer: return ColorDarkTokens.SurfaceContainer;
                case ColorRole.SurfaceContainerHigh: return ColorDarkTokens.SurfaceContainerHigh;
                case ColorRole.SurfaceContainerHighest: return ColorDarkTokens.SurfaceContainerHighest;
                case ColorRole.SurfaceContainerLow: return ColorDarkTokens.SurfaceContainerLow;
                case ColorRole.SurfaceContainerLowest: return ColorDarkTokens.SurfaceContainerLowest;
                case ColorRole.SurfaceDim: return ColorDarkTokens.SurfaceDim;
                case ColorRole.SurfaceTint: return ColorDarkTokens.SurfaceTint;
                case ColorRole.SurfaceVariant: return ColorDarkTokens.SurfaceVariant;
                case ColorRole.Tertiary: return ColorDarkTokens.Tertiary;
                case ColorRole.TertiaryContainer: return ColorDarkTokens.TertiaryContainer;
                case ColorRole.TertiaryFixed: return ColorDarkTokens.TertiaryFixed;
                case ColorRole.TertiaryFixedDim: return ColorDarkTokens.TertiaryFixedDim;
            }
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        /// <summary>Harmonizes a custom color toward the seed (Blend.harmonize), as M3 custom colors do.</summary>
        public static int Harmonize(int customArgb, int seedArgb) => Blend.harmonize(customArgb, seedArgb);

        /// <summary>
        /// Extracts the best seed colors from image pixels (ARGB), as Android wallpaper theming does:
        /// QuantizerCelebi to 128 colors, then Score.
        /// </summary>
        public static List<int> SeedsFromImage(int[] argbPixels, int maxColors = 4)
        {
            var quantized = QuantizerCelebi.quantize(argbPixels, 128);
            var ranked = Score.score(quantized, maxColors);
            return ranked;
        }
    }
}
