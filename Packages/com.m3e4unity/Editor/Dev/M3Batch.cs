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
