using System;
using System.IO;
using System.Linq;
using System.Reflection;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>
    /// Developer entry points for batch mode:
    ///   Unity -batchmode -projectPath P -executeMethod M3E4Unity.Editor.Dev.M3Batch.Catalog
    /// builds Assets/M3Catalog/M3Catalog.unity and renders PNGs of every page to &lt;project&gt;/Shots.
    /// </summary>
    public static class M3Batch
    {
        public const string ScenePath = "Assets/M3Catalog/M3Catalog.unity";

        /// <summary>
        /// First run in a fresh project: imports TMP Essential Resources (asynchronous, so the
        /// editor stays open until the import completes) and creates the UdonSharp assets.
        /// </summary>
        public static void Setup()
        {
#if UDONSHARP
            M3UdonSetup.Ensure();
#endif
            if (AssetDatabase.FindAssets("t:Shader TMP_SDF", new[] { "Assets" }).Length > 0 ||
                Shader.Find("TextMeshPro/Distance Field") != null)
            {
                Debug.Log("[M3Batch] setup: TMP essentials present");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            AssetDatabase.importPackageCompleted += name =>
            {
                Debug.Log("[M3Batch] setup: imported " + name);
                AssetDatabase.Refresh();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (name, error) =>
            {
                Debug.LogError("[M3Batch] setup: import failed " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            };
            string pkg = Path.GetFullPath("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(pkg, false);
        }

        public static void Catalog()
        {
            int code = 0;
            try
            {
#if UDONSHARP
                M3UdonSetup.Ensure();
#endif
                CompileUdon();
                var pages = M3Catalog.Build(ScenePath);
                RenderPages(pages);
            }
            catch (Exception e)
            {
                Debug.LogError("[M3Batch] " + e);
                code = 1;
            }
            Debug.Log("[M3Batch] done code=" + code);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Compiles all UdonSharp programs (no-op outside VRChat projects).</summary>
        public static void CompileUdon()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies();
            var compT = asm.Select(a => a.GetType("UdonSharp.Compiler.UdonSharpCompilerV1")).FirstOrDefault(x => x != null);
            var optT = asm.Select(a => a.GetType("UdonSharp.Compiler.UdonSharpCompileOptions")).FirstOrDefault(x => x != null);
            if (compT == null || optT == null) return;
            var opt = Activator.CreateInstance(optT);
            optT.GetProperty("IsEditorBuild")?.SetValue(opt, true);
            optT.GetField("IsEditorBuild")?.SetValue(opt, true);
            compT.GetMethod("CompileSync", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new[] { opt });
        }

        /// <summary>
        /// Renders the shader-animated indicators (progress and loading indicators) at fixed times into
        /// Shots/motion_sheet.png: one row per time step (M3_MOTION_START, M3_MOTION_STEP, M3_MOTION_ROWS).
        /// </summary>
        public static void MotionSheet()
        {
            int code = 0;
            try
            {
                float t0 = float.TryParse(Environment.GetEnvironmentVariable("M3_MOTION_START"), out var a) ? a : 0f;
                float dt = float.TryParse(Environment.GetEnvironmentVariable("M3_MOTION_STEP"), out var b) ? b : 0.1f;
                int rows = int.TryParse(Environment.GetEnvironmentVariable("M3_MOTION_ROWS"), out var c) ? c : 24;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var theme = M3Canvas.DefaultTheme();
                const float cell = 64f;
                var builders = new Action<RectTransform, M3Context>[]
                {
                    (p, x) => M3ProgressIndicators.Circular(p, x, 0.6f, wavy: true),
                    (p, x) => M3ProgressIndicators.Circular(p, x, 0.3f, wavy: true),
                    (p, x) => M3ProgressIndicators.Circular(p, x, null, wavy: true),
                    (p, x) => M3ProgressIndicators.Circular(p, x, null),
                    (p, x) => M3ProgressIndicators.Loading(p, x),
                    (p, x) => M3ProgressIndicators.Loading(p, x, contained: true),
                    (p, x) => M3ProgressIndicators.Loading(p, x, progress: 0.3f),
                };
                var size = new Vector2(cell * builders.Length, cell);
                var root = M3Canvas.Create("Motion", theme, size, worldSpace: false);
                M3Canvas.Surface(root);
                var ctx = new M3Context(theme);
                for (int i = 0; i < builders.Length; i++)
                {
                    var slot = M3Build.Rect("Slot" + i, root);
                    slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                    slot.pivot = new Vector2(0.5f, 0.5f);
                    slot.anchoredPosition = new Vector2(cell * (i + 0.5f), -cell / 2f);
                    slot.sizeDelta = new Vector2(cell, cell);
                    builders[i](slot, ctx);
                    foreach (Transform child in slot)
                    {
                        var rt = (RectTransform)child;
                        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = Vector2.zero;
                    }
                }
                M3Canvas.ApplyTheme(root.gameObject);
                M3ShapeAtlas.Save();
                M3ShapeTiles.Save();

                int scale = 3;
                int w = Mathf.CeilToInt(size.x * scale), h = Mathf.CeilToInt(size.y * scale);
                var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var camGo = new GameObject("ShotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographic = true;
                cam.targetTexture = rtex;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scale;
                var sheet = new Texture2D(w, h * rows, TextureFormat.RGB24, false);
                var row = new Texture2D(w, h, TextureFormat.RGB24, false);
                Shader.SetGlobalFloat("_M3TimeOverrideOn", 1f);
                for (int r = 0; r < rows; r++)
                {
                    Shader.SetGlobalFloat("_M3TimeOverride", t0 + r * dt);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rtex;
                    row.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    row.Apply();
                    RenderTexture.active = prev;
                    sheet.SetPixels(0, h * (rows - 1 - r), w, h, row.GetPixels());
                }
                Shader.SetGlobalFloat("_M3TimeOverrideOn", 0f);
                sheet.Apply();
                string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Shots");
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, "motion_sheet.png"), sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(camGo);
                rtex.Release();
                Debug.Log($"[M3Batch] motion sheet: {rows} rows from {t0}s every {dt}s");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Renders every catalog page (a screen-space-camera canvas) to Shots/&lt;name&gt;.png.</summary>
        public static void RenderPages(M3Catalog.Page[] pages)
        {
            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Shots");
            Directory.CreateDirectory(outDir);
            foreach (var page in pages)
            {
                var canvas = page.Root.GetComponent<Canvas>();
                var size = page.Size;
                int scale = 2;
                int w = Mathf.CeilToInt(size.x * scale), h = Mathf.CeilToInt(size.y * scale);
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                var camGo = new GameObject("ShotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.gray;
                cam.orthographic = true;
                cam.targetTexture = rt;
                var oldMode = canvas.renderMode;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scale;
                foreach (var other in pages) other.Root.gameObject.SetActive(other == page);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(page.Root);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, page.Name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                canvas.renderMode = oldMode;
                UnityEngine.Object.DestroyImmediate(camGo);
                rt.Release();
                Debug.Log($"[M3Batch] shot {page.Name} {w}x{h}");
            }
            foreach (var page in pages) page.Root.gameObject.SetActive(true);
        }
    }
}
