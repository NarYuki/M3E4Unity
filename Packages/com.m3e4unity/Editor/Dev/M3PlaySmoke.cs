#if !UDONSHARP
using System.Collections.Generic;
using System.IO;
using M3E4Unity.Udon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>
    /// Play-mode smoke test of the Showcase (plain Unity): rebuilds the scene, enters play mode
    /// (without a domain reload), shows every page, then drives the runtime behaviours the way input
    /// would (press / release / hover, selection, navigation, sheets, progress, FAB expansion...) while
    /// capturing frames to Shots/smoke_*.png. Any exception or error logged during play fails the run.
    /// pwsh tools/unity_batch.ps1 -Project ./Showcase -Method M3E4Unity.Editor.Dev.M3PlaySmoke.Run
    /// </summary>
    public static class M3PlaySmoke
    {
        static int frame;
        static int errors;
        static float lastActionTime;
        static bool savedOptionsEnabled;
        static EnterPlayModeOptions savedOptions;
        static readonly List<string> messages = new List<string>();

        public static void Run()
        {
            M3Showcase.BuildScene();
            EditorSceneManager.OpenScene(M3Showcase.ScenePath);
            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            Application.logMessageReceived += OnLog;
            frame = 0;
            EditorApplication.update += Step;
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                if (messages.Count < 20) messages.Add(condition + "\n" + stack);
            }
        }

        static T[] All<T>() where T : Component => Object.FindObjectsOfType<T>(true);

        static void Step()
        {
            if (!EditorApplication.isPlaying) return;
            frame++;
            switch (frame)
            {
                case 5:
                    // every page visible so that its behaviours start
                    foreach (var rt in All<RectTransform>())
                        if (rt.name.StartsWith("Page") && !rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
                    break;
                case 20:
                    foreach (var it in All<M3Interactive>()) if (it.isActiveAndEnabled) { it._Enter(); it._Down(); }
                    foreach (var s in All<M3Selection>()) if (s.isActiveAndEnabled) s._Click();
                    foreach (var p in All<M3Progress>()) if (p.isActiveAndEnabled) p.SetProgress(0.8f);
                    foreach (var d in All<M3DatePicker>()) if (d.isActiveAndEnabled) d._Next();
                    foreach (var f in All<M3ExtendedFab>()) if (f.isActiveAndEnabled) f._Toggle();
                    foreach (var sh in All<M3Sheet>()) if (sh.isActiveAndEnabled) sh._Toggle();
                    foreach (var t in All<M3TimePicker>()) if (t.isActiveAndEnabled) t._SelMinute();
                    break;
                case 24:
                    Shot("smoke_pressed");
                    break;
                case 32:
                    foreach (var it in All<M3Interactive>()) if (it.isActiveAndEnabled) { it._Up(); it._Click(); it._Exit(); }
                    foreach (var d in All<M3DatePicker>()) if (d.isActiveAndEnabled) d._Prev();
                    break;
                case 36:
                    Shot("smoke_released");
                    break;
                case 60:
                    foreach (var p in All<M3Progress>()) if (p.isActiveAndEnabled) p.SetProgress(0.05f);
                    foreach (var f in All<M3ExtendedFab>()) if (f.isActiveAndEnabled) f._Toggle();
                    foreach (var sh in All<M3Sheet>()) if (sh.isActiveAndEnabled) sh._Toggle();
                    foreach (var it in All<M3Interactive>()) if (it.isActiveAndEnabled) { it._Down(); it._Up(); }
                    lastActionTime = Time.time;
                    break;
            }
            // everything has settled 2 s after the last input
            if (frame > 60 && Time.time - lastActionTime > 2f)
            {
                Shot("smoke_settled");
                Finish();
            }
        }

        static void Shot(string name)
        {
            var cam = Object.FindObjectOfType<Camera>();
            var canvas = Object.FindObjectOfType<Canvas>();
            if (cam == null || canvas == null) return;
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
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Shots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            rt.Release();
        }

        static void Finish()
        {
            EditorApplication.update -= Step;
            Application.logMessageReceived -= OnLog;
            foreach (var m in messages) Debug.Log("[M3Batch] play error: " + m.Substring(0, Mathf.Min(600, m.Length)));
            Debug.Log($"[M3Batch] play smoke: {frame} frames, {Time.time:F2} s, {errors} errors");
            int code = errors == 0 ? 0 : 1;
            EditorApplication.ExitPlaymode();
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
