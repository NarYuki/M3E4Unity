#if !UDONSHARP
using System.IO;
using M3E4Unity.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>
    /// Records the Showcase in play mode: shows page M3_CAPTURE_PAGE (default 3, progress) and saves
    /// a frame every M3_CAPTURE_STEP seconds (default 0.05) for M3_CAPTURE_COUNT frames (default 60)
    /// into Showcase/Shots/capture/NNN.png, timed by the real play-mode clock.
    /// </summary>
    public static class M3PlayCapture
    {
        static int page, count, taken, frame;
        static float step, next;
        static bool savedEnabled;
        static EnterPlayModeOptions savedOptions;

        public static void Run()
        {
            page = int.TryParse(System.Environment.GetEnvironmentVariable("M3_CAPTURE_PAGE"), out var p) ? p : 3;
            step = float.TryParse(System.Environment.GetEnvironmentVariable("M3_CAPTURE_STEP"), out var s) ? s : 0.05f;
            count = int.TryParse(System.Environment.GetEnvironmentVariable("M3_CAPTURE_COUNT"), out var c) ? c : 60;
            if (!File.Exists(M3Showcase.ScenePath)) M3Showcase.BuildScene();
            EditorSceneManager.OpenScene(M3Showcase.ScenePath);
            savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            taken = 0; frame = 0;
            string dir = Dir();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            EditorApplication.update += Step;
            EditorApplication.EnterPlaymode();
        }

        static string Dir() => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Shots", "capture");

        static void Step()
        {
            if (!EditorApplication.isPlaying) return;
            frame++;
            if (frame == 5)
            {
                Object.FindObjectOfType<M3ShowcaseApp>().ShowPage(page);
                next = Time.time + 0.5f;
                return;
            }
            if (frame < 5 || Time.time < next) return;
            next += step;
            Shot(Path.Combine(Dir(), taken.ToString("D3") + ".png"));
            taken++;
            if (taken >= count)
            {
                EditorApplication.update -= Step;
                EditorApplication.ExitPlaymode();
                EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
                EditorSettings.enterPlayModeOptions = savedOptions;
                Debug.Log($"[M3Batch] captured {taken} frames");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }

        static void Shot(string path)
        {
            var cam = Object.FindObjectOfType<Camera>();
            var canvas = Object.FindObjectOfType<Canvas>();
            var rt = new RenderTexture(1600, 900, 24);
            var prevMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            canvas.renderMode = prevMode;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            rt.Release();
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
