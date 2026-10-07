using System.Collections.Generic;
using System.IO;
using M3E4Unity.Tokens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Creates the TextMesh Pro font assets M3E4Unity uses from the bundled fonts:
    /// Google Sans Flex (brand and plain, 400/500/700) with Noto Sans JP as fallback, and
    /// Material Symbols Rounded (fill 0 and 1). All are dynamic assets, so any character
    /// (Japanese included) is rasterised on first use.
    /// </summary>
    public static class M3Fonts
    {
        public const string PackageFonts = "Packages/com.m3e4unity/Fonts";
        public const string FontFolder = M3ShapeAtlas.GeneratedFolder + "/Fonts";

        static readonly (int weight, string name)[] Weights = { (400, "Regular"), (500, "Medium"), (700, "Bold") };

        public static void EnsureTmpEssentials()
        {
            if (Shader.Find("TextMeshPro/Distance Field") != null) return;
            // Imports "TMP Essential Resources" (settings, default shaders) without a dialog.
            // The import completes asynchronously; callers creating fonts must run afterwards.
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            throw new System.InvalidOperationException("TextMesh Pro Essential Resources were missing and are being imported. Run the command again after the import finishes.");
        }

        static TMP_FontAsset CreateOrLoad(string ttf, string assetName, int samplingSize, int atlasSize)
        {
            Directory.CreateDirectory(FontFolder);
            string path = $"{FontFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{PackageFonts}/{ttf}");
            if (font == null) throw new FileNotFoundException($"{PackageFonts}/{ttf} not found");
            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, samplingSize / 10, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            // The atlas texture and material must live inside the font asset file.
            asset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>Creates (once) and returns the M3 text font with weights and the Japanese fallback.</summary>
        public static TMP_FontAsset Text()
        {
            EnsureTmpEssentials();
            var fonts = new Dictionary<int, TMP_FontAsset>();
            var jp = new Dictionary<int, TMP_FontAsset>();
            foreach (var (weight, name) in Weights)
            {
                fonts[weight] = CreateOrLoad($"GoogleSansFlex-{name}.ttf", $"GoogleSansFlex-{name} SDF", 90, 1024);
                jp[weight] = CreateOrLoad($"NotoSansJP-{name}.ttf", $"NotoSansJP-{name} SDF", 64, 2048);
            }
            foreach (var (weight, _) in Weights)
            {
                var f = fonts[weight];
                f.fallbackFontAssetTable = new List<TMP_FontAsset> { jp[weight] };
                EditorUtility.SetDirty(f);
            }
            // Weight table on the regular face so that TMP_Text.fontWeight selects the right file.
            // fontWeightTable is an array of structs: edit the elements in place.
            var regular = fonts[400];
            regular.fontWeightTable[4].regularTypeface = fonts[400];
            regular.fontWeightTable[5].regularTypeface = fonts[500];
            regular.fontWeightTable[7].regularTypeface = fonts[700];
            var jpRegular = jp[400];
            jpRegular.fontWeightTable[4].regularTypeface = jp[400];
            jpRegular.fontWeightTable[5].regularTypeface = jp[500];
            jpRegular.fontWeightTable[7].regularTypeface = jp[700];
            EditorUtility.SetDirty(regular);
            EditorUtility.SetDirty(jpRegular);
            AssetDatabase.SaveAssets();
            return regular;
        }

        public static TMP_FontAsset Icons(bool filled)
        {
            EnsureTmpEssentials();
            string fill = filled ? "Fill1" : "Fill0";
            return CreateOrLoad($"MaterialSymbolsRounded-{fill}.ttf", $"MaterialSymbolsRounded-{fill} SDF", 96, 1024);
        }

        /// <summary>Fills empty font slots of a theme with the bundled defaults.</summary>
        public static void FillTheme(M3ThemeAsset theme)
        {
            if (theme.brandFont == null) theme.brandFont = Text();
            if (theme.plainFont == null) theme.plainFont = Text();
            if (theme.iconFont == null) theme.iconFont = Icons(false);
            if (theme.iconFontFilled == null) theme.iconFontFilled = Icons(true);
            EditorUtility.SetDirty(theme);
        }

        static FontWeight ToTmpWeight(int weight)
        {
            switch (weight)
            {
                case 500: return FontWeight.Medium;
                case 600: return FontWeight.SemiBold;
                case 700: return FontWeight.Bold;
                default: return FontWeight.Regular;
            }
        }

        /// <summary>
        /// Applies a type scale role to a TextMesh Pro text: font, weight, size, letter spacing
        /// (sp -> 1/100 em) and line height (line spacing so that baselines are LineHeight apart).
        /// </summary>
        public static void ApplyStyle(TMP_Text text, TypeRole role, M3ThemeAsset theme)
        {
            var style = TypeScale.Get(role);
            var font = style.Font == FontRole.Brand ? theme.brandFont : theme.plainFont;
            if (font == null) font = Text();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontWeight = ToTmpWeight(style.Weight);
            text.fontStyle = FontStyles.Normal;
            text.fontSize = style.Size;
            text.characterSpacing = style.TmpCharacterSpacing;
            var face = font.faceInfo;
            float natural = face.lineHeight / face.pointSize;
            text.lineSpacing = (style.LineHeight / style.Size - natural) * 100f;
            text.enableAutoSizing = false;
            text.extraPadding = false;
            text.richText = false;
            text.raycastTarget = false;
        }
    }
}
