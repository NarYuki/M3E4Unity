using System;
using System.Collections.Generic;
using M3E4Unity.MaterialColor;
using M3E4Unity.Theming;
using M3E4Unity.Tokens;
using TMPro;
using UnityEngine;

namespace M3E4Unity
{
    /// <summary>One color scheme of a theme (a light or dark scheme from a seed).</summary>
    [Serializable]
    public class M3SchemeEntry
    {
        public string name = "Light";
        public SchemeSource source = SchemeSource.Dynamic;
        [Tooltip("Seed (source) color for dynamic color.")]
        public Color seed = new Color32(0x67, 0x50, 0xA4, 0xFF);
        public Variant variant = Variant.TONAL_SPOT;
        public bool dark;
        [Range(-1f, 1f)] public float contrast;
        public ColorSpec.SpecVersion specVersion = ColorSpec.SpecVersion.SPEC_2025;
        public DynamicScheme.Platform platform = DynamicScheme.Platform.PHONE;

        public SchemeSpec ToSpec()
        {
            Color32 c = seed;
            return new SchemeSpec
            {
                Source = source,
                SeedArgb = (0xFF << 24) | (c.r << 16) | (c.g << 8) | c.b,
                Variant = variant,
                IsDark = dark,
                ContrastLevel = contrast,
                SpecVersion = specVersion,
                Platform = platform,
            };
        }
    }

    /// <summary>
    /// A Material 3 theme: the color schemes to bake (light / dark / more), the motion scheme
    /// and the typefaces. Assign it to an M3 canvas and press "Apply theme".
    /// </summary>
    [CreateAssetMenu(menuName = "M3E4Unity/Theme", fileName = "M3Theme")]
    public sealed class M3ThemeAsset : ScriptableObject
    {
        public List<M3SchemeEntry> schemes = new List<M3SchemeEntry>
        {
            new M3SchemeEntry { name = "Light", dark = false },
            new M3SchemeEntry { name = "Dark", dark = true },
        };

        [Tooltip("Index of the scheme shown in the editor and at start.")]
        public int defaultScheme;

        public MotionSchemeKind motionScheme = MotionSchemeKind.Expressive;

        [Header("Typography")]
        [Tooltip("Brand typeface (display, headline, title large). Google Sans Flex by default.")]
        public TMP_FontAsset brandFont;
        [Tooltip("Plain typeface (body, label, title medium/small).")]
        public TMP_FontAsset plainFont;
        [Tooltip("Material Symbols, unfilled (FILL 0).")]
        public TMP_FontAsset iconFont;
        [Tooltip("Material Symbols, filled (FILL 1), used for selected icons.")]
        public TMP_FontAsset iconFontFilled;

        public ResolvedScheme Resolve(int index) => SchemeBuilder.Resolve(schemes[Mathf.Clamp(index, 0, schemes.Count - 1)].ToSpec());

        /// <summary>Convenience: seeds all schemes from one color and fills light + dark.</summary>
        public void SetSeed(Color seed)
        {
            foreach (var s in schemes) s.seed = seed;
        }
    }
}
