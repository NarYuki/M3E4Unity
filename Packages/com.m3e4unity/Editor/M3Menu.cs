using M3E4Unity.Tokens;
using UnityEditor;
using UnityEngine;

namespace M3E4Unity.Editor
{
    /// <summary>Editor menus: create an M3 canvas, add components under the selection, bake the theme.</summary>
    public static class M3Menu
    {
        const string Tools = "Tools/M3E4Unity/";
        const string Create = "GameObject/M3E4Unity/";

        [MenuItem(Tools + "Create World Space Canvas (VRChat)", priority = 0)]
        static void CreateWorldCanvas() => NewCanvas(true);

        [MenuItem(Tools + "Create Screen Space Canvas", priority = 1)]
        static void CreateScreenCanvas() => NewCanvas(false);

        static void NewCanvas(bool world)
        {
            var root = M3Canvas.Create("M3 Canvas", M3Canvas.DefaultTheme(), new Vector2(412, 915), world);
            M3Canvas.Surface(root);
            M3Canvas.ApplyTheme(root.gameObject);
            Selection.activeGameObject = root.gameObject;
        }

        [MenuItem(Tools + "Apply Theme to Selected Canvas", priority = 20)]
        static void ApplyTheme()
        {
            foreach (var go in Selection.gameObjects) M3Canvas.ApplyTheme(go);
        }

        [MenuItem(Tools + "Apply Theme to Selected Canvas", true)]
        static bool ApplyThemeValid() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<Udon.M3Theme>(true) != null;

        [MenuItem(Tools + "Build Component Catalog", priority = 40)]
        static void BuildCatalog()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Dev.M3Catalog.Build(Dev.M3Batch.ScenePath);
        }

        /// <summary>The selected RectTransform inside an M3 canvas, and a context for building under it.</summary>
        static bool Target(out RectTransform parent, out M3Context ctx)
        {
            parent = Selection.activeTransform as RectTransform;
            ctx = null;
            if (parent == null) return false;
            var runtime = parent.GetComponentInParent<Udon.M3Theme>(true);
            if (runtime == null) return false;
            var source = runtime.GetComponent<M3ThemeSource>();
            ctx = new M3Context(source != null && source.theme != null ? source.theme : M3Canvas.DefaultTheme());
            return true;
        }

        static void Build(System.Func<RectTransform, M3Context, RectTransform> build)
        {
            if (!Target(out var parent, out var ctx))
            {
                EditorUtility.DisplayDialog("M3E4Unity", "Select a RectTransform inside an M3 canvas (Tools > M3E4Unity > Create … Canvas).", "OK");
                return;
            }
            var rt = build(parent, ctx);
            Undo.RegisterCreatedObjectUndo(rt.gameObject, "Create " + rt.name);
            M3Canvas.ApplyTheme(rt.gameObject);
            Selection.activeGameObject = rt.gameObject;
        }

        [MenuItem(Create + "Button/Filled", priority = 10)] static void Filled() => Build((p, c) => M3Buttons.Create(p, c, "Button", ButtonStyle.Filled));
        [MenuItem(Create + "Button/Tonal")] static void Tonal() => Build((p, c) => M3Buttons.Create(p, c, "Button", ButtonStyle.Tonal));
        [MenuItem(Create + "Button/Elevated")] static void Elevated() => Build((p, c) => M3Buttons.Create(p, c, "Button", ButtonStyle.Elevated));
        [MenuItem(Create + "Button/Outlined")] static void Outlined() => Build((p, c) => M3Buttons.Create(p, c, "Button", ButtonStyle.Outlined));
        [MenuItem(Create + "Button/Text")] static void TextButton() => Build((p, c) => M3Buttons.Create(p, c, "Button", ButtonStyle.Text));
        [MenuItem(Create + "Button/Icon Button")] static void IconButton() => Build((p, c) => M3IconButtons.Create(p, c, "settings"));
        [MenuItem(Create + "Button/FAB")] static void Fab() => Build((p, c) => M3Fabs.Create(p, c, "add"));
        [MenuItem(Create + "Button/Extended FAB")] static void ExtendedFab() => Build((p, c) => M3Fabs.Extended(p, c, "Compose", "edit"));
        [MenuItem(Create + "Button/Button Group")] static void ButtonGroup() => Build((p, c) => M3ButtonGroups.Standard(p, c, new[] { "One", "Two", "Three" }, ButtonStyle.Tonal));
        [MenuItem(Create + "Button/Segmented Buttons")] static void Segmented() => Build((p, c) => M3SegmentedButtons.Create(p, c, new[] { "Day", "Week", "Month" }, new[] { true, false, false }));
        [MenuItem(Create + "Button/FAB Menu")] static void FabMenu() => Build((p, c) => M3FabMenus.Create(p, c, new[] { ("person_add", "Contact"), ("edit", "Note") }, false));

