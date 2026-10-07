using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Docked search bar (SearchBar.kt DockedSearchBar + SearchBarDefaults.InputField): a 56 dp
    /// SurfaceContainerHigh field (min 360 dp wide) with leading / trailing icons offset 4 dp inwards,
    /// expanding on focus into an ExtraLarge surface with a divider and suggestions (ListItems).
    /// </summary>
    public static class M3SearchBars
    {
        const float MinWidth = 360f;           // SearchBarMinWidth
        const float IconOffset = 4f;           // SearchBarIconOffsetX
        const float InteractiveSize = 48f;
        const float Padding = 16f;             // TextFieldPadding (contentPaddingWithoutLabel)
        const float ResultsMinHeight = 240f;   // DockedExpandedTableMinHeight

        public static RectTransform Docked(Transform parent, M3Context ctx, string placeholder, string[] suggestions,
            string leadingIcon = "search", string trailingIcon = "more_vert", float width = MinWidth, bool expanded = false)
        {
            width = Mathf.Max(width, MinWidth);
            var bg = SearchBarTokens.ContainerColor;
            var inner = ctx.On(bg);
            float h = SearchBarTokens.ContainerHeight;
            float itemH = ListTokens.ItemOneLineContainerHeight;
            float resultsH = Mathf.Max(ResultsMinHeight, suggestions.Length * itemH);
            float expandedH = h + DividerTokens.Thickness + resultsH;

            var root = M3Build.Rect("DockedSearchBar", parent);
            root.sizeDelta = new Vector2(width, expandedH);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = expandedH;

            var surface = M3Build.Rect("Surface", root);
            surface.anchorMin = surface.anchorMax = new Vector2(0f, 1f);
            surface.pivot = new Vector2(0f, 1f);
            surface.anchoredPosition = Vector2.zero;
            surface.sizeDelta = new Vector2(width, expanded ? expandedH : h);
            M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(SearchViewTokens.DockedContainerShape)), ColorRef.Role(bg));
            surface.gameObject.AddComponent<RectMask2D>();

            // input field row
            var field = M3Build.Rect("InputField", surface);
            Place(field, 0f, 0f, width, h);
            var hit = field.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            float iconPad = (InteractiveSize - 24f) / 2f;
            float textX = Padding, textEnd = width - Padding;
            if (leadingIcon != null)
            {
                var icon = M3Build.Icon("LeadingIcon", field, leadingIcon, 24f, ColorRef.Role(SearchBarTokens.LeadingIconColor), inner);
                Center(icon.rectTransform, InteractiveSize / 2f + IconOffset, h / 2f);
                textX = InteractiveSize + (Padding - iconPad);
            }
            if (trailingIcon != null)
            {
                var icon = M3Build.Icon("TrailingIcon", field, trailingIcon, 24f, ColorRef.Role(SearchBarTokens.TrailingIconColor), inner);
                Center(icon.rectTransform, width - InteractiveSize / 2f - IconOffset, h / 2f);
                textEnd = width - InteractiveSize - (Padding - iconPad);
            }
            var viewport = M3Build.Rect("TextArea", field);
            Place(viewport, textX, (h - 24f) / 2f, textEnd - textX, 24f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = M3Build.Text("Text", viewport, "", SearchBarTokens.InputTextFont, ColorRef.Role(SearchBarTokens.InputTextColor), inner);
            M3Build.Fill(text.rectTransform);
            text.alignment = TextAlignmentOptions.Left;
            var ph = M3Build.Text("Placeholder", viewport, placeholder, SearchBarTokens.SupportingTextFont, ColorRef.Role(SearchBarTokens.SupportingTextColor), inner);
            M3Build.Fill(ph.rectTransform);
            ph.alignment = TextAlignmentOptions.Left;
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = hit;
            input.transition = Selectable.Transition.None;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = text.font;
            input.pointSize = text.fontSize;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            // results: divider + suggestions (ListItems on the search surface color)
            var results = M3Build.Rect("Results", surface);
            Place(results, 0f, h, width, DividerTokens.Thickness + resultsH);
            var group = results.gameObject.AddComponent<CanvasGroup>();
            var divider = M3Build.Shape("Divider", results, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(SearchViewTokens.DividerColor), false);
            Place((RectTransform)divider.transform, 0f, 0f, width, DividerTokens.Thickness);
            var bar = M3UdonBridge.Add<M3SearchBar>(root.gameObject);
            var texts = new List<TextMeshProUGUI>();
            for (int i = 0; i < suggestions.Length && i < 8; i++)
            {
                // the search view's items sit on the search container (ListItemDefaults.colors(containerColor = Transparent) in the samples)
                var item = M3Lists.Item(results, inner, new ListItemSpec { Headline = suggestions[i], LeadingIcon = "history", Container = bg }, width);
                Place(item, 0f, DividerTokens.Thickness + i * itemH, width, itemH);
                M3UdonBridge.Wire(item.GetComponent<Button>().onClick, bar, "_Pick" + i);
                texts.Add(item.Find("Content/Headline").GetComponent<TextMeshProUGUI>());
            }

            bar.input = input;
            bar.surface = surface;
            bar.results = group;
            bar.collapsedHeight = h;
            bar.expandedHeight = expandedH;
            bar.expanded = expanded;
            bar.suggestionTexts = texts.ToArray();
            M3UdonBridge.Wire(input.onSelect, bar, nameof(M3SearchBar._Expand));
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef>
            {
                ColorRef.Role(ColorRole.Primary), ColorRef.Role(ColorRole.Error), ColorRef.Translucent(ColorRole.Primary, 0.4f), // cursor, error cursor, selection
            };
            if (!expanded) results.gameObject.SetActive(false);
            M3UdonBridge.Sync(bar);
            return root;
        }

        /// <summary>
        /// Full-screen search (SearchBar + ExpandedFullScreenSearchBar, FullScreenSearchBarScaffoldSample):
        /// a collapsed SearchBar (CircleShape input field, centered at the top with SearchBarVerticalPadding)
        /// in a screen of screenWidth × screenHeight; focusing it expands to the full-screen layer with a
        /// back button, a divider and the suggestions. M3FullScreenSearchBar drives the transition.
        /// </summary>
        public static RectTransform FullScreen(Transform parent, M3Context ctx, string placeholder, string[] suggestions,
            float screenWidth = 412f, float screenHeight = 915f, bool expanded = false)
        {
            var bg = SearchBarTokens.ContainerColor;
            var inner = ctx.On(bg);
            float h = SearchBarTokens.ContainerHeight;
            float w = Mathf.Min(MinWidth, screenWidth);
            float cx = Mathf.Round((screenWidth - w) / 2f), cy = 8f; // SearchBarVerticalPadding

            var root = M3Build.Rect("FullScreenSearch", parent);
            root.sizeDelta = new Vector2(screenWidth, screenHeight);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = screenWidth;
            le.preferredHeight = le.minHeight = screenHeight;
            var bar = M3UdonBridge.Add<M3FullScreenSearchBar>(root.gameObject);

            // the collapsed SearchBar (SearchBarImpl: Surface(inputFieldShape) around the input field)
            var collapsed = M3Build.Rect("SearchBar", root);
            Place(collapsed, cx, cy, w, h);
            M3Build.Shape("Container", collapsed, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(SearchBarTokens.ContainerShape)), ColorRef.Role(bg));
            var collapsedInput = InputRow(collapsed, inner, placeholder, w, h, "search", null, null, "more_vert");
            M3UdonBridge.Wire(collapsedInput.onSelect, bar, nameof(M3FullScreenSearchBar._Open));
            M3UdonBridge.Wire(collapsedInput.onValueChanged, bar, nameof(M3FullScreenSearchBar._CollapsedChanged));

            // the expanded layer (BasicEdgeToEdgeDialog): surface, input field, content
            var layer = M3Build.Rect("Expanded", root);
            Place(layer, 0f, 0f, screenWidth, screenHeight);
            var surface = M3Build.Rect("Surface", layer);
            var surfaceImg = M3Build.Shape("Container", surface, ShapeCell.Create(ShapeKind.Fill).WithRadius(0f), ColorRef.Role(bg));
            float radius = h / 2f;
            var frames = new Sprite[Mathf.RoundToInt(radius) + 1];
            for (int i = 0; i < frames.Length; i++) frames[i] = M3ShapeAtlas.Get(ShapeCell.Create(ShapeKind.Fill).WithRadius(i));
            var field = M3Build.Rect("InputField", layer);
            var expandedInput = InputRow(field, inner, placeholder, screenWidth, h, null, "arrow_back", bar, "more_vert");
            var fle = field.gameObject.AddComponent<LayoutElement>(); fle.ignoreLayout = true;
            // the row stretches with the animated field width: the trailing icon sticks to the end, the text area fills between
            float iconPadX = (InteractiveSize - 24f) / 2f;
            float textStart = InteractiveSize + (Padding - iconPadX);
            foreach (RectTransform child in field)
            {
                if (child.name == "TrailingIcon")
                {
                    child.anchorMin = child.anchorMax = new Vector2(1f, 1f);
                    child.anchoredPosition = new Vector2(-InteractiveSize / 2f - IconOffset, -h / 2f);
                }
                else if (child.name == "TextArea")
                {
                    child.anchorMin = new Vector2(0f, 1f);
                    child.anchorMax = new Vector2(1f, 1f);
                    child.pivot = new Vector2(0f, 1f);
                    child.offsetMin = new Vector2(textStart, -(h + 24f) / 2f);
                    child.offsetMax = new Vector2(-textStart, -(h - 24f) / 2f);
                }
            }
            M3UdonBridge.Wire(expandedInput.onValueChanged, bar, nameof(M3FullScreenSearchBar._ExpandedChanged));
            var content = M3Build.Rect("Content", layer);
            var group = content.gameObject.AddComponent<CanvasGroup>();
            var divider = M3Build.Shape("Divider", content, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(SearchViewTokens.DividerColor), false);
            var drt = (RectTransform)divider.transform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = Vector2.zero; drt.sizeDelta = new Vector2(0f, DividerTokens.Thickness);
            float itemH = ListTokens.ItemOneLineContainerHeight;
            var texts = new List<TextMeshProUGUI>();
            for (int i = 0; i < suggestions.Length && i < 8; i++)
            {
                var item = M3Lists.Item(content, inner, new ListItemSpec { Headline = suggestions[i], LeadingIcon = "history", Container = bg }, screenWidth);
                Place(item, 0f, DividerTokens.Thickness + i * itemH, screenWidth, itemH);
                M3UdonBridge.Wire(item.GetComponent<Button>().onClick, bar, "_Pick" + i);
                texts.Add(item.Find("Content/Headline").GetComponent<TextMeshProUGUI>());
            }

            bar.expanded = expanded;
            bar.collapsedX = cx; bar.collapsedY = cy; bar.collapsedWidth = w; bar.collapsedHeight = h;
            bar.collapsedInput = collapsedInput;
            bar.layer = layer.gameObject;
            bar.surface = surface;
            bar.surfaceImage = surfaceImg;
            bar.radiusFrames = frames;
            bar.cornerRadius = radius;
            bar.field = field;
            bar.expandedInput = expandedInput;
            bar.content = content;
            bar.contentGroup = group;
            bar.screenWidth = screenWidth;
            bar.screenHeight = screenHeight;
            bar.suggestionTexts = texts.ToArray();
            bar.inputs = new[] { collapsedInput, expandedInput };
            var slow = MotionScheme.Get(ctx.Motion, MotionRole.SlowSpatial);
            var def = MotionScheme.Get(ctx.Motion, MotionRole.DefaultSpatial);
            bar.expandDamping = slow.DampingRatio; bar.expandStiffness = slow.Stiffness;
            bar.collapseDamping = def.DampingRatio; bar.collapseStiffness = def.Stiffness;
            layer.gameObject.SetActive(expanded);
            var sc = root.gameObject.AddComponent<M3SelectionColors>();
            sc.extra = new List<ColorRef> { ColorRef.Role(ColorRole.Primary), ColorRef.Role(ColorRole.Error), ColorRef.Translucent(ColorRole.Primary, 0.4f) };
            M3UdonBridge.Sync(bar);
            return root;
        }

        /// <summary>SearchBarDefaults.InputField: leading icon (or an icon button), text / placeholder, trailing icon.</summary>
        static TMP_InputField InputRow(RectTransform field, M3Context inner, string placeholder, float width, float h,
            string leadingIcon, string leadingButton, Component buttonTarget, string trailingIcon)
        {
            var hit = field.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            float iconPad = (InteractiveSize - 24f) / 2f;
            float textX = Padding, textEnd = width - Padding;
            if (leadingIcon != null)
            {
                var icon = M3Build.Icon("LeadingIcon", field, leadingIcon, 24f, ColorRef.Role(SearchBarTokens.LeadingIconColor), inner);
                Center(icon.rectTransform, InteractiveSize / 2f + IconOffset, h / 2f);
                textX = InteractiveSize + (Padding - iconPad);
            }
            if (leadingButton != null)
            {
                var btn = M3IconButtons.Create(field, inner, leadingButton, contentColor: SearchBarTokens.LeadingIconColor);
                var bl = btn.GetComponent<LayoutElement>(); if (bl != null) bl.ignoreLayout = true;
                Center(btn, InteractiveSize / 2f + IconOffset, h / 2f);
                if (buttonTarget != null) M3UdonBridge.Wire(btn.GetComponent<Button>().onClick, buttonTarget, nameof(M3FullScreenSearchBar._Close));
                textX = InteractiveSize + (Padding - iconPad);
            }
            if (trailingIcon != null)
            {
                var icon = M3Build.Icon("TrailingIcon", field, trailingIcon, 24f, ColorRef.Role(SearchBarTokens.TrailingIconColor), inner);
                Center(icon.rectTransform, width - InteractiveSize / 2f - IconOffset, h / 2f);
                textEnd = width - InteractiveSize - (Padding - iconPad);
            }
            var viewport = M3Build.Rect("TextArea", field);
            Place(viewport, textX, (h - 24f) / 2f, textEnd - textX, 24f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = M3Build.Text("Text", viewport, "", SearchBarTokens.InputTextFont, ColorRef.Role(SearchBarTokens.InputTextColor), inner);
            M3Build.Fill(text.rectTransform);
            text.alignment = TextAlignmentOptions.Left;
            var ph = M3Build.Text("Placeholder", viewport, placeholder, SearchBarTokens.SupportingTextFont, ColorRef.Role(SearchBarTokens.SupportingTextColor), inner);
            M3Build.Fill(ph.rectTransform);
            ph.alignment = TextAlignmentOptions.Left;
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = hit;
            input.transition = Selectable.Transition.None;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = text.font;
            input.pointSize = text.fontSize;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            var l = rt.GetComponent<LayoutElement>();
            if (l != null) l.ignoreLayout = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Center(RectTransform rt, float cx, float cy)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }
    }
}
