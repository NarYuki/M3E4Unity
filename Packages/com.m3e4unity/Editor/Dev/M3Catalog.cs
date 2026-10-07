using System;
using System.Collections.Generic;
using System.IO;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor.Dev
{
    /// <summary>The component catalog: one page per component family, in every scheme of the theme.</summary>
    public static class M3Catalog
    {
        public sealed class Page
        {
            public string Name;
            public RectTransform Root;
            public Vector2 Size;
        }

        static readonly List<(string name, Vector2 size, Action<RectTransform, M3Context> build)> Sections =
            new List<(string, Vector2, Action<RectTransform, M3Context>)>
            {
                ("buttons", new Vector2(1180, 980), BuildButtons),
                ("iconbuttons_fabs", new Vector2(1180, 980), BuildIconButtonsAndFabs),
                ("selection", new Vector2(700, 420), BuildSelection),
                ("progress", new Vector2(760, 520), BuildProgress),
                ("sliders", new Vector2(760, 500), BuildSliders),
                ("containment", new Vector2(900, 520), BuildContainment),
                ("groups_chips", new Vector2(1000, 640), BuildGroupsAndChips),
                ("lists", new Vector2(860, 700), BuildLists),
                ("textfields", new Vector2(960, 560), BuildTextFields),
                ("menus", new Vector2(900, 620), BuildMenus),
                ("dialogs", new Vector2(1320, 520), BuildDialogs),
                ("snackbars", new Vector2(760, 520), BuildSnackbars),
                ("tooltips", new Vector2(1400, 420), BuildTooltips),
                ("navigation_bar", new Vector2(760, 460), BuildNavigationBars),
                ("navigation_rail", new Vector2(1720, 560), BuildNavigationRails),
                ("tabs", new Vector2(760, 560), BuildTabs),
                ("app_bars", new Vector2(760, 860), BuildAppBars),
                ("toolbars", new Vector2(760, 520), BuildToolbars),
                ("segmented", new Vector2(760, 360), BuildSegmented),
                ("sheets_drawers", new Vector2(1000, 720), BuildSheetsAndDrawers),
                ("date_picker", new Vector2(1260, 680), BuildDatePicker),
                ("date_range_picker", new Vector2(920, 840), BuildDateRangePicker),
                ("time_picker", new Vector2(1820, 720), BuildTimePicker),
                ("fab_menu", new Vector2(560, 480), BuildFabMenu),
                ("carousel", new Vector2(640, 820), BuildCarousels),
                ("search", new Vector2(1760, 720), BuildSearch),
            };

        /// <summary>The catalog sections (for the showcase app).</summary>
        public static int SectionCount => Sections.Count;
        public static string SectionName(int i) => Sections[i].name;
        public static Vector2 SectionSize(int i) => Sections[i].size;
        public static void BuildSection(int i, RectTransform parent, M3Context ctx) => Sections[i].build(parent, ctx);

        public static Page[] Build(string scenePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var theme = M3Canvas.DefaultTheme();
            var pages = new List<Page>();
            for (int s = 0; s < theme.schemes.Count; s++)
            {
                foreach (var (name, size, build) in Sections)
                {
                    string pageName = $"{name}_{theme.schemes[s].name.ToLowerInvariant()}";
                    var root = M3Canvas.Create(pageName, theme, size, worldSpace: false);
                    var runtimeTheme = root.GetComponent<M3Theme>();
                    M3Canvas.Surface(root);
                    var content = M3Build.Rect("Content", root);
                    M3Build.Fill(content, 24, 24, 24, 24);
                    var col = M3Build.Column(content, 24);
                    var ctx = new M3Context(theme);
                    build(content, ctx);
                    // bake with this page's scheme as default
                    int saved = theme.defaultScheme;
                    theme.defaultScheme = s;
                    M3Canvas.ApplyTheme(root.gameObject);
                    theme.defaultScheme = saved;
                    pages.Add(new Page { Name = pageName, Root = root, Size = size });
                }
            }
            M3ShapeAtlas.Save();
            M3ShapeTiles.Save();
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            return pages.ToArray();
        }

        static RectTransform Heading(RectTransform parent, string text, M3Context ctx)
        {
            var t = M3Build.Text("Heading", parent, text, TypeRole.TitleLarge, ColorRef.Role(ColorRole.OnSurface), ctx);
            t.alignment = TextAlignmentOptions.Left;
            return (RectTransform)t.transform;
        }

        static RectTransform Row(RectTransform parent, float spacing = 12)
        {
            var r = M3Build.Rect("Row", parent);
            M3Build.Row(r, spacing, TextAnchor.MiddleLeft);
            return r;
        }

        static void BuildButtons(RectTransform page, M3Context ctx)
        {
            Heading(page, "Buttons", ctx);
            foreach (ButtonSize size in Enum.GetValues(typeof(ButtonSize)))
            {
                var row = Row(page);
                foreach (ButtonStyle style in Enum.GetValues(typeof(ButtonStyle)))
                {
                    string label = size >= ButtonSize.Large ? style.ToString() : style.ToString();
                    M3Buttons.Create(row, ctx, label, style, size, ButtonShape.Round,
                        leadingIcon: size == ButtonSize.Small ? "add" : null);
                }
            }
            Heading(page, "Square, toggle, disabled", ctx);
            var r1 = Row(page);
            foreach (ButtonStyle style in Enum.GetValues(typeof(ButtonStyle)))
                M3Buttons.Create(r1, ctx, style.ToString(), style, ButtonSize.Small, ButtonShape.Square);
            var r2 = Row(page);
            foreach (ButtonStyle style in new[] { ButtonStyle.Filled, ButtonStyle.Tonal, ButtonStyle.Elevated, ButtonStyle.Outlined })
            {
                M3Buttons.Create(r2, ctx, "Off", style, ButtonSize.Small, ButtonShape.Round, leadingIcon: "favorite", toggle: true, selected: false);
                M3Buttons.Create(r2, ctx, "On", style, ButtonSize.Small, ButtonShape.Round, leadingIcon: "favorite", toggle: true, selected: true);
            }
            var r3 = Row(page);
            foreach (ButtonStyle style in Enum.GetValues(typeof(ButtonStyle)))
                M3Buttons.Create(r3, ctx, "Disabled", style, ButtonSize.Small, ButtonShape.Round, enabled: false);
        }
    

        static void BuildIconButtonsAndFabs(RectTransform page, M3Context ctx)
        {
            Heading(page, "Icon buttons", ctx);
            foreach (ButtonSize size in Enum.GetValues(typeof(ButtonSize)))
            {
                var row = Row(page);
                foreach (IconButtonStyle style in Enum.GetValues(typeof(IconButtonStyle)))
                    M3IconButtons.Create(row, ctx, "settings", style, size);
                foreach (IconButtonWidth w in new[] { IconButtonWidth.Narrow, IconButtonWidth.Wide })
                    M3IconButtons.Create(row, ctx, "edit", IconButtonStyle.Filled, size, w);
                M3IconButtons.Create(row, ctx, "favorite", IconButtonStyle.Tonal, size, shape: ButtonShape.Square);
            }
            Heading(page, "Icon toggle buttons (off / on), disabled", ctx);
            var r = Row(page);
            foreach (IconButtonStyle style in Enum.GetValues(typeof(IconButtonStyle)))
            {
                M3IconButtons.Create(r, ctx, "bookmark", style, toggle: true, selected: false);
                M3IconButtons.Create(r, ctx, "bookmark", style, toggle: true, selected: true);
            }
            foreach (IconButtonStyle style in Enum.GetValues(typeof(IconButtonStyle)))
                M3IconButtons.Create(r, ctx, "delete", style, enabled: false);
            Heading(page, "FABs", ctx);
            var f = Row(page, 16);
            foreach (FabSize size in Enum.GetValues(typeof(FabSize)))
                M3Fabs.Create(f, ctx, "edit", size);
            M3Fabs.Create(f, ctx, "add", FabSize.Baseline, ColorRole.SecondaryContainer);
            M3Fabs.Create(f, ctx, "add", FabSize.Baseline, ColorRole.TertiaryContainer);
            M3Fabs.Create(f, ctx, "add", FabSize.Baseline, ColorRole.Primary);
            var e = Row(page, 16);
            foreach (ExtendedFabSize size in Enum.GetValues(typeof(ExtendedFabSize)))
                M3Fabs.Extended(e, ctx, "Compose", "edit", size);
        }
    

        static void BuildSelection(RectTransform page, M3Context ctx)
        {
            Heading(page, "Checkbox (off / on / indeterminate / error), disabled", ctx);
            var r = Row(page, 8);
            for (int s = 0; s < 3; s++) M3SelectionControls.Checkbox(r, ctx, s);
            M3SelectionControls.Checkbox(r, ctx, 1, error: true);
            for (int s = 0; s < 3; s++) M3SelectionControls.Checkbox(r, ctx, s, enabled: false);
            Heading(page, "Radio buttons", ctx);
            var r2 = Row(page, 8);
            M3SelectionControls.Radio(r2, ctx, false);
            M3SelectionControls.Radio(r2, ctx, true);
            M3SelectionControls.Radio(r2, ctx, false, false);
            M3SelectionControls.Radio(r2, ctx, true, false);
            Heading(page, "Switches", ctx);
            var r3 = Row(page, 16);
            M3SelectionControls.Switch(r3, ctx, false);
            M3SelectionControls.Switch(r3, ctx, true);
            M3SelectionControls.Switch(r3, ctx, false, thumbIcon: "close");
            M3SelectionControls.Switch(r3, ctx, true, thumbIcon: "check");
            M3SelectionControls.Switch(r3, ctx, false, false);
            M3SelectionControls.Switch(r3, ctx, true, false);
        }
    

        static void BuildProgress(RectTransform page, M3Context ctx)
        {
            Heading(page, "Linear progress: flat, wavy, indeterminate", ctx);
            var r = Row(page, 24);
            M3ProgressIndicators.Linear(r, ctx, 0.3f);
            M3ProgressIndicators.Linear(r, ctx, 0.6f, wavy: true);
            var r2 = Row(page, 24);
            M3ProgressIndicators.Linear(r2, ctx, null);
            M3ProgressIndicators.Linear(r2, ctx, null, wavy: true);
            Heading(page, "Circular progress: flat, wavy, indeterminate", ctx);
            var r3 = Row(page, 24);
            M3ProgressIndicators.Circular(r3, ctx, 0.3f);
            M3ProgressIndicators.Circular(r3, ctx, 0.7f);
            M3ProgressIndicators.Circular(r3, ctx, 0.6f, wavy: true);
            M3ProgressIndicators.Circular(r3, ctx, null);
            M3ProgressIndicators.Circular(r3, ctx, null, wavy: true);
            Heading(page, "Loading indicator: default, contained, determinate", ctx);
            var r4 = Row(page, 24);
            M3ProgressIndicators.Loading(r4, ctx);
            M3ProgressIndicators.Loading(r4, ctx, contained: true);
            M3ProgressIndicators.Loading(r4, ctx, progress: 0.3f);
            M3ProgressIndicators.Loading(r4, ctx, contained: true, progress: 0.8f);
        }
    

        static void BuildSliders(RectTransform page, M3Context ctx)
        {
            Heading(page, "Sliders: continuous, discrete, centered, disabled", ctx);
            var r = Row(page, 32);
            M3Sliders.Create(r, ctx, 300, 0.4f);
            M3Sliders.Create(r, ctx, 300, 0.6f, steps: 4);
            var r2 = Row(page, 32);
            M3Sliders.Create(r2, ctx, 300, 0.75f, centered: true);
            M3Sliders.Create(r2, ctx, 300, 0.3f, enabled: false);
            var r3 = Row(page, 32);
            M3Sliders.Create(r3, ctx, 300, 0f);
            M3Sliders.Create(r3, ctx, 300, 1f);
            var r4 = Row(page, 32);
            M3Sliders.Range(r4, ctx, 300, 0.2f, 0.7f);
            M3Sliders.Range(r4, ctx, 300, 0.3f, 0.6f, enabled: false);
        }
    

        static void BuildContainment(RectTransform page, M3Context ctx)
        {
            Heading(page, "Badges", ctx);
            var r = Row(page, 24);
            var a = M3IconButtons.Create(r, ctx, "mail");
            M3Containment.Badge((RectTransform)a.Find("Icon"), ctx);
            var b = M3IconButtons.Create(r, ctx, "notifications");
            M3Containment.Badge((RectTransform)b.Find("Icon"), ctx, "3");
            var c = M3IconButtons.Create(r, ctx, "chat");
            M3Containment.Badge((RectTransform)c.Find("Icon"), ctx, "999+");
            Heading(page, "Dividers", ctx);
            M3Containment.Divider(page, ctx, false, 400);
            Heading(page, "Cards: filled, elevated, outlined (static / clickable)", ctx);
            var cr = Row(page, 16);
            foreach (CardStyle s in Enum.GetValues(typeof(CardStyle)))
            {
                var card = M3Containment.Card(cr, ctx, s, new Vector2(180, 120));
                var t = M3Build.Text("Title", card.Find("Content"), s.ToString(), TypeRole.TitleMedium, ColorRef.Role(ColorRole.OnSurface), ctx);
                M3Build.Fill((RectTransform)t.transform, 16, 16, 16, 16);
                t.alignment = TextAlignmentOptions.TopLeft;
            }
            foreach (CardStyle s in Enum.GetValues(typeof(CardStyle)))
                M3Containment.Card(cr, ctx, s, new Vector2(80, 120), clickable: true);
        }
    

        static void BuildGroupsAndChips(RectTransform page, M3Context ctx)
        {
            Heading(page, "Button groups: standard, connected", ctx);
            var r = Row(page, 32);
            M3ButtonGroups.Standard(r, ctx, new[] { "Undo", "Redo", "Save" }, ButtonStyle.Tonal);
            M3ButtonGroups.Standard(r, ctx, new string[] { null, null, null, null }, ButtonStyle.Filled, icons: new[] { "format_bold", "format_italic", "format_underlined", "format_color_text" });
            var r2 = Row(page, 32);
            M3ButtonGroups.Connected(r2, ctx, new[] { "Day", "Week", "Month", "Year" }, selected: new[] { 1 });
            M3ButtonGroups.Connected(r2, ctx, new[] { "Wi-Fi", "Bluetooth", "NFC" }, new[] { "wifi", "bluetooth", "nfc" }, new[] { 0, 2 }, false, ButtonStyle.Filled);
            Heading(page, "Split buttons", ctx);
            var r3 = Row(page, 24);
            M3ButtonGroups.Split(r3, ctx, "Edit");
            M3ButtonGroups.Split(r3, ctx, "Share", "share", ButtonStyle.Tonal, checkedTrailing: true);
            M3ButtonGroups.Split(r3, ctx, "Save", "save", ButtonStyle.Outlined);
            M3ButtonGroups.Split(r3, ctx, "Send", "send", ButtonStyle.Elevated, ButtonSize.Medium);
            Heading(page, "Chips: assist, filter, input, suggestion", ctx);
            var r4 = Row(page, 8);
            M3Chips.Create(r4, ctx, ChipKind.Assist, "Add to calendar", "event");
            M3Chips.Create(r4, ctx, ChipKind.Assist, "Elevated", "event", elevated: true);
            M3Chips.Create(r4, ctx, ChipKind.Filter, "Filter");
            M3Chips.Create(r4, ctx, ChipKind.Filter, "Selected", selected: true);
            M3Chips.Create(r4, ctx, ChipKind.Filter, "Elevated", elevated: true, selected: true);
            var r5 = Row(page, 8);
            M3Chips.Create(r5, ctx, ChipKind.Input, "Input", trailingIcon: "close");
            M3Chips.Create(r5, ctx, ChipKind.Input, "Selected", "person", "close", selected: true);
            M3Chips.Create(r5, ctx, ChipKind.Suggestion, "Suggestion");
            M3Chips.Create(r5, ctx, ChipKind.Suggestion, "Elevated", elevated: true);
            M3Chips.Create(r5, ctx, ChipKind.Assist, "Disabled", "event", enabled: false);
            M3Chips.Create(r5, ctx, ChipKind.Filter, "Disabled", selected: true, enabled: false);
        }

        static void BuildTextFields(RectTransform page, M3Context ctx)
        {
            foreach (TextFieldStyle style in System.Enum.GetValues(typeof(TextFieldStyle)))
            {
                Heading(page, style + " text fields: empty, filled in, icons, error, disabled", ctx);
                var r = Row(page, 24);
                ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
                M3TextFields.Create(r, ctx, style, "Label", supporting: "Supporting text", width: 200);
                M3TextFields.Create(r, ctx, style, "Label", "Input text", width: 200);
                M3TextFields.Create(r, ctx, style, "Search", "Material", leadingIcon: "search", trailingIcon: "close", width: 240);
                var r2 = Row(page, 24);
                ((HorizontalLayoutGroup)r2.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
                M3TextFields.Create(r2, ctx, style, "Email", "name@", supporting: "Invalid address", trailingIcon: "error", error: true, width: 200);
                M3TextFields.Create(r2, ctx, style, "Disabled", "Read only", enabled: false, width: 200);
                M3TextFields.Create(r2, ctx, style, null, placeholder: "Placeholder (no label)", width: 240);
            }
        }

        static void BuildMenus(RectTransform page, M3Context ctx)
        {
            Heading(page, "Menus: standard, vibrant, single group", ctx);
            var r = Row(page, 32);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            M3Menus.Create(r, ctx, new[]
            {
                new[]
                {
                    new MenuItemSpec { Text = "Cut", LeadingIcon = "content_cut", TrailingIcon = "keyboard_command_key" },
                    new MenuItemSpec { Text = "Copy", LeadingIcon = "content_copy" },
                    new MenuItemSpec { Text = "Paste", LeadingIcon = "content_paste", Enabled = false },
                },
                new[]
                {
                    new MenuItemSpec { Text = "Bold", SelectedLeadingIcon = "check", Selected = true },
                    new MenuItemSpec { Text = "Italic", SelectedLeadingIcon = "check" },
                },
                new[] { new MenuItemSpec { Text = "Settings", Supporting = "App preferences", LeadingIcon = "settings" } },
            });
            M3Menus.Create(r, ctx, new[]
            {
                new[]
                {
                    new MenuItemSpec { Text = "Share", LeadingIcon = "share" },
                    new MenuItemSpec { Text = "Favorite", LeadingIcon = "favorite", Selected = true },
                    new MenuItemSpec { Text = "Archive", LeadingIcon = "archive" },
                },
                new[] { new MenuItemSpec { Text = "Delete", LeadingIcon = "delete" } },
            }, vibrant: true);
            M3Menus.Create(r, ctx, new[]
            {
                new[]
                {
                    new MenuItemSpec { Text = "Item 1" },
                    new MenuItemSpec { Text = "Item 2", Selected = true, SelectedLeadingIcon = "check" },
                    new MenuItemSpec { Text = "Item 3", TrailingIcon = "chevron_right" },
                },
            });
        }

        static void BuildDialogs(RectTransform page, M3Context ctx)
        {
            Heading(page, "Dialogs: basic, with icon", ctx);
            var r = Row(page, 24);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            foreach (var withIcon in new[] { false, true })
            {
                // each dialog on its own scrimmed area
                var area = M3Build.Rect("DialogArea", r);
                var le = area.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 620;
                le.preferredHeight = le.minHeight = 400;
                if (withIcon)
                    M3Dialogs.Create(area, ctx, "Reset settings?", "This will reset your device to its default factory settings.", "Accept", "Cancel", icon: "restart_alt");
                else
                    M3Dialogs.Create(area, ctx, "Basic dialog title", "A dialog is a type of modal window that appears in front of app content to provide critical information, or prompt for a decision to be made.", "Confirm", "Dismiss");
            }
        }

        static void BuildSnackbars(RectTransform page, M3Context ctx)
        {
            Heading(page, "Snackbars: plain, action, dismiss, two lines, action on new line", ctx);
            M3Snackbars.Create(page, ctx, "Photo has been archived", duration: SnackbarDuration.Indefinite);
            M3Snackbars.Create(page, ctx, "Photo has been archived", "Undo", duration: SnackbarDuration.Indefinite);
            M3Snackbars.Create(page, ctx, "Photo has been archived", "Undo", withDismissAction: true, duration: SnackbarDuration.Indefinite);
            M3Snackbars.Create(page, ctx, "Connection lost. Some changes may not be saved until you reconnect to the network.", "Retry", width: 420, duration: SnackbarDuration.Indefinite);
            M3Snackbars.Create(page, ctx, "Your storage is almost full. Free up space to keep syncing photos.", "Manage storage", actionOnNewLine: true, width: 420, duration: SnackbarDuration.Indefinite);
        }

        static void BuildTooltips(RectTransform page, M3Context ctx)
        {
            Heading(page, "Tooltips: plain, plain with caret, rich, rich with action", ctx);
            var spacer = M3Build.Rect("Spacer", page);
            spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = 170;
            var r = Row(page, 300);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.LowerLeft;
            r.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(80, 0, 0, 0);
            var a = M3IconButtons.Create(r, ctx, "favorite");
            M3Tooltips.Plain(a, ctx, "Add to favorites", expanded: true);
            var b = M3IconButtons.Create(r, ctx, "share");
            M3Tooltips.Plain(b, ctx, "Share", caret: true, expanded: true);
            var c = M3IconButtons.Create(r, ctx, "info");
            M3Tooltips.Rich(c, ctx, "Rich tooltip", "Rich tooltips bring attention to a particular element or feature.", expanded: true);
            var d = M3IconButtons.Create(r, ctx, "help");
            M3Tooltips.Rich(d, ctx, "Camera settings", "Adjust exposure and focus before taking the photo.", "Learn more", caret: true, expanded: true);
        }

        static void BuildNavigationBars(RectTransform page, M3Context ctx)
        {
            Heading(page, "Short navigation bar: top icons, start icons (centered), disabled item", ctx);
            var items = new[]
            {
                new NavigationItemSpec { Icon = "home", Label = "Home" },
                new NavigationItemSpec { Icon = "search", Label = "Search" },
                new NavigationItemSpec { Icon = "favorite", Label = "Favorites" },
                new NavigationItemSpec { Icon = "person", Label = "Profile" },
            };
            M3NavigationBars.Create(page, ctx, items, 0, 412);
            M3NavigationBars.Create(page, ctx, items, 2, 700, NavigationItemIconPosition.Start, ShortNavigationBarArrangement.Centered);
            var withDisabled = new[]
            {
                new NavigationItemSpec { Icon = "mail", Label = "Mail" },
                new NavigationItemSpec { Icon = "chat", Label = "Chat" },
                new NavigationItemSpec { Icon = "videocam", Label = "Meet", Enabled = false },
            };
            M3NavigationBars.Create(page, ctx, withDisabled, 1, 412);
        }

        static void BuildNavigationRails(RectTransform page, M3Context ctx)
        {
            Heading(page, "Wide navigation rail: collapsed, expanded; modal (open), modal hideOnCollapse (open)", ctx);
            var r = Row(page, 24);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            var items = new[]
            {
                new NavigationItemSpec { Icon = "inbox", Label = "Inbox" },
                new NavigationItemSpec { Icon = "send", Label = "Outbox" },
                new NavigationItemSpec { Icon = "favorite", Label = "Favorites" },
                new NavigationItemSpec { Icon = "delete", Label = "Trash", Enabled = false },
            };
            M3NavigationRails.Create(r, ctx, items, 0, 440);
            M3NavigationRails.Create(r, ctx, items, 1, 440, expanded: true);
            foreach (var hide in new[] { false, true })
            {
                var frame = M3Build.Rect("Screen", r);
                var fl = frame.gameObject.AddComponent<LayoutElement>();
                fl.preferredWidth = fl.minWidth = 400f;
                fl.preferredHeight = fl.minHeight = 440f;
                frame.sizeDelta = new Vector2(400f, 440f);
                var text = M3Build.Text("Body", frame, "Screen content", TypeRole.BodyLarge, ColorRef.Role(ColorRole.OnSurface), ctx);
                text.alignment = TMPro.TextAlignmentOptions.Center;
                M3Build.Fill(text.rectTransform);
                var modal = M3NavigationRails.Modal(frame, ctx, items, 2, 440, 400f, hideOnCollapse: hide, open: true);
                modal.anchorMin = modal.anchorMax = new Vector2(0f, 1f);
                modal.pivot = new Vector2(0f, 1f);
                modal.anchoredPosition = Vector2.zero;
            }
        }

        static void BuildTabs(RectTransform page, M3Context ctx)
        {
            Heading(page, "Tabs: primary (text, icon + text, leading icon), secondary", ctx);
            M3Tabs.Create(page, ctx, TabStyle.Primary, new[] { new TabSpec { Text = "Video" }, new TabSpec { Text = "Photos" }, new TabSpec { Text = "Audio" } }, 0, 412);
            M3Tabs.Create(page, ctx, TabStyle.Primary, new[]
            {
                new TabSpec { Text = "Flights", Icon = "flight" }, new TabSpec { Text = "Trips", Icon = "luggage" }, new TabSpec { Text = "Explore", Icon = "explore" },
            }, 1, 412);
            M3Tabs.Create(page, ctx, TabStyle.Primary, new[]
            {
                new TabSpec { Text = "Mail", Icon = "mail", LeadingIcon = true }, new TabSpec { Text = "Chat", Icon = "chat", LeadingIcon = true },
            }, 0, 412);
            M3Tabs.Create(page, ctx, TabStyle.Secondary, new[] { new TabSpec { Text = "Overview" }, new TabSpec { Text = "Specifications" }, new TabSpec { Text = "Reviews", Enabled = false } }, 1, 412);
        }

        static void BuildAppBars(RectTransform page, M3Context ctx)
        {
            Heading(page, "Top app bars: small, center aligned, medium flexible, large flexible", ctx);
            var acts = new[] { "attach_file", "event", "more_vert" };
            M3TopAppBars.Create(page, ctx, TopAppBarStyle.Small, "Title", 412, navigationIcon: "menu", actions: acts);
            M3TopAppBars.Create(page, ctx, TopAppBarStyle.CenterAligned, "Title", 412, subtitle: "Subtitle", actions: new[] { "account_circle" });
            M3TopAppBars.Create(page, ctx, TopAppBarStyle.MediumFlexible, "Medium title", 412, actions: acts);
            M3TopAppBars.Create(page, ctx, TopAppBarStyle.MediumFlexible, "Medium title", 412, subtitle: "Subtitle", actions: acts);
            M3TopAppBars.Create(page, ctx, TopAppBarStyle.LargeFlexible, "Large title", 412, subtitle: "Subtitle", actions: acts, centerTitle: true);
        }

        static void BuildToolbars(RectTransform page, M3Context ctx)
        {
            Heading(page, "Toolbars: floating standard / vibrant, with FAB, docked", ctx);
            var icons = new[] { "format_bold", "format_italic", "format_underlined", "format_color_text" };
            M3Toolbars.Floating(page, ctx, icons);
            M3Toolbars.Floating(page, ctx, icons, vibrant: true);
            M3Toolbars.Floating(page, ctx, new[] { "arrow_back", "arrow_forward", "download", "more_vert" }, vibrant: true, fabIcon: "add");
            M3Toolbars.Docked(page, ctx, new[] { "arrow_back", "arrow_forward", "add", "download", "more_vert" }, 412);
        }

        static void BuildSegmented(RectTransform page, M3Context ctx)
        {
            Heading(page, "Segmented buttons: single choice, multi choice, disabled", ctx);
            M3SegmentedButtons.Create(page, ctx, new[] { "Day", "Week", "Month" }, new[] { false, true, false });
            M3SegmentedButtons.Create(page, ctx, new[] { "Walk", "Ride", "Drive", "Fly" }, new[] { true, false, true, false }, singleChoice: false);
            M3SegmentedButtons.Create(page, ctx, new[] { "$", "$$", "$$$" }, new[] { false, true, false }, enabled: new[] { true, false, false });
        }

        static void BuildSheetsAndDrawers(RectTransform page, M3Context ctx)
        {
            Heading(page, "Modal bottom sheet, modal navigation drawer", ctx);
            var r = Row(page, 24);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            var a = M3Build.Rect("SheetArea", r);
            var ale = a.gameObject.AddComponent<LayoutElement>(); ale.preferredWidth = ale.minWidth = 412; ale.preferredHeight = ale.minHeight = 600;
            a.sizeDelta = new Vector2(412, 600);
            M3Sheets.ModalBottomSheet(a, ctx, 320, out var content);
            var sheetList = M3Lists.List(content, ctx.On(SheetBottomTokens.DockedContainerColor), new[]
            {
                new ListItemSpec { Headline = "Share", LeadingIcon = "share" },
                new ListItemSpec { Headline = "Get link", LeadingIcon = "link" },
                new ListItemSpec { Headline = "Edit name", LeadingIcon = "edit" },
                new ListItemSpec { Headline = "Delete collection", LeadingIcon = "delete" },
            }, 412);
            sheetList.anchorMin = sheetList.anchorMax = new Vector2(0.5f, 1f);
            sheetList.pivot = new Vector2(0.5f, 1f);
            sheetList.anchoredPosition = Vector2.zero;
            var b = M3Build.Rect("DrawerArea", r);
            var ble = b.gameObject.AddComponent<LayoutElement>(); ble.preferredWidth = ble.minWidth = 520; ble.preferredHeight = ble.minHeight = 600;
            b.sizeDelta = new Vector2(520, 600);
            M3NavigationDrawers.Modal(b, ctx, new[]
            {
                new DrawerItemSpec { Label = "Inbox", Icon = "inbox", Badge = "24" },
                new DrawerItemSpec { Label = "Outbox", Icon = "send" },
                new DrawerItemSpec { Label = "Favorites", Icon = "favorite" },
                new DrawerItemSpec { Label = "Trash", Icon = "delete" },
            }, 0);
        }

        static void BuildDatePicker(RectTransform page, M3Context ctx)
        {
            Heading(page, "Date picker dialog (month grid / year picker / input mode)", ctx);
            var row = Row(page, 40);
            M3DatePickers.Dialog(row, ctx, 2026, 10, 17, 2026, 10);
            M3DatePickers.Dialog(row, ctx, 2026, 10, 17, 2026, 10, yearPickerVisible: true);
            M3DatePickers.Dialog(row, ctx, 2026, 10, 17, 2026, 10, inputMode: true);
        }

        static void BuildDateRangePicker(RectTransform page, M3Context ctx)
        {
            Heading(page, "Date range picker (full screen: picker / input mode)", ctx);
            var row = Row(page, 40);
            M3DateRangePickers.FullScreen(row, ctx, 412f, 740f, 20261014, 20261022);
            M3DateRangePickers.FullScreen(row, ctx, 412f, 740f, 20261014, 20261022, inputMode: true);
        }

        static void BuildTimePicker(RectTransform page, M3Context ctx)
        {
            Heading(page, "Time picker dialogs: standard picker / input, vibrant picker / input / scroll", ctx);
            var row = Row(page, 32);
            M3TimePickers.Dialog(row, ctx, 9, 30);
            M3TimePickers.Dialog(row, ctx, 9, 30, initialMode: 1);
            M3TimePickers.Dialog(row, ctx, 9, 30, vibrant: true);
            M3TimePickers.Dialog(row, ctx, 9, 30, vibrant: true, initialMode: 1);
            M3TimePickers.Dialog(row, ctx, 9, 30, vibrant: true, modes: TimePickerDialogModes.ScrollAndInput);
        }

        static void BuildFabMenu(RectTransform page, M3Context ctx)
        {
            Heading(page, "FAB menu: expanded, collapsed", ctx);
            var r = Row(page, 24);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.LowerLeft;
            var items = new[] { ("person_add", "Contact"), ("group", "Group"), ("edit", "Note"), ("event", "Event") };
            M3FabMenus.Create(r, ctx, items, expanded: true);
            M3FabMenus.Create(r, ctx, items, expanded: false);
        }

        static void BuildCarousels(RectTransform page, M3Context ctx)
        {
            Heading(page, "Carousels: multi-browse, uncontained, centered hero", ctx);
            M3Carousels.Create(page, ctx, CarouselKind.MultiBrowse, 412, 221, 10);
            M3Carousels.Create(page, ctx, CarouselKind.Uncontained, 412, 221, 10, preferredItemWidth: 186f);
            M3Carousels.Create(page, ctx, CarouselKind.CenteredHero, 412, 221, 10, preferredItemWidth: 267f);
        }

        static void BuildSearch(RectTransform page, M3Context ctx)
        {
            Heading(page, "Search: docked (collapsed, expanded), full screen (collapsed, expanded)", ctx);
            var r = Row(page, 24);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            var s = new[] { "Material Design", "Material 3 Expressive", "Motion schemes" };
            M3SearchBars.Docked(r, ctx, "Search", s);
            M3SearchBars.Docked(r, ctx, "Search", s, expanded: true);
            M3SearchBars.FullScreen(r, ctx, "Search", s, 412f, 600f);
            M3SearchBars.FullScreen(r, ctx, "Search", s, 412f, 600f, expanded: true);
        }

        static void BuildLists(RectTransform page, M3Context ctx)
        {
            Heading(page, "Lists: standard, segmented", ctx);
            var r = Row(page, 32);
            ((HorizontalLayoutGroup)r.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            M3Lists.List(r, ctx, new[]
            {
                new ListItemSpec { Headline = "One line item", LeadingIcon = "inbox", TrailingIcon = "chevron_right" },
                new ListItemSpec { Headline = "Two line item", Supporting = "Supporting text", LeadingAvatar = "A", TrailingText = "100+" },
                new ListItemSpec { Overline = "Overline", Headline = "Three line item", Supporting = "Supporting text", LeadingIcon = "person", TrailingIcon = "more_vert" },
                new ListItemSpec { Headline = "Multi-line supporting", Supporting = "Supporting text that is long enough to wrap onto a second line in this list.", LeadingIcon = "description" },
                new ListItemSpec { Headline = "Selected item", Supporting = "Selectable", LeadingIcon = "star", Selected = true, Selectable = true },
                new ListItemSpec { Headline = "Disabled item", LeadingIcon = "block", TrailingIcon = "chevron_right", Enabled = false },
                new ListItemSpec { Headline = "Selected disabled", LeadingIcon = "block", Selected = true, Enabled = false },
            }, 380);
            // segmented lists sit on a contrasting container so the Surface items and the gaps show
            var panel = M3Build.Rect("SegmentedPanel", r);
            M3Build.Shape("Backdrop", panel, ShapeCell.Create(ShapeKind.Fill).WithShape(ShapeScale.Get(ShapeRole.CornerExtraLarge)), ColorRef.Role(ColorRole.SurfaceContainerHighest));
            M3Pressable.Ignore(panel.Find("Backdrop").GetComponent<Image>());
            var pc = M3Build.Column(panel, 0);
            pc.padding = new RectOffset(12, 12, 12, 12);
            var sctx = ctx.On(ColorRole.SurfaceContainerHighest);
            M3Lists.List(panel, sctx, new[]
            {
                new ListItemSpec { Headline = "Wi-Fi", Supporting = "Connected", LeadingIcon = "wifi", TrailingIcon = "chevron_right" },
                new ListItemSpec { Headline = "Bluetooth", Supporting = "On", LeadingIcon = "bluetooth", Selected = true, Selectable = true },
                new ListItemSpec { Headline = "Mobile data", LeadingIcon = "signal_cellular_alt", Selectable = true },
                new ListItemSpec { Headline = "Airplane mode", LeadingIcon = "flight", Enabled = false },
            }, 360, segmented: true);
            M3Lists.List(panel, sctx, new[] { new ListItemSpec { Headline = "Single segmented item", LeadingIcon = "settings" } }, 360, segmented: true);
            pc.spacing = 16;
        }
    }
}