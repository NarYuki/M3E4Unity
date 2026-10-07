using System.Collections.Generic;
using M3E4Unity.Tokens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Context for building M3 UI: the theme and the color the component sits on.</summary>
    public sealed class M3Context
    {
        public M3ThemeAsset Theme;
        /// <summary>Role of the surface behind the component (used to pre-composite translucent colors).</summary>
        public ColorRole Background = ColorRole.Surface;

        public M3Context(M3ThemeAsset theme, ColorRole background = ColorRole.Surface)
        {
            Theme = theme;
            Background = background;
        }

        public M3Context On(ColorRole background) => new M3Context(Theme, background);
        public MotionSchemeKind Motion => Theme != null ? Theme.motionScheme : MotionSchemeKind.Expressive;
    }

    /// <summary>Low-level helpers shared by all component builders.</summary>
    public static class M3Build
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return rt;
        }

        /// <summary>Stretches a child over its parent with the given insets (dp).</summary>
        public static RectTransform Fill(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Size(RectTransform rt, float width, float height)
        {
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>An Image drawing an atlas shape, colored from the theme.</summary>
        public static Image Shape(string name, Transform parent, ShapeCell cell, ColorRef color, bool fill = true)
        {
            var rt = Rect(name, parent);
            if (fill) Fill(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = M3ShapeAtlas.Get(cell);
            img.material = M3ShapeAtlas.Material;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            Bind(img, color);
            return img;
        }

        /// <summary>Same atlas cell on an existing Image.</summary>
        public static void SetShape(Image img, ShapeCell cell)
        {
            img.sprite = M3ShapeAtlas.Get(cell);
            img.material = M3ShapeAtlas.Material;
        }

        public static void Bind(Graphic graphic, ColorRef color)
        {
            var b = graphic.GetComponent<M3ColorBinding>();
            if (b == null) b = graphic.gameObject.AddComponent<M3ColorBinding>();
            b.color = color;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, TypeRole role, ColorRef color, M3Context ctx)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            M3Fonts.ApplyStyle(t, role, ctx.Theme);
            t.text = text;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
            Bind(t, color);
            return t;
        }

        /// <summary>Height and first / last baseline (distance from the top) of text wrapped at width (paddingFromBaseline).</summary>
        public static (float height, float firstBaseline, float lastBaseline) MeasureText(TextMeshProUGUI t, string text, float width)
        {
            // baselines are measured from the top of the text, whatever the caller's alignment is
            var alignment = t.alignment;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.rectTransform.sizeDelta = new Vector2(width, 10000f);
            t.text = text;
            t.ForceMeshUpdate();
            var info = t.textInfo;
            float h = Mathf.Ceil(t.GetPreferredValues(text, width, 0f).y);
            float top = t.rectTransform.rect.yMax;
            float first = info.lineCount > 0 ? top - info.lineInfo[0].baseline : 0f;
            float last = info.lineCount > 0 ? top - info.lineInfo[info.lineCount - 1].baseline : 0f;
            t.alignment = alignment;
            return (h, first, last);
        }

        /// <summary>A Material Symbols icon of the given size (dp).</summary>
        public static TextMeshProUGUI Icon(string name, Transform parent, string icon, float size, ColorRef color, M3Context ctx, bool filled = false)
        {
            var rt = Rect(name, parent);
            Size(rt, size, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = filled ? ctx.Theme.iconFontFilled : ctx.Theme.iconFont;
            if (font == null) font = M3Fonts.Icons(filled);
            t.font = font;
            t.fontSharedMaterial = font.material;
            t.fontSize = size;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = false;
            t.raycastTarget = false;
            t.text = M3Icons.Glyph(icon);
            Bind(t, color);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size;
            le.minHeight = le.preferredHeight = size;
            return t;
        }

        /// <summary>
        /// Keeps a content-sized component sized when it is not driven by a parent layout group
        /// (e.g. a filter chip that grows when its check mark appears).
        /// </summary>
        public static void AutoSize(RectTransform rt, bool horizontal = true, bool vertical = false)
        {
            if (rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null) return;
            var fit = rt.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            if (horizontal) fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (vertical) fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>Sprite frames of a shape layer for corner radii min..max in 0.5 dp steps.</summary>
        public static Sprite[] RadiusFrames(System.Func<float, ShapeCell> cellForRadius, float min, float max, out int count)
        {
            count = Mathf.Max(2, Mathf.CeilToInt((max - min) / 0.5f) + 1);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                float r = count == 1 ? min : Mathf.Lerp(min, max, i / (float)(count - 1));
                frames[i] = M3ShapeAtlas.Get(cellForRadius(r));
            }
            return frames;
        }

        /// <summary>Adds the two material-web elevation shadows (key, ambient) behind a container.</summary>
        public static Image[] Shadows(Transform parent, CornerShape shape, int level, int siblingIndex)
        {
            var result = new Image[2];
            for (int layer = 0; layer < 2; layer++)
            {
                var cell = ShadowCell(shape, level, layer, out float margin);
                var img = Shape(layer == 0 ? "ShadowKey" : "ShadowAmbient", parent, cell, ShadowColor(layer));
                Fill((RectTransform)img.transform, -margin, -margin, -margin, -margin);
                img.transform.SetSiblingIndex(siblingIndex + layer);
                result[layer] = img;
            }
            return result;
        }

        /// <summary>Key shadow 30%, ambient shadow 15% of the shadow color (material-web).</summary>
        public static ColorRef ShadowColor(int layer) => ColorRef.Shadow(layer == 0 ? 0.3f : 0.15f);

        /// <summary>The atlas cell for one shadow layer at an elevation level; the quad is grown by margin.</summary>
        public static ShapeCell ShadowCell(CornerShape shape, int level, int layer, out float margin)
        {
            Elevation.Shadows(level, out var key, out var ambient);
            var s = layer == 0 ? key : ambient;
            float sigma = s.Blur * 0.5f;
            margin = 12f + 8f; // fits the largest level (blur 12, spread 6, y 8) at 3 sigma
            var cell = ShapeCell.Create(ShapeKind.Shadow).WithShape(shape)
                .WithBlur(sigma, s.Spread)
                .WithInsets(margin, margin - s.OffsetY, margin, margin + s.OffsetY);
            if (level <= 0) cell = cell.WithBlur(0, -1000f); // empty
            return cell;
        }
    }
}
