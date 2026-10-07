using System.Collections.Generic;
using M3E4Unity.Tokens;
using M3E4Unity.Udon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Segmented buttons (SegmentedButton.kt): Single / MultiChoiceSegmentedButtonRow of equal-width
    /// segments (width(IntrinsicSize.Min) + weight(1f)) overlapping by the border width. The check
    /// icon appears when selected and the label slides from the centered position
    /// (SegmentedButtonContentMeasurePolicy, FastSpatial).
    /// </summary>
    public static class M3SegmentedButtons
    {
        const float MinWidth = 58f;     // ButtonDefaults.MinWidth
        const float PadH = 12f;         // SegmentedButtonDefaults.ContentPadding start / end
        const float IconSpacing = 8f;

        public static RectTransform Create(Transform parent, M3Context ctx, string[] labels, bool[] selected, bool singleChoice = true,
            bool[] enabled = null)
        {
            int n = labels.Length;
            float height = OutlinedSegmentedButtonTokens.ContainerHeight;
            float iconSize = OutlinedSegmentedButtonTokens.IconSize;
            float border = OutlinedSegmentedButtonTokens.OutlineWidth;
            var bg = ctx.Background;

            // intrinsic width of a segment: icon slot + spacing + label + paddings (all segments get the widest)
            float segW = MinWidth;
            var probeRoot = M3Build.Rect("Probe", parent);
            for (int i = 0; i < n; i++)
            {
                var probe = M3Build.Text("Probe", probeRoot, labels[i], OutlinedSegmentedButtonTokens.LabelTextFont, ColorRef.Clear, ctx);
                segW = Mathf.Max(segW, PadH + iconSize + IconSpacing + Mathf.Ceil(probe.GetPreferredValues(labels[i]).x) + PadH);
            }
            Object.DestroyImmediate(probeRoot.gameObject);
            float width = n * segW - (n - 1) * border;

            var root = M3Build.Rect(singleChoice ? "SingleChoiceSegmentedButtonRow" : "MultiChoiceSegmentedButtonRow", parent);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;

            float full = height / 2f; // baseShape = CornerFull
            var interactives = new List<M3Interactive>();
            for (int i = 0; i < n; i++)
            {
                bool first = n > 1 && i == 0, last = n > 1 && i == n - 1, only = n == 1;
                // SegmentedButtonDefaults.itemShape: start(), RectangleShape, end()
                var shape = only ? new CornerShape(full) : first ? new CornerShape(full, 0f, 0f, full) : last ? new CornerShape(0f, full, full, 0f) : CornerShape.None;
                bool sel = selected != null && selected[i];
                bool en = enabled == null || enabled[i];

                var seg = M3Build.Rect("SegmentedButton", root);
                seg.anchorMin = seg.anchorMax = new Vector2(0f, 0.5f);
                seg.pivot = new Vector2(0f, 0.5f);
                seg.anchoredPosition = new Vector2(i * (segW - border), 0f);
                seg.sizeDelta = new Vector2(segW, height);

                var style = new PressableStyle
                {
                    Container = null,
                    ContainerChecked = OutlinedSegmentedButtonTokens.SelectedContainerColor,
                    // disabledActiveContainerColor = SelectedContainerColor
                    DisabledContainer = OutlinedSegmentedButtonTokens.SelectedContainerColor,
                    DisabledContainerOpacity = 1f,
                    Content = OutlinedSegmentedButtonTokens.UnselectedLabelTextColor,
                    ContentChecked = OutlinedSegmentedButtonTokens.SelectedLabelTextColor,
                    DisabledContent = OutlinedSegmentedButtonTokens.DisabledLabelTextColor,
                    DisabledContentOpacity = OutlinedSegmentedButtonTokens.DisabledLabelTextOpacity,
                    Outline = OutlinedSegmentedButtonTokens.OutlineColor,
                    OutlineWidth = border,
                    DisabledOutlineOpacity = OutlinedSegmentedButtonTokens.DisabledOutlineOpacity,
                    OutlineWhenChecked = true,
                    Toggle = true,
                    Selected = sel,
                    Enabled = en,
                    // the shape does not change; the 0 → 1 "shape" value drives the label offset animation
                    RestShape = 0f, PressedShape = 0f, CheckedShape = 1f, CheckedPressedShape = 1f, MaxShape = 1f,
                    Spring = MotionScheme.Get(ctx.Motion, MotionRole.FastSpatial),
                    ShapeFor = _ => shape,
                };
                var p = new M3Pressable(seg, ctx, style);

                var content = M3Build.Rect("Content", seg);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                content.pivot = new Vector2(0.5f, 0.5f);
                var probe = M3Build.Text("Label", content, labels[i], OutlinedSegmentedButtonTokens.LabelTextFont, ColorRef.Role(style.Content), ctx);
                float lw = Mathf.Ceil(probe.GetPreferredValues(labels[i]).x);
                float cw = iconSize + IconSpacing + lw;
                content.sizeDelta = new Vector2(cw, height);
                var label = probe;
                label.raycastTarget = false;
                label.alignment = TextAlignmentOptions.Left;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
                lrt.sizeDelta = new Vector2(lw, 0f);
                p.Content(label);

                var check = M3Build.Icon("Check", content, "check", iconSize, ColorRef.Role(style.ContentChecked.Value), ctx);
                var crt = check.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
                crt.pivot = new Vector2(0f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                p.Content(check);
                p.Spec.ShowWhenSelected.Add(check.gameObject);

                // label x = IconSize + IconSpacing + offset, offset -(IconSize + IconSpacing) / 2 without the icon
                var labelHolder = M3Build.Rect("LabelOffset", content);
                labelHolder.anchorMin = new Vector2(0f, 0f); labelHolder.anchorMax = new Vector2(0f, 1f);
                labelHolder.pivot = new Vector2(0f, 0.5f);
                labelHolder.sizeDelta = new Vector2(lw, 0f);
                lrt.SetParent(labelHolder, false);
                lrt.anchoredPosition = Vector2.zero;
                float offUnchecked = -(iconSize + IconSpacing) / 2f;
                p.Spec.OpticalContent = labelHolder;
                p.Spec.OpticalA = iconSize + IconSpacing + offUnchecked;   // value 0 (unchecked)
                p.Spec.OpticalB = -offUnchecked;                           // value 1 (checked)
                p.Spec.OpticalMin = -1000f;
                p.Spec.OpticalMax = 1000f;
                labelHolder.anchoredPosition = new Vector2(p.Spec.OpticalA + (sel ? p.Spec.OpticalB : 0f), 0f);
                interactives.Add(p.Finish());
            }
            if (singleChoice)
                foreach (var it in interactives)
                {
                    it.exclusiveWith = interactives.ToArray();
                    M3UdonBridge.Sync(it);
                }
            return root;
        }
    }
}
