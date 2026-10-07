using System.Collections.Generic;
using System.Linq;
using M3E4Unity.Theming;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Bakes an M3ThemeAsset into the runtime M3Theme of a UI root: every ColorRef used by the
    /// bindings below it becomes a palette entry, resolved for every scheme of the theme.
    /// </summary>
    public static class M3ThemeBaker
    {
        public static void Bake(M3Theme runtime, M3ThemeAsset theme)
        {
            var root = runtime.transform;
            var palette = new List<ColorRef>();
            var index = new Dictionary<ColorRef, int>();

            int Idx(ColorRef c)
            {
                if (!index.TryGetValue(c, out int i))
                {
                    i = palette.Count;
                    palette.Add(c);
                    index[c] = i;
                }
                return i;
            }

            // Interactive components: per-state colors (their graphics are excluded from static binding).
            var dynamicGraphics = new HashSet<Graphic>();
            var interactives = root.GetComponentsInChildren<M3Interactive>(true);
            foreach (var it in interactives)
            {
                var sc = it.GetComponent<M3StateColors>();
                if (sc == null) continue;
                var colored = new Graphic[sc.entries.Count];
                var indices = new int[sc.entries.Count * 4];
                for (int i = 0; i < sc.entries.Count; i++)
                {
                    var e = sc.entries[i];
                    colored[i] = e.graphic;
                    dynamicGraphics.Add(e.graphic);
                    indices[i * 4 + 0] = Idx(e.enabled);
                    indices[i * 4 + 1] = Idx(e.enabledSelected);
                    indices[i * 4 + 2] = Idx(e.disabled);
                    indices[i * 4 + 3] = Idx(e.disabledSelected);
                }
                it.theme = runtime;
                it.colored = colored;
                it.colorIndices = indices;
                if (sc.hasRipple)
                {
                    it.rippleColor = Idx(sc.ripple);
                    it.rippleColorSelected = Idx(sc.rippleSelected);
                }
            }

            // Checkbox / radio / switch / slider...: six color states per part (M3SelectionColors).
            var sixState = new List<Component>();
            foreach (var sc in root.GetComponentsInChildren<M3SelectionColors>(true))
            {
                var target = SixStateTarget(sc.gameObject);
                if (target == null) continue;
                var colored = new Graphic[sc.entries.Count];
                var indices = new int[sc.entries.Count * 6];
                for (int i = 0; i < sc.entries.Count; i++)
                {
                    var e = sc.entries[i];
                    colored[i] = e.graphic;
                    dynamicGraphics.Add(e.graphic);
                    indices[i * 6 + 0] = Idx(e.off);
                    indices[i * 6 + 1] = Idx(e.on);
                    indices[i * 6 + 2] = Idx(e.indeterminate);
                    indices[i * 6 + 3] = Idx(e.disabledOff);
                    indices[i * 6 + 4] = Idx(e.disabledOn);
                    indices[i * 6 + 5] = Idx(e.disabledIndeterminate);
                }
                var type = target.GetType();
                type.GetField("theme").SetValue(target, runtime);
                type.GetField("colored").SetValue(target, colored);
                type.GetField("colorIndices").SetValue(target, indices);
                var extraField = type.GetField("extraColors");
                if (extraField != null) extraField.SetValue(target, sc.extra.Select(Idx).ToArray());
                sixState.Add(target);
            }
            // Static bindings.
            var bindings = root.GetComponentsInChildren<M3ColorBinding>(true)
                .Where(b => b.Target != null && !dynamicGraphics.Contains(b.Target)).ToArray();
            var graphics = new Graphic[bindings.Length];
            var graphicColors = new int[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                graphics[i] = bindings[i].Target;
                graphicColors[i] = Idx(bindings[i].color);
            }

            // Resolve every scheme.
            var schemes = theme.schemes.Select(s => SchemeBuilder.Resolve(s.ToSpec())).ToList();
            var colors = new Color[schemes.Count * palette.Count];
            for (int s = 0; s < schemes.Count; s++)
            {
                var scheme = schemes[s];
                for (int i = 0; i < palette.Count; i++)
                {
                    palette[i].Resolve(role => scheme[role], scheme.Shadow, out float r, out float g, out float b, out float a);
                    colors[s * palette.Count + i] = new Color(r, g, b, a);
                }
            }

            Undo.RecordObject(runtime, "Bake M3 theme");
            runtime.schemeNames = theme.schemes.Select(s => s.name).ToArray();
            runtime.paletteSize = palette.Count;
            runtime.palettes = colors;
            runtime.currentScheme = Mathf.Clamp(theme.defaultScheme, 0, Mathf.Max(0, schemes.Count - 1));
            runtime.graphics = graphics;
            runtime.graphicColors = graphicColors;
#if UDONSHARP
            runtime.listeners = root.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)
                .Where(b => b != runtime && b.GetType().Namespace == "M3E4Unity.Udon").ToArray();
#else
            runtime.listeners = root.GetComponentsInChildren<M3BehaviourBase>(true)
                .Where(b => b != runtime && b.GetType().Namespace == "M3E4Unity.Udon").ToArray();
#endif

            // Show the default scheme in the editor right away.
            int off = runtime.currentScheme * palette.Count;
            for (int i = 0; i < graphics.Length; i++)
            {
                Undo.RecordObject(graphics[i], "Bake M3 theme");
                graphics[i].color = colors[off + graphicColors[i]];
                EditorUtility.SetDirty(graphics[i]);
            }
            foreach (var it in interactives)
            {
                if (it.colored == null) continue;
                bool enabled = it.selectable == null || it.selectable.interactable;
                int slot = (enabled ? 0 : 2) + (it.selected ? 1 : 0);
                for (int i = 0; i < it.colored.Length; i++)
                {
                    int idx = it.colorIndices[i * 4 + slot];
                    if (idx < 0 || it.colored[i] == null) continue;
                    it.colored[i].color = colors[off + idx];
                    EditorUtility.SetDirty(it.colored[i]);
                }
                if (it.ripple != null && it.rippleColor >= 0) it.ripple.color = colors[off + it.rippleColor];
                EditorUtility.SetDirty(it);
                M3UdonBridge.Sync(it);
            }
            foreach (var target in sixState)
            {
                var type = target.GetType();
                var colored = (Graphic[])type.GetField("colored").GetValue(target);
                var indices = (int[])type.GetField("colorIndices").GetValue(target);
                var selectableField = type.GetField("selectable") ?? type.GetField("slider");
                var selectable = selectableField?.GetValue(target) as Selectable;
                bool enabled = selectable == null || selectable.interactable;
                int state = type.GetField("state") != null ? (int)type.GetField("state").GetValue(target) : 0;
                int slot = state + (enabled ? 0 : 3);
                for (int i = 0; i < colored.Length; i++)
                {
                    int idx = indices[i * 6 + slot];
                    if (idx < 0 || colored[i] == null) continue;
                    colored[i].color = colors[off + idx];
                    EditorUtility.SetDirty(colored[i]);
                }
                // text fields: caret / selection colors (not settable from Udon at runtime)
                var extra = type.GetField("extraColors")?.GetValue(target) as int[];
                if (type.GetField("input")?.GetValue(target) is TMPro.TMP_InputField input && extra != null && extra.Length >= 3)
                {
                    bool err = type.GetField("isError") != null && (bool)type.GetField("isError").GetValue(target);
                    int caret = err ? extra[1] : extra[0];
                    Undo.RecordObject(input, "Bake M3 theme");
                    if (caret >= 0) input.caretColor = colors[off + caret];
                    if (extra[2] >= 0) input.selectionColor = colors[off + extra[2]];
                    EditorUtility.SetDirty(input);
                }
                // several fields (time input): extra[0] caret, extra[2] selection for each
                if (type.GetField("inputs")?.GetValue(target) is TMPro.TMP_InputField[] inputs && extra != null && extra.Length >= 3)
                {
                    foreach (var field in inputs)
                    {
                        if (field == null) continue;
                        Undo.RecordObject(field, "Bake M3 theme");
                        if (extra[0] >= 0) field.caretColor = colors[off + extra[0]];
                        if (extra[2] >= 0) field.selectionColor = colors[off + extra[2]];
                        EditorUtility.SetDirty(field);
                    }
                }
                EditorUtility.SetDirty(target);
                M3UdonBridge.Sync(target);
            }            // let behaviours lay themselves out with the baked colors (editor preview)
            foreach (var target in sixState)
            {
                var refresh = target.GetType().GetMethod("_M3Refresh");
                if (refresh != null && !(target is M3Selection)) refresh.Invoke(target, null);
                M3UdonBridge.Sync(target);
            }
            // every other runtime behaviour lays itself out once with the baked theme (editor preview)
            foreach (var behaviour in runtime.listeners)
            {
                if (behaviour == null || behaviour is M3Interactive || behaviour is M3Selection || sixState.Contains(behaviour)) continue;
                var themeField = behaviour.GetType().GetField("theme");
                if (themeField != null && themeField.FieldType == typeof(M3Theme)) themeField.SetValue(behaviour, runtime);
                var refresh = behaviour.GetType().GetMethod("_M3Refresh");
                if (refresh == null) continue;
                refresh.Invoke(behaviour, null);
                EditorUtility.SetDirty(behaviour);
                M3UdonBridge.Sync(behaviour);
            }
            EditorUtility.SetDirty(runtime);
            M3UdonBridge.Sync(runtime);
        }

        /// <summary>The M3 behaviour on go that has theme / colored / colorIndices fields.</summary>
        static Component SixStateTarget(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null || c.GetType().Namespace != "M3E4Unity.Udon" || c is M3Interactive) continue;
                var t = c.GetType();
                if (t.GetField("colored") != null && t.GetField("colorIndices") != null && t.GetField("theme") != null) return c;
            }
            return null;
        }

        /// <summary>Resolves a single ColorRef against a theme scheme (for previews and tools).</summary>
        public static Color Resolve(ColorRef c, M3ThemeAsset theme, int scheme)
        {
            var s = theme.Resolve(scheme);
            c.Resolve(role => s[role], s.Shadow, out float r, out float g, out float b, out float a);
            return new Color(r, g, b, a);
        }
    }
}
