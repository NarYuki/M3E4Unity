using System;
using System.IO;
using System.Reflection;
using M3E4Unity.Udon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>
    /// Renders the pull-to-refresh indicators (Indicator and LoadingIndicator) at distance fractions
    /// 0 … 1.75 and while refreshing into Shots/pull_sheet.png (columns = fractions, rows = styles).
    /// </summary>
    public static class M3PullSheet
    {
        static readonly float[] Fractions = { 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1.25f, 1.75f, -1f };

        public static void Run()
        {
            int code = 0;
            try
            {
                M3ShapeAtlas.BeginDynamicScope("PullSheet");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var theme = M3Canvas.DefaultTheme();
                const float w = 96f, h = 140f;
                var size = new Vector2(w * Fractions.Length, h * 2);
                var root = M3Canvas.Create("Pull", theme, size, worldSpace: false);
                M3Canvas.Surface(root);
                var ctx = new M3Context(theme);
                var boxes = new M3PullToRefresh[Fractions.Length * 2];
                for (int s = 0; s < 2; s++)
                for (int i = 0; i < Fractions.Length; i++)
                {
                    var slot = M3Build.Rect("Slot", root);
                    slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                    slot.pivot = new Vector2(0f, 1f);
                    slot.anchoredPosition = new Vector2(w * i, -h * s);
                    slot.sizeDelta = new Vector2(w, h);
                    var box = M3PullToRefreshes.Box(slot, ctx, w - 8f, h - 8f, new string[0], loadingIndicator: s == 1);
                    box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
                    box.anchoredPosition = Vector2.zero;
                    boxes[s * Fractions.Length + i] = box.GetComponent<M3PullToRefresh>();
                }
                M3Canvas.ApplyTheme(root.gameObject);
                M3ShapeAtlas.DynamicScope = null;
                M3ShapeAtlas.Save();
                M3ShapeTiles.Save();
                var t = typeof(M3PullToRefresh);
                const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                for (int k = 0; k < boxes.Length; k++)
                {
                    float f = Fractions[k % Fractions.Length];
                    var b = boxes[k];
                    bool refreshing = f < 0f;
                    b.refreshing = refreshing;
                    t.GetField("fraction", F).SetValue(b, refreshing ? 1f : f);
                    t.GetField("crossfade", F).SetValue(b, refreshing ? 1f : 0f);
                    t.GetField("arrowAlpha", F).SetValue(b, !refreshing && f >= 1f ? 1f : 0.3f);
                    t.GetMethod("Apply", F).Invoke(b, null);
                }

                int scale = 3;
                int pw = Mathf.CeilToInt(size.x * scale), ph = Mathf.CeilToInt(size.y * scale);
                var rtex = new RenderTexture(pw, ph, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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
                Shader.SetGlobalFloat("_M3TimeOverrideOn", 1f);
                Shader.SetGlobalFloat("_M3TimeOverride", 0.4f);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                Shader.SetGlobalFloat("_M3TimeOverrideOn", 0f);
                var prev = RenderTexture.active;
                RenderTexture.active = rtex;
                var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Shots");
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, "pull_sheet.png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(camGo);
                rtex.Release();
                Debug.Log("[M3Batch] pull sheet written");
            }
            catch (Exception e)
            {
                Debug.LogError("[M3Batch] " + e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