        [MenuItem(Create + "Selection/Checkbox", priority = 11)] static void Checkbox() => Build((p, c) => M3SelectionControls.Checkbox(p, c, 0));
        [MenuItem(Create + "Selection/Radio Button")] static void Radio() => Build((p, c) => M3SelectionControls.Radio(p, c, false));
        [MenuItem(Create + "Selection/Switch")] static void Switch() => Build((p, c) => M3SelectionControls.Switch(p, c, false));
        [MenuItem(Create + "Selection/Slider")] static void Slider() => Build((p, c) => M3Sliders.Create(p, c, 280f, 0.5f));
        [MenuItem(Create + "Selection/Range Slider")] static void RangeSlider() => Build((p, c) => M3Sliders.Range(p, c, 280f));
        [MenuItem(Create + "Selection/Chip")] static void Chip() => Build((p, c) => M3Chips.Create(p, c, ChipKind.Filter, "Filter"));

        [MenuItem(Create + "Input/Filled Text Field", priority = 12)] static void FilledField() => Build((p, c) => M3TextFields.Create(p, c, TextFieldStyle.Filled, "Label"));
        [MenuItem(Create + "Input/Outlined Text Field")] static void OutlinedField() => Build((p, c) => M3TextFields.Create(p, c, TextFieldStyle.Outlined, "Label"));
        [MenuItem(Create + "Input/Search Bar")] static void Search() => Build((p, c) => M3SearchBars.Docked(p, c, "Search", new[] { "Suggestion" }));
        [MenuItem(Create + "Input/Full Screen Search")] static void FullSearch() => Build((p, c) => M3SearchBars.FullScreen(p, c, "Search", new[] { "Suggestion" }));
        [MenuItem(Create + "Input/Date Picker")] static void DatePicker() => Build((p, c) => M3DatePickers.Dialog(p, c));
        [MenuItem(Create + "Input/Date Range Picker")] static void DateRangePicker() => Build((p, c) => M3DateRangePickers.FullScreen(p, c));
        [MenuItem(Create + "Input/Time Picker")] static void TimePicker() => Build((p, c) => M3TimePickers.Dialog(p, c));
        [MenuItem(Create + "Input/Time Picker (Vibrant)")] static void TimePickerVibrant() => Build((p, c) => M3TimePickers.Dialog(p, c, vibrant: true));
        [MenuItem(Create + "Input/Time Scroll Picker (Vibrant)")] static void TimeScroll() => Build((p, c) => M3TimePickers.Dialog(p, c, modes: TimePickerDialogModes.ScrollAndInput));
        [MenuItem(Create + "Input/Scroll Field")] static void ScrollField() => Build((p, c) => M3TimePickers.ScrollField(p, c, 60));

