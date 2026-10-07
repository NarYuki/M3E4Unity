using System;
using System.Collections.Generic;
using System.IO;
using M3E4Unity.Shapes;
using UnityEditor;
using UnityEngine;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Signed-distance tiles for shapes that are not rounded rectangles: the 35 MaterialShapes,
    /// LoadingIndicator morph frames and the wavy circular indicator paths. Tiles are baked from
    /// the ported graphics-shapes geometry into an RG half-float texture
    /// (R = signed distance in shape units, G = arc-length parameter of the closest point).
    /// </summary>
    public static class M3ShapeTiles
    {
        public const string TilesPath = M3ShapeAtlas.GeneratedFolder + "/M3ShapeTiles.asset";
        public const int TilePx = 48;
        public const int TexturePx = 1024;
        public const int TilesPerRow = TexturePx / TilePx;
        public const int Pad = 2;
        const int MaxTiles = TilesPerRow * TilesPerRow;

        static Texture2D texture;
        static TextAsset registryAsset;
        static Dictionary<string, int> registry;
        static bool dirty;

        [Serializable]
        class Registry
        {
            public List<string> keys = new List<string>();
        }

        public static Texture2D Texture { get { Load(); return texture; } }

        static void Load()
        {
            if (texture != null && registry != null) return;
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesPath);
            if (texture == null)
            {
                Directory.CreateDirectory(M3ShapeAtlas.GeneratedFolder);
                texture = new Texture2D(TexturePx, TexturePx, TextureFormat.RGHalf, false, true)
                {
                    name = "M3ShapeTiles",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var fill = new Color[TexturePx * TexturePx];
                for (int i = 0; i < fill.Length; i++) fill[i] = new Color(1f, 0f, 0f, 0f);
                texture.SetPixels(fill);
                texture.Apply(false, false);
                AssetDatabase.CreateAsset(texture, TilesPath);
                registryAsset = new TextAsset(JsonUtility.ToJson(new Registry())) { name = "M3ShapeTilesRegistry" };
                AssetDatabase.AddObjectToAsset(registryAsset, TilesPath);
                AssetDatabase.SaveAssets();
            }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(TilesPath))
                if (o is TextAsset t) registryAsset = t;
            registry = new Dictionary<string, int>();
            if (registryAsset != null)
            {
                var r = JsonUtility.FromJson<Registry>(registryAsset.text) ?? new Registry();
                for (int i = 0; i < r.keys.Count; i++) registry[r.keys[i]] = i;
            }
        }

        /// <summary>Sets the tile texture and grid on an M3Shape material.</summary>
        public static void BindMaterial(Material m)
        {
            Load();
            m.SetTexture("_ShapeTex", texture);
            m.SetVector("_ShapeGrid", new Vector4(TilesPerRow, TilePx, TexturePx, 0));
        }

        /// <summary>Tile for a filled shape given as closed cubic outlines in the unit square (y down, Compose space).</summary>
        public static int GetFilled(string key, IReadOnlyList<Cubic> cubics)
        {
            Load();
            if (registry.TryGetValue(key, out int tile)) return tile;
            tile = Allocate(key);
            var poly = Flatten(cubics, 24, out _);
            Bake(tile, poly, null, filled: true);
            return tile;
        }

        /// <summary>Tile for a stroked path: R = unsigned distance to the path, G = arc-length parameter.</summary>
        public static int GetPath(string key, IReadOnlyList<Cubic> cubics)
        {
            Load();
            if (registry.TryGetValue(key, out int tile)) return tile;
            tile = Allocate(key);
            var poly = Flatten(cubics, 24, out var param);
            Bake(tile, poly, param, filled: false);
            return tile;
        }

        public static bool Has(string key)
        {
            Load();
            return registry.ContainsKey(key);
        }

        static int Allocate(string key)
        {
            int tile = registry.Count;
            if (tile >= MaxTiles) throw new InvalidOperationException("M3ShapeTiles is full");
            registry[key] = tile;
            dirty = true;
            return tile;
        }

        /// <summary>Flattens closed cubics (unit square, y down) to a polyline in y-up tile space.</summary>
        static List<Vector2> Flatten(IReadOnlyList<Cubic> cubics, int stepsPerCubic, out List<float> arcParam)
        {
            var pts = new List<Vector2>();
            foreach (var c in cubics)
            {
                for (int s = 0; s < stepsPerCubic; s++)
                {
                    float t = s / (float)stepsPerCubic, u = 1 - t;
                    float x = c.Anchor0X * u * u * u + c.Control0X * 3 * t * u * u + c.Control1X * 3 * t * t * u + c.Anchor1X * t * t * t;
                    float y = c.Anchor0Y * u * u * u + c.Control0Y * 3 * t * u * u + c.Control1Y * 3 * t * t * u + c.Anchor1Y * t * t * t;
                    pts.Add(new Vector2(x, 1f - y));
                }
            }
            arcParam = new List<float>(pts.Count + 1) { 0f };
            float total = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                total += Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
                arcParam.Add(total);
            }
            for (int i = 0; i < arcParam.Count; i++) arcParam[i] /= Mathf.Max(total, 1e-6f);
            return pts;
        }

        static void Bake(int tile, List<Vector2> poly, List<float> arcParam, bool filled)
        {
            int col = tile % TilesPerRow, row = tile / TilesPerRow;
            var block = new Color[TilePx * TilePx];
            float inner = TilePx - 2 * Pad;
            int n = poly.Count;
            for (int j = 0; j < TilePx; j++)
            for (int i = 0; i < TilePx; i++)
            {
                var q = new Vector2((i + 0.5f - Pad) / inner, (j + 0.5f - Pad) / inner);
                float best = float.MaxValue, bestParam = 0f;
                int winding = 0;
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = poly[k], b = poly[(k + 1) % n];
                    Vector2 ab = b - a;
                    float len2 = Vector2.Dot(ab, ab);
                    float h = len2 > 0 ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                    float d = (q - a - ab * h).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        if (arcParam != null) bestParam = Mathf.Lerp(arcParam[k], arcParam[k + 1], h);
                    }
                    // winding number (non-zero rule, like Compose paths)
                    if (a.y <= q.y)
                    {
                        if (b.y > q.y && Cross(ab, q - a) > 0) winding++;
                    }
                    else if (b.y <= q.y && Cross(ab, q - a) < 0) winding--;
                }
                float dist = Mathf.Sqrt(best);
                if (filled && winding != 0) dist = -dist;
                block[j * TilePx + i] = new Color(dist, bestParam, 0, 0);
            }
            texture.SetPixels(col * TilePx, row * TilePx, TilePx, TilePx, block);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static void Save()
        {
            if (!dirty) return;
            dirty = false;
            texture.Apply(false, false);
            var keys = new string[registry.Count];
            foreach (var kv in registry) keys[kv.Value] = kv.Key;
            var json = JsonUtility.ToJson(new Registry { keys = new List<string>(keys) });
            if (registryAsset != null) AssetDatabase.RemoveObjectFromAsset(registryAsset);
            registryAsset = new TextAsset(json) { name = "M3ShapeTilesRegistry" };
            AssetDatabase.AddObjectToAsset(registryAsset, TilesPath);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }

        public static void Reset()
        {
            texture = null;
            registry = null;
            registryAsset = null;
        }
    }
}
