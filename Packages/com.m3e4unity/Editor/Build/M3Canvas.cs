using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Creates an M3 UI root: a Canvas measured in dp (1 canvas unit = 1 dp), the runtime theme,
    /// and, in VRChat projects, the VRCUiShape that makes the canvas clickable in the world.
    /// </summary>
    public static class M3Canvas
    {
        public const string DefaultThemePath = M3ShapeAtlas.GeneratedFolder + "/M3Theme.asset";

        public static M3ThemeAsset DefaultTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<M3ThemeAsset>(DefaultThemePath);
            if (theme == null)
            {
                System.IO.Directory.CreateDirectory(M3ShapeAtlas.GeneratedFolder);
                theme = ScriptableObject.CreateInstance<M3ThemeAsset>();
                AssetDatabase.CreateAsset(theme, DefaultThemePath);
            }
            M3Fonts.FillTheme(theme);
            return theme;
        }

        /// <param name="worldSpace">World-space canvas (VRChat); 1 dp = metersPerDp.</param>
        public static RectTransform Create(string name, M3ThemeAsset theme, Vector2 sizeDp, bool worldSpace, float metersPerDp = 0.001f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            var rt = (RectTransform)go.transform;
            var canvas = go.AddComponent<Canvas>();
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            if (worldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                rt.sizeDelta = sizeDp;
                rt.localScale = Vector3.one * metersPerDp;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 1f;
                scaler.referencePixelsPerUnit = 100f;
                go.layer = 0; // VRChat raycasts UI on the Default layer
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
                // dp is defined at 160 dpi
                scaler.physicalUnit = CanvasScaler.Unit.Points;
                scaler.fallbackScreenDPI = 160f;
                scaler.defaultSpriteDPI = 160f;
                go.layer = 5;
            }
            go.AddComponent<GraphicRaycaster>();
#if UDONSHARP
            go.AddComponent<VRC.SDK3.Components.VRCUiShape>();
#endif
            var runtime = M3UdonBridge.Add<M3Theme>(go);
            var source = go.AddComponent<M3ThemeSource>();
            source.theme = theme;
            if (!worldSpace && Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
            return rt;
        }

        /// <summary>A full-size surface behind content (the scheme's background).</summary>
        public static Image Surface(RectTransform parent, ColorRole role = ColorRole.Surface, float radius = 0f)
        {
            var img = M3Build.Shape("Surface", parent, ShapeCell.Create(ShapeKind.Fill).WithRadius(radius), ColorRef.Role(role));
            img.transform.SetAsFirstSibling();
            return img;
        }

        /// <summary>Bakes the theme of the root containing this object.</summary>
        public static void ApplyTheme(GameObject anyChild)
        {
            var runtime = anyChild.GetComponentInParent<M3Theme>(true);
            if (runtime == null) return;
            var source = runtime.GetComponent<M3ThemeSource>();
            var theme = source != null && source.theme != null ? source.theme : DefaultTheme();
            M3ShapeAtlas.Save();
            M3ShapeTiles.Save();
            M3ThemeBaker.Bake(runtime, theme);
        }
    }
}