        [MenuItem(Create + "Containment/Card", priority = 13)] static void Card() => Build((p, c) => M3Containment.Card(p, c, CardStyle.Elevated, new Vector2(180, 120), clickable: true));
        [MenuItem(Create + "Containment/List")] static void List() => Build((p, c) => M3Lists.List(p, c, new[] { new ListItemSpec { Headline = "List item", LeadingIcon = "inbox" } }, 360f));
        [MenuItem(Create + "Containment/Divider")] static void Divider() => Build((p, c) => M3Containment.Divider(p, c, false, 360f));
        [MenuItem(Create + "Containment/Dialog")] static void Dialog() => Build((p, c) => M3Dialogs.Create(p, c, "Title", "Supporting text", "OK", "Cancel"));
        [MenuItem(Create + "Containment/Bottom Sheet")] static void Sheet() => Build((p, c) => M3Sheets.ModalBottomSheet(p, c, 320f, out _));
        [MenuItem(Create + "Containment/Carousel")] static void Carousel() => Build((p, c) => M3Carousels.Create(p, c, CarouselKind.MultiBrowse, 412f, 221f, 6));

        [MenuItem(Create + "Navigation/Navigation Bar", priority = 14)]
        static void NavBar() => Build((p, c) => M3NavigationBars.Create(p, c, new[]
        {
            new NavigationItemSpec { Icon = "home", Label = "Home" }, new NavigationItemSpec { Icon = "search", Label = "Search" },
            new NavigationItemSpec { Icon = "person", Label = "Profile" },
        }, 0, 412f));

        [MenuItem(Create + "Navigation/Navigation Rail")]
        static void NavRail() => Build((p, c) => M3NavigationRails.Create(p, c, new[]
        {
            new NavigationItemSpec { Icon = "inbox", Label = "Inbox" }, new NavigationItemSpec { Icon = "send", Label = "Outbox" },
        }, 0, 600f));

        [MenuItem(Create + "Navigation/Modal Navigation Rail")]
        static void ModalRail() => Build((p, c) => M3NavigationRails.Modal(p, c, new[]
        {
            new NavigationItemSpec { Icon = "inbox", Label = "Inbox" }, new NavigationItemSpec { Icon = "send", Label = "Outbox" },
        }, 0, 600f, 412f));

        [MenuItem(Create + "Navigation/Navigation Drawer")]
        static void Drawer() => Build((p, c) => M3NavigationDrawers.Modal(p, c, new[] { new DrawerItemSpec { Label = "Inbox", Icon = "inbox" } }, 0));

        [MenuItem(Create + "Navigation/Tabs")]
        static void Tabs() => Build((p, c) => M3Tabs.Create(p, c, TabStyle.Primary, new[] { new TabSpec { Text = "One" }, new TabSpec { Text = "Two" } }, 0, 412f));

        [MenuItem(Create + "Navigation/Top App Bar")] static void AppBar() => Build((p, c) => M3TopAppBars.Create(p, c, TopAppBarStyle.Small, "Title", 412f, actions: new[] { "more_vert" }));
        [MenuItem(Create + "Navigation/Floating Toolbar")] static void Toolbar() => Build((p, c) => M3Toolbars.Floating(p, c, new[] { "format_bold", "format_italic", "format_underlined" }));

        [MenuItem(Create + "Feedback/Linear Progress", priority = 15)] static void Linear() => Build((p, c) => M3ProgressIndicators.Linear(p, c, 0.5f));
        [MenuItem(Create + "Feedback/Circular Progress")] static void Circular() => Build((p, c) => M3ProgressIndicators.Circular(p, c, null));
        [MenuItem(Create + "Feedback/Loading Indicator")] static void Loading() => Build((p, c) => M3ProgressIndicators.Loading(p, c));
        [MenuItem(Create + "Feedback/Snackbar")] static void Snackbar() => Build((p, c) => M3Snackbars.Create(p, c, "Message", "Action", duration: SnackbarDuration.Indefinite));
        [MenuItem(Create + "Feedback/Menu")] static void Menu() => Build((p, c) => M3Menus.Create(p, c, new[] { new[] { new MenuItemSpec { Text = "Item" } } }));
    }
}
