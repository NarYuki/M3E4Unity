using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>Button groups (ButtonGroup.kt) and split buttons (SplitButton.kt).</summary>
    public static class M3ButtonGroups
    {
        /// <summary>ButtonDefaults.ContentPadding end padding: the default compressionLimit of animateWidth.</summary>
        const float DefaultCompressionLimit = 24f;

        static M3ButtonGroup Group(RectTransform root, List<RectTransform> items, float spacing, M3Context ctx, bool singleSelect)
        {
            var group = M3UdonBridge.Add<M3ButtonGroup>(root.gameObject);
            group.items = items.ToArray();
            group.baseWidths = items.ConvertAll(i => i.sizeDelta.x).ToArray();
            group.compressionLimits = items.ConvertAll(_ => DefaultCompressionLimit).ToArray();
            group.spacing = spacing;
            group.expandedRatio = 0.15f; // ButtonGroupDefaults.ExpandedRatio
            var spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial);
            group.springDamping = spring.DampingRatio;
            group.springStiffness = spring.Stiffness;
            group.singleSelect = singleSelect;
            group.toggles = items.ConvertAll(i => i.GetComponent<M3Interactive>()).ToArray();
            for (int i = 0; i < items.Count && i < 12; i++)
            {
                M3UdonBridge.WirePointer(items[i].gameObject, EventTriggerType.PointerDown, group, "_Down" + i);
                M3UdonBridge.WirePointer(items[i].gameObject, EventTriggerType.PointerUp, group, "_Up" + i);
            }
            float total = 0f, height = 0f;
            foreach (var i in items)
            {
                total += i.sizeDelta.x;
                height = Mathf.Max(height, i.sizeDelta.y);
            }
            total += spacing * (items.Count - 1);
            root.sizeDelta = new Vector2(total, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = total;
            le.preferredHeight = le.minHeight = height;
            group.Layout();
            M3UdonBridge.Sync(group);
            return group;
        }

        static void Detach(RectTransform item)
        {
            var le = item.GetComponent<LayoutElement>();
            if (le == null) le = item.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        /// <summary>A standard button group: buttons spaced by ButtonGroupSmallTokens.BetweenSpace that grow when pressed.</summary>
        public static RectTransform Standard(Transform parent, M3Context ctx, string[] labels, ButtonStyle style = ButtonStyle.Filled,
            ButtonSize size = ButtonSize.Small, string[] icons = null)
        {
            var root = M3Build.Rect("ButtonGroup", parent);
            var items = new List<RectTransform>();
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform item = labels[i] != null
                    ? M3Buttons.Create(root, ctx, labels[i], style, size, leadingIcon: icons != null ? icons[i] : null)
                    : M3IconButtons.Create(root, ctx, icons[i], style == ButtonStyle.Tonal ? IconButtonStyle.Tonal : style == ButtonStyle.Outlined ? IconButtonStyle.Outlined : IconButtonStyle.Filled, size);
                Detach(item);
                items.Add(item);
            }
            Group(root, items, ButtonGroupSmallTokens.BetweenSpace, ctx, false);
            return root;
        }

        /// <summary>
        /// A connected button group (toggle buttons with leading / middle / trailing shapes, 2 dp apart).
        /// selected: initially selected indices; singleSelect makes it behave like a segmented control.
        /// </summary>
        public static RectTransform Connected(Transform parent, M3Context ctx, string[] labels, string[] icons = null,
            int[] selected = null, bool singleSelect = true, ButtonStyle style = ButtonStyle.Tonal, ButtonSize size = ButtonSize.Small)
        {
            var root = M3Build.Rect("ConnectedButtonGroup", parent);
            var items = new List<RectTransform>();
            var sel = new HashSet<int>(selected ?? new int[0]);
            for (int i = 0; i < labels.Length; i++)
            {
                var seg = i == 0 ? ButtonSegment.ConnectedLeading : i == labels.Length - 1 ? ButtonSegment.ConnectedTrailing : ButtonSegment.ConnectedMiddle;
                var item = M3Buttons.Create(root, ctx, labels[i], style, size, leadingIcon: icons != null ? icons[i] : null,
                    toggle: true, selected: sel.Contains(i), segment: seg);
                Detach(item);
                items.Add(item);
            }
            Group(root, items, ConnectedButtonGroupSmallTokens.BetweenSpace, ctx, singleSelect);
            return root;
        }

        /// <summary>A split button: a leading action button and a trailing toggle (menu) button.</summary>
        public static RectTransform Split(Transform parent, M3Context ctx, string label, string leadingIcon = "edit",
            ButtonStyle style = ButtonStyle.Filled, ButtonSize size = ButtonSize.Small, bool checkedTrailing = false)
        {
            var root = M3Build.Rect("SplitButton", parent);
            var lead = M3Buttons.Create(root, ctx, label, style, size, leadingIcon: leadingIcon, segment: ButtonSegment.SplitLeading);
            var trail = M3Buttons.Create(root, ctx, null, style, size, trailingIcon: "keyboard_arrow_down", selected: checkedTrailing, segment: ButtonSegment.SplitTrailing);
            // SplitButtonSample: the arrow turns 180° while the menu (checked) is open
            var it = trail.GetComponent<M3Interactive>();
            foreach (var t in trail.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name != "TrailingIcon" || it == null) continue;
                it.rotateTarget = t;
                t.localEulerAngles = new Vector3(0f, 0f, checkedTrailing ? -180f : 0f);
                M3UdonBridge.Sync(it);
            }
            float spacing = SplitButtonSmallTokens.BetweenSpace;
            var row = M3Build.Row(root, spacing);
            row.childControlWidth = false;
            row.childControlHeight = false;
            float w = lead.sizeDelta.x + spacing + trail.sizeDelta.x;
            root.sizeDelta = new Vector2(w, lead.sizeDelta.y);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = lead.sizeDelta.y;
            return root;
        }
    }
}
