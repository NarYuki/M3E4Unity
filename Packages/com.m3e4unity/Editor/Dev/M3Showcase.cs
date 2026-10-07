#if !UDONSHARP
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using M3E4Unity.MaterialColor;
using M3E4Unity.Showcase;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>
    /// Builds the showcase app (plain Unity projects only): one screen-space canvas with every catalog
    /// page, a navigation list, a theme switcher (all seed × variant × light / dark × contrast schemes
    /// are baked) and an event log; and a Windows player of it.
    /// </summary>
    public static class M3Showcase
    {
        const string Folder = "Assets/M3Showcase";
        public const string ScenePath = Folder + "/Showcase.unity";
        const string ThemePath = Folder + "/ShowcaseTheme.asset";
        const float NavWidth = 300f, TopBarHeight = 72f, LogHeight = 200f;

        static readonly (string name, Color seed)[] Seeds =
        {
            ("Purple", new Color32(0x67, 0x50, 0xA4, 0xFF)),   // the baseline seed
            ("Blue", new Color32(0x1B, 0x6E, 0xF3, 0xFF)),
            ("Green", new Color32(0x38, 0x6A, 0x20, 0xFF)),
            ("Orange", new Color32(0xE8, 0x71, 0x0A, 0xFF)),
            ("Pink", new Color32(0xD8, 0x1B, 0x60, 0xFF)),
        };

        static readonly (string name, Variant variant)[] Variants =
        {
            ("Tonal spot", Variant.TONAL_SPOT), ("Expressive", Variant.EXPRESSIVE), ("Vibrant", Variant.VIBRANT),
            ("Neutral", Variant.NEUTRAL), ("Monochrome", Variant.MONOCHROME), ("Fidelity", Variant.FIDELITY),
            ("Content", Variant.CONTENT), ("Rainbow", Variant.RAINBOW), ("Fruit salad", Variant.FRUIT_SALAD),
        };

        static readonly (string name, float level)[] Contrasts = { ("Standard", 0f), ("Medium", 0.5f), ("High", 1f) };

        static readonly Dictionary<string, (string title, string icon)> Titles = new Dictionary<string, (string, string)>
        {
            { "buttons", ("Buttons", "smart_button") }, { "iconbuttons_fabs", ("Icon buttons & FABs", "add_circle") },
            { "selection", ("Checkbox, radio & switch", "check_box") }, { "progress", ("Progress & loading", "progress_activity") },
            { "sliders", ("Sliders", "tune") }, { "containment", ("Cards, badges & dividers", "dashboard") },
            { "groups_chips", ("Button groups & chips", "label") }, { "lists", ("Lists", "list") },
            { "textfields", ("Text fields", "text_fields") }, { "menus", ("Menus", "menu_open") },
            { "dialogs", ("Dialogs", "chat_bubble") }, { "snackbars", ("Snackbars", "info") },
            { "tooltips", ("Tooltips", "help") }, { "navigation_bar", ("Navigation bar", "bottom_navigation") },
            { "navigation_rail", ("Navigation rail", "side_navigation") }, { "tabs", ("Tabs", "tab") },
            { "app_bars", ("Top app bars", "web_asset") }, { "toolbars", ("Toolbars", "construction") },
            { "segmented", ("Segmented buttons", "view_week") }, { "sheets_drawers", ("Sheets & drawers", "vertical_split") },
            { "date_picker", ("Date picker", "calendar_month") }, { "date_range_picker", ("Date range picker", "date_range") },
            { "time_picker", ("Time pickers", "schedule") }, { "fab_menu", ("FAB menu", "more_horiz") },
            { "carousel", ("Carousel", "view_carousel") }, { "search", ("Search", "search") },
        };

        [MenuItem("Tools/M3E4Unity/Showcase/Build Showcase Scene", priority = 60)]
        public static void BuildSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        [MenuItem("Tools/M3E4Unity/Showcase/Build Windows App", priority = 61)]
        public static void BuildPlayerMenu()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            var path = BuildPlayer(DefaultOutput());
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Batch entry: builds the scene and the Windows player, then exits.</summary>
        public static void BatchBuild()
        {
            int code = 0;
            try
            {
                BuildScene();
                var exe = BuildPlayer(DefaultOutput());
                Debug.Log("[M3Batch] showcase built: " + exe);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        static string DefaultOutput()
        {
            var env = Environment.GetEnvironmentVariable("M3_SHOWCASE_OUT");
            if (!string.IsNullOrEmpty(env)) return env;
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Build", "M3E4Unity Showcase.exe");
        }

        static M3ThemeAsset ShowcaseTheme()
        {
            Directory.CreateDirectory(Folder);
            var theme = AssetDatabase.LoadAssetAtPath<M3ThemeAsset>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<M3ThemeAsset>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            // scheme index = ((seed * variants + variant) * 2 + dark) * contrasts + contrast
            theme.schemes = new List<M3SchemeEntry>();
            foreach (var (seedName, seed) in Seeds)
                foreach (var (variantName, variant) in Variants)
                    foreach (bool dark in new[] { false, true })
                        foreach (var (contrastName, level) in Contrasts)
                            theme.schemes.Add(new M3SchemeEntry
                            {
                                name = $"{seedName} {variantName} {(dark ? "Dark" : "Light")} {contrastName}",
                                seed = seed, variant = variant, dark = dark, contrast = level,
                            });
            theme.defaultScheme = 0;
            M3Fonts.FillTheme(theme);
            EditorUtility.SetDirty(theme);
            return theme;
        }

        public static void BuildScene()
        {
            Directory.CreateDirectory(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var theme = ShowcaseTheme();
            var ctx = new M3Context(theme);
            var root = M3Canvas.Create("Showcase", theme, new Vector2(1600, 900), worldSpace: false);
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            M3Canvas.Surface(root);
            var app = root.gameObject.AddComponent<M3ShowcaseApp>();
            app.theme = root.GetComponent<M3Theme>();
            app.seedCount = Seeds.Length;
            app.variantCount = Variants.Length;
            app.contrastCount = Contrasts.Length;
            app.variantNames = Array.ConvertAll(Variants, v => v.name);

            // ---- content: one page per catalog section in a two-way scroll view ----
            var area = M3Build.Rect("ContentArea", root);
            Stretch(area, NavWidth, TopBarHeight, 0f, 0f);
            area.gameObject.AddComponent<RectMask2D>();
            var catcher = area.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            var holder = M3Build.Rect("Pages", area);
            holder.anchorMin = holder.anchorMax = new Vector2(0f, 1f);
            holder.pivot = new Vector2(0f, 1f);
            holder.anchoredPosition = Vector2.zero;
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = holder;
            scroll.viewport = area;
            scroll.horizontal = scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            app.contentScroll = scroll;
            app.pageHolder = holder;

            int n = M3Catalog.SectionCount;
            var pages = new GameObject[n];
            var titles = new string[n];
            var sizes = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                string key = M3Catalog.SectionName(i);
                titles[i] = Titles.TryGetValue(key, out var t) ? t.title : key;
                sizes[i] = M3Catalog.SectionSize(i);
                var page = M3Build.Rect(titles[i], holder);
                page.anchorMin = page.anchorMax = new Vector2(0f, 1f);
                page.pivot = new Vector2(0f, 1f);
                page.anchoredPosition = Vector2.zero;
                page.sizeDelta = sizes[i];
                var content = M3Build.Rect("Content", page);
                M3Build.Fill(content, 24, 24, 24, 24);
                M3Build.Column(content, 24);
                M3Catalog.BuildSection(i, content, ctx);
                pages[i] = page.gameObject;
            }
            app.pages = pages;
            app.pageTitles = titles;
            app.pageSizes = sizes;

            // ---- navigation panel ----
            var nav = M3Build.Rect("Navigation", root);
            Stretch(nav, 0f, 0f, -1f, 0f, NavWidth);
            M3Build.Shape("Container", nav, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(ColorRole.SurfaceContainerLow));
            var navCtx = ctx.On(ColorRole.SurfaceContainerLow);
            var brand = M3Build.Text("Brand", nav, "M3E4Unity", TypeRole.TitleLarge, ColorRef.Role(ColorRole.OnSurface), navCtx);
            brand.alignment = TextAlignmentOptions.TopLeft;
            TopLeft(brand.rectTransform, 28f, 24f, NavWidth - 40f, 28f);
            var tagline = M3Build.Text("Tagline", nav, "Material 3 Expressive for Unity — showcase", TypeRole.BodySmall, ColorRef.Role(ColorRole.OnSurfaceVariant), navCtx);
            tagline.alignment = TextAlignmentOptions.TopLeft;
            TopLeft(tagline.rectTransform, 28f, 54f, NavWidth - 40f, 18f);

            var listArea = M3Build.Rect("Destinations", nav);
            Stretch(listArea, 0f, 88f, 0f, LogHeight);
            listArea.gameObject.AddComponent<RectMask2D>();
            var listCatch = listArea.gameObject.AddComponent<Image>();
            listCatch.color = Color.clear;
            var list = M3Build.Rect("List", listArea);
            list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            const float itemH = 48f;
            list.sizeDelta = new Vector2(0f, n * itemH + 12f);
            var listScroll = listArea.gameObject.AddComponent<ScrollRect>();
            listScroll.content = list; listScroll.viewport = listArea;
            listScroll.horizontal = false; listScroll.movementType = ScrollRect.MovementType.Clamped; listScroll.scrollSensitivity = 40f;

            // the selected destination's indicator (NavigationDrawerItem: SecondaryContainer pill)
            var indicator = M3Build.Shape("Indicator", list, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Role(ColorRole.SecondaryContainer), false);
            indicator.raycastTarget = false;
            app.navIndicator = (RectTransform)indicator.transform;

            var items = new RectTransform[n];
            var labels = new TextMeshProUGUI[n];
            var pill = ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full);
            for (int i = 0; i < n; i++)
            {
                string key = M3Catalog.SectionName(i);
                string icon = Titles.TryGetValue(key, out var t) ? t.icon : "widgets";
                var item = M3Build.Rect("Item " + titles[i], list);
                TopLeft(item, 0f, 6f + i * itemH, NavWidth, itemH);
                var body = M3Build.Rect("Body", item);
                Stretch(body, 12f, 0f, 12f, 0f);
                var stateLayer = M3Build.Shape("StateLayer", body, pill, ColorRef.Clear);
                var ripple = M3Build.Shape("Ripple", body, ShapeCell.Create(ShapeKind.Ripple).WithShape(CornerShape.Full), ColorRef.Clear);
                var ic = M3Build.Icon("Icon", body, icon, 24f, ColorRef.Role(ColorRole.OnSurfaceVariant), navCtx);
                ic.raycastTarget = false;
                TopLeft(ic.rectTransform, 16f, 12f, 24f, 24f);
                var label = M3Build.Text("Label", body, titles[i], TypeRole.LabelLarge, ColorRef.Role(ColorRole.OnSurfaceVariant), navCtx);
                label.alignment = TextAlignmentOptions.Left;
                label.raycastTarget = false;
                TopLeft(label.rectTransform, 52f, 0f, NavWidth - 24f - 64f, itemH);
                var spec = new InteractionSpec
                {
                    Root = body, Context = navCtx, StateLayer = stateLayer, Ripple = ripple, RippleClipShape = CornerShape.Full,
                    RestShape = 24f, PressedShape = 24f, SelectedShape = 24f, SelectedPressedShape = 24f, MaxShape = 24f,
                };
                spec.ShapeLayers.Add((stateLayer, _ => pill));
                spec.Color(stateLayer, ColorRef.Over(ColorRole.SurfaceContainerLow, ColorRole.OnSurfaceVariant, StateLayer.Hover));
                spec.RippleColor = ColorRef.Over(ColorRole.SurfaceContainerLow, ColorRole.OnSurfaceVariant, StateLayer.Hover, ColorRole.OnSurfaceVariant, StateLayer.Pressed);
                M3Interaction.Apply(spec);
                UnityEventTools.AddIntPersistentListener(body.GetComponent<Button>().onClick, app.ShowPage, i);
                items[i] = item;
                labels[i] = label;
            }
            app.navItems = items;
            app.navLabels = labels;

            // event log
            var logBox = M3Build.Rect("EventLog", nav);
            Stretch(logBox, 16f, -1f, 16f, 16f, 0f, LogHeight - 24f);
            M3Build.Shape("Container", logBox, ShapeCell.Create(ShapeKind.Fill).WithRadius(16f), ColorRef.Role(ColorRole.SurfaceContainerHighest));
            var logCtx = ctx.On(ColorRole.SurfaceContainerHighest);
            var logTitle = M3Build.Text("Title", logBox, "Event log", TypeRole.LabelLarge, ColorRef.Role(ColorRole.OnSurface), logCtx);
            logTitle.alignment = TextAlignmentOptions.TopLeft;
            TopLeft(logTitle.rectTransform, 16f, 12f, NavWidth - 64f, 20f);
            var logText = M3Build.Text("Lines", logBox, "", TypeRole.BodySmall, ColorRef.Role(ColorRole.OnSurfaceVariant), logCtx);
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.enableWordWrapping = false;
            logText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(logText.rectTransform, 16f, 36f, 16f, 12f);
            app.logText = logText;
            app.logLines = 8;

            // ---- top bar: page title + theme switcher ----
            var bar = M3Build.Rect("TopBar", root);
            Stretch(bar, NavWidth, 0f, 0f, -1f, 0f, TopBarHeight);
            M3Build.Shape("Container", bar, ShapeCell.Create(ShapeKind.Fill), ColorRef.Role(ColorRole.Surface));
            var title = M3Build.Text("Title", bar, titles[0], TypeRole.HeadlineSmall, ColorRef.Role(ColorRole.OnSurface), ctx);
            title.alignment = TextAlignmentOptions.Left;
            const float titleW = 420f;
            TopLeft(title.rectTransform, 24f, 0f, titleW, TopBarHeight);
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;
            app.pageTitle = title;

            // the theme switcher (right side of the bar); it scrolls sideways when the window is too narrow for it
            var controlsView = M3Build.Rect("ThemeView", bar);
            Stretch(controlsView, 24f + titleW, 0f, 24f, 0f);
            controlsView.gameObject.AddComponent<RectMask2D>();
            var controlsCatch = controlsView.gameObject.AddComponent<Image>();
            controlsCatch.color = Color.clear;
            var controls = M3Build.Rect("Theme", controlsView);
            controls.anchorMin = controls.anchorMax = new Vector2(1f, 0.5f);
            controls.pivot = new Vector2(1f, 0.5f);
            controls.anchoredPosition = Vector2.zero;
            var controlsScroll = controlsView.gameObject.AddComponent<ScrollRect>();
            controlsScroll.content = controls; controlsScroll.viewport = controlsView;
            controlsScroll.vertical = false; controlsScroll.horizontal = true;
            controlsScroll.movementType = ScrollRect.MovementType.Clamped; controlsScroll.scrollSensitivity = 40f;
            app.themeView = controlsView;
            app.themeContent = controls;
            var row = M3Build.Row(controls, 12f, TextAnchor.MiddleRight);
            row.childControlWidth = false; row.childControlHeight = false;
            var fitter = controls.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // seed swatches
            var rings = new RectTransform[Seeds.Length];
            for (int i = 0; i < Seeds.Length; i++)
            {
                var sw = M3Build.Rect("Seed " + Seeds[i].name, controls);
                sw.sizeDelta = new Vector2(40f, 40f);
                var le = sw.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 40f;
                var dot = M3Build.Shape("Color", sw, ShapeCell.Create(ShapeKind.Fill).WithShape(CornerShape.Full), ColorRef.Clear, false);
                var dotRt = (RectTransform)dot.transform;
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f); dotRt.sizeDelta = new Vector2(28f, 28f);
                var binding = dot.GetComponent<M3ColorBinding>(); if (binding != null) UnityEngine.Object.DestroyImmediate(binding);
                dot.color = Seeds[i].seed;
                var ring = M3Build.Shape("Selected", sw, ShapeCell.Create(ShapeKind.Stroke).WithShape(CornerShape.Full).WithStroke(2f), ColorRef.Role(ColorRole.OnSurface), false);
                ring.raycastTarget = false;
                var ringRt = (RectTransform)ring.transform;
                ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f); ringRt.sizeDelta = new Vector2(38f, 38f);
                ring.gameObject.SetActive(i == 0);
                var btn = sw.gameObject.AddComponent<Button>();
                btn.targetGraphic = dot;
                btn.transition = Selectable.Transition.None;
                UnityEventTools.AddIntPersistentListener(btn.onClick, app.SetSeed, i);
                rings[i] = ringRt;
            }
            app.seedRings = rings;

            var variantBtn = M3Buttons.Create(controls, ctx, Variants[0].name, ButtonStyle.Tonal, ButtonSize.Small, leadingIcon: "palette");
            UnityEventTools.AddVoidPersistentListener(variantBtn.GetComponent<Button>().onClick, app.NextVariant);
            foreach (var tmp in variantBtn.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (!tmp.name.Contains("Icon")) { app.variantLabel = tmp; break; }

            var darkBtn = M3IconButtons.Create(controls, ctx, "dark_mode");
            UnityEventTools.AddVoidPersistentListener(darkBtn.GetComponent<Button>().onClick, app.ToggleDark);
            foreach (var tmp in darkBtn.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (tmp.name == "Icon") { app.darkIcon = tmp; break; }
            app.darkGlyph = M3Icons.Glyph("dark_mode");
            app.lightGlyph = M3Icons.Glyph("light_mode");

            var seg = M3SegmentedButtons.Create(controls, ctx, Array.ConvertAll(Contrasts, c => c.name), new[] { true, false, false });
            var toggles = new List<M3Interactive>();
            foreach (var it in seg.GetComponentsInChildren<M3Interactive>(true))
            {
                int index = toggles.Count;
                toggles.Add(it);
                UnityEventTools.AddIntPersistentListener(it.GetComponent<Button>().onClick, app.SetContrast, index);
            }
            app.contrastToggles = toggles.ToArray();

            // log the components' change events
            foreach (var b in root.GetComponentsInChildren<M3BehaviourBase>(true))
            {
                if (b == app) continue;
                foreach (var fieldName in new[] { "changeListener", "selectionListener" })
                {
                    var f = b.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    if (f == null || !f.FieldType.IsAssignableFrom(typeof(M3ShowcaseApp)) || f.GetValue(b) as UnityEngine.Object != null) continue;
                    f.SetValue(b, app);
                    EditorUtility.SetDirty(b);
                }
            }

            for (int i = 1; i < n; i++) pages[i].SetActive(false);
            holder.sizeDelta = sizes[0];
            ShowFirst(app);
            M3Canvas.ApplyTheme(root.gameObject);
            M3ShapeAtlas.Save();
            M3ShapeTiles.Save();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[M3Batch] showcase scene: " + ScenePath + " (" + theme.schemes.Count + " schemes, " + n + " pages)");
        }

        /// <summary>
        /// Batch self-test: opens the showcase scene and sends every theme event to every behaviour in
        /// its "before Start" state (edit mode), one at a time, reporting the ones that throw.
        /// </summary>
        public static void SelfTest()
        {
            int failures = 0;
            EditorSceneManager.OpenScene(ScenePath);
            var theme = UnityEngine.Object.FindObjectOfType<M3Theme>(true);
            var all = UnityEngine.Object.FindObjectsOfType<M3BehaviourBase>(true);
            var seen = new HashSet<string>();
            foreach (int scheme in new[] { 1, 37, 200 })
            {
                theme.currentScheme = scheme;
                foreach (var b in all)
                {
                    foreach (var ev in new[] { "_M3ThemeChanged", "_M3Refresh" })
                    {
                        var m = b.GetType().GetMethod(ev, BindingFlags.Public | BindingFlags.Instance);
                        if (m == null) continue;
                        try { m.Invoke(b, null); }
                        catch (Exception e)
                        {
                            failures++;
                            var inner = e.InnerException ?? e;
                            string key = b.GetType().Name + "." + ev + ": " + inner.GetType().Name;
                            if (seen.Add(key)) Debug.Log("[M3Batch] FAIL " + key + "\n" + inner.StackTrace);
                        }
                    }
                }
            }
            // scroll and drag over a component must reach the scroll view it sits in
            int blocked = 0, checkedCount = 0;
            foreach (var relay in UnityEngine.Object.FindObjectsOfType<M3PointerEvents>(true))
            {
                var view = relay.GetComponentInParent<ScrollRect>(true);
                if (view == null) continue;
                checkedCount++;
                var scrollTarget = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IScrollHandler>(relay.gameObject);
                var dragTarget = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IBeginDragHandler>(relay.gameObject);
                if (scrollTarget == null || scrollTarget.GetComponent<ScrollRect>() == null || dragTarget == null || dragTarget.GetComponent<ScrollRect>() == null)
                {
                    if (blocked++ < 5) Debug.Log("[M3Batch] FAIL scroll blocked at " + (scrollTarget != null ? scrollTarget.name : "null") + " for " + relay.name);
                }
            }
            failures += blocked;
            Debug.Log("[M3Batch] scroll pass-through: " + checkedCount + " components checked, " + blocked + " blocked");
            Debug.Log("[M3Batch] self-test: " + all.Length + " behaviours, " + failures + " failures");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void ShowFirst(M3ShowcaseApp app)
        {
            var ind = app.navIndicator;
            ind.SetParent(app.navItems[0], false);
            ind.SetAsFirstSibling();
            ind.anchorMin = Vector2.zero; ind.anchorMax = Vector2.one;
            ind.offsetMin = new Vector2(12f, 0f); ind.offsetMax = new Vector2(-12f, 0f);
        }

        public static string BuildPlayer(string exePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exePath));
            PlayerSettings.productName = "M3E4Unity Showcase";
            PlayerSettings.companyName = "M3E4Unity";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Showcase build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            return exePath;
        }

        // left / top / right / bottom insets; a negative inset means "anchored to the other side" with the given size
        static void Stretch(RectTransform rt, float left, float top, float right, float bottom, float width = 0f, float height = 0f)
        {
            float xMin = 0f, xMax = 1f, yMin = 0f, yMax = 1f;
            if (right < 0f) xMax = 0f;
            if (bottom < 0f) yMin = 1f;
            if (top < 0f) yMax = 0f;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0f, 1f);
            if (right < 0f) { rt.offsetMin = new Vector2(left, rt.offsetMin.y); rt.offsetMax = new Vector2(left + width, rt.offsetMax.y); }
            else { rt.offsetMin = new Vector2(left, rt.offsetMin.y); rt.offsetMax = new Vector2(-right, rt.offsetMax.y); }
            if (bottom < 0f) { rt.offsetMax = new Vector2(rt.offsetMax.x, -top); rt.offsetMin = new Vector2(rt.offsetMin.x, -top - height); }
            else if (top < 0f) { rt.offsetMin = new Vector2(rt.offsetMin.x, bottom); rt.offsetMax = new Vector2(rt.offsetMax.x, bottom + height); }
            else { rt.offsetMax = new Vector2(rt.offsetMax.x, -top); rt.offsetMin = new Vector2(rt.offsetMin.x, bottom); }
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
#endif
