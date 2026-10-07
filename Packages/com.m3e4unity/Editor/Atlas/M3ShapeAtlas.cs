using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// The project's parameter atlas for M3Shape.shader. The asset is a half-float texture
    /// (main object) whose 4x4-texel cells hold shape parameters; every cell has a Sprite
    /// sub-asset covering its inner 2x2 texels, and the shared material is a sub-asset too.
    /// Cells are allocated on demand and deduplicated by their parameters, so the registry
    /// can always be rebuilt from the texture itself.
    /// </summary>
    public static class M3ShapeAtlas
    {
        public const string GeneratedFolder = "Assets/M3E4Unity.Generated";
        public const string AtlasPath = GeneratedFolder + "/M3ShapeAtlas.asset";
        public const int TextureSize = 512;
        const int CellSize = 4;
        const int CellsPerRow = TextureSize / CellSize;
        const int MaxCells = CellsPerRow * CellsPerRow;

        static Texture2D texture;
        static Material material;
        static Dictionary<string, Sprite> spritesByKey;
        static int nextCell;
        static bool dirty;

        public static Texture2D Texture { get { Load(); return texture; } }
        public static Material Material { get { Load(); return material; } }
        public static int CellCount { get { Load(); return nextCell; } }

        static void Load()
        {
            if (texture != null && spritesByKey != null) return;
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture == null)
            {
                Create();
                return;
            }
            spritesByKey = new Dictionary<string, Sprite>();
            nextCell = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
            {
                if (o is Material m) material = m;
                if (o is Sprite s)
                {
                    int cell = CellOf(s);
                    var key = ReadCell(cell).Key();
                    spritesByKey[key] = s;
                    nextCell = Mathf.Max(nextCell, cell + 1);
                }
            }
            if (material == null) material = CreateMaterial();
            M3ShapeTiles.BindMaterial(material);
        }

        static void Create()
        {
            Directory.CreateDirectory(GeneratedFolder);
            texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBAHalf, false, true)
            {
                name = "M3ShapeAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            var clear = new Color[TextureSize * TextureSize];
            texture.SetPixels(clear);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, AtlasPath);
            material = CreateMaterial();
            spritesByKey = new Dictionary<string, Sprite>();
            nextCell = 0;
            AssetDatabase.SaveAssets();
        }

        static Material CreateMaterial()
        {
            var shader = Shader.Find("M3E4Unity/UI/Shape");
            if (shader == null) throw new System.InvalidOperationException("M3E4Unity/UI/Shape shader not found");
            var m = new Material(shader) { name = "M3Shape" };
            AssetDatabase.AddObjectToAsset(m, AtlasPath);
            M3ShapeTiles.BindMaterial(m);
            return m;
        }

        static int CellOf(Sprite s)
        {
            var r = s.rect;
            int cx = Mathf.FloorToInt(r.x / CellSize);
            int cy = Mathf.FloorToInt(r.y / CellSize);
            return cy * CellsPerRow + cx;
        }

        static ShapeCell ReadCell(int cell)
        {
            int x0 = (cell % CellsPerRow) * CellSize, y0 = (cell / CellsPerRow) * CellSize;
            var c = new ShapeCell { V = new float[24] };
            for (int i = 0; i < 6; i++)
            {
                var px = texture.GetPixel(x0 + i % 4, y0 + i / 4);
                c.V[i * 4] = px.r; c.V[i * 4 + 1] = px.g; c.V[i * 4 + 2] = px.b; c.V[i * 4 + 3] = px.a;
            }
            // half-float storage rounds; normalise the key the same way as freshly written cells
            for (int i = 0; i < 24; i++) c.V[i] = Mathf.HalfToFloat(Mathf.FloatToHalf(c.V[i]));
            return c;
        }

        static string NormalisedKey(ShapeCell c)
        {
            var n = new ShapeCell { V = new float[24] };
            for (int i = 0; i < 24; i++) n.V[i] = Mathf.HalfToFloat(Mathf.FloatToHalf(c.V[i]));
            return n.Key();
        }

        /// <summary>The sprite for a shape, allocating an atlas cell the first time.</summary>
        public static Sprite Get(ShapeCell cell)
        {
            Load();
            var key = NormalisedKey(cell);
            if (spritesByKey.TryGetValue(key, out var existing) && existing != null) return existing;
            if (nextCell >= MaxCells) throw new System.InvalidOperationException("M3ShapeAtlas is full");
            int index = nextCell++;
            int x0 = (index % CellsPerRow) * CellSize, y0 = (index / CellsPerRow) * CellSize;
            for (int j = 0; j < CellSize; j++)
            for (int i = 0; i < CellSize; i++)
            {
                int t = j * 4 + i;
                // texels 0..5 carry the parameters; the rest repeat P(0,0) (unused)
                Color px = t < 6
                    ? new Color(cell.V[t * 4], cell.V[t * 4 + 1], cell.V[t * 4 + 2], cell.V[t * 4 + 3])
                    : new Color(cell.V[0], cell.V[1], cell.V[2], cell.V[3]);
                texture.SetPixel(x0 + i, y0 + j, px);
            }
            var sprite = Sprite.Create(texture, new Rect(x0 + 1, y0 + 1, 2, 2), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = $"cell{index:D5} {cell.Kind}";
            AssetDatabase.AddObjectToAsset(sprite, AtlasPath);
            spritesByKey[key] = sprite;
            dirty = true;
            EditorUtility.SetDirty(texture);
            return sprite;
        }

        /// <summary>Writes pending changes; call once after building UI.</summary>
        public static void Save()
        {
            if (!dirty) return;
            dirty = false;
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Forgets cached state (after the asset was deleted or reimported).</summary>
        public static void Reset()
        {
            texture = null;
            material = null;
            spritesByKey = null;
        }
    }
}
