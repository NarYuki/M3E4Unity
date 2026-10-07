using M3E4Unity.Tokens;
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Editor
{
    public enum ChipKind { Assist, Filter, Input, Suggestion }

    /// <summary>Assist, filter, input and suggestion chips (Chip.kt).</summary>
    public static class M3Chips
    {
        /// <summary>
        /// ChipContent: Row(padding = contentPadding, spacedBy(8 dp)) of [leading or 0-width spacer, label,
        /// trailing or 0-width spacer]; the empty spacers still take part in the spacing.
        /// </summary>
        public static RectTransform Create(Transform parent, M3Context ctx, ChipKind kind, string label,
            string leadingIcon = null, string trailingIcon = null, bool elevated = false,
            bool selected = false, bool enabled = true, bool checkmarkWhenSelected = true)
        {
            var bg = ctx.Background;
            float height = AssistChipTokens.ContainerHeight;
            float iconSize = AssistChipTokens.IconSize;
            float radius = ShapeScale.Get(AssistChipTokens.ContainerShape).TopStart;
            bool toggle = kind == ChipKind.Filter || kind == ChipKind.Input;

            var style = new PressableStyle
            {
                Height = height,
                RestShape = radius, PressedShape = radius, CheckedShape = radius, MaxShape = radius,
                Toggle = toggle,
                Selected = selected,
                Enabled = enabled,
                DisabledContent = ColorRole.OnSurface,
                DisabledContentOpacity = 0.38f,
                OutlineWidth = 1f,
            };
            ColorRole labelColor, leadingColor, trailingColor, labelSelected = ColorRole.OnSecondaryContainer,
                leadingSelected = ColorRole.OnSecondaryContainer, trailingSelected = ColorRole.OnSecondaryContainer;
            switch (kind)
            {
                case ChipKind.Assist:
                    labelColor = AssistChipTokens.LabelTextColor;
                    leadingColor = trailingColor = AssistChipTokens.IconColor;
                    if (elevated)
                    {
                        style.Container = AssistChipTokens.ElevatedContainerColor;
                        style.DisabledContainer = AssistChipTokens.ElevatedDisabledContainerColor;
                        style.DisabledContainerOpacity = AssistChipTokens.ElevatedDisabledContainerOpacity;
                        style.Elevation = Levels(AssistChipTokens.ElevatedContainerElevation, AssistChipTokens.ElevatedHoverContainerElevation,
                            AssistChipTokens.ElevatedPressedContainerElevation, AssistChipTokens.ElevatedDisabledContainerElevation);
                    }
                    else
                    {
                        // assistChipBorder: FlatOutlineColor / FlatDisabledOutlineColor at 0.12
                        style.Outline = AssistChipTokens.FlatOutlineColor;
                        style.DisabledOutlineOpacity = AssistChipTokens.FlatDisabledOutlineOpacity;
                    }
                    break;
                case ChipKind.Suggestion:
                    labelColor = SuggestionChipTokens.LabelTextColor;
                    leadingColor = trailingColor = SuggestionChipTokens.LeadingIconColor;
                    if (elevated)
                    {
                        style.Container = SuggestionChipTokens.ElevatedContainerColor;
                        style.DisabledContainer = SuggestionChipTokens.ElevatedDisabledContainerColor;
                        style.DisabledContainerOpacity = AssistChipTokens.ElevatedDisabledContainerOpacity;
                        style.Elevation = Levels(SuggestionChipTokens.ElevatedContainerElevation, SuggestionChipTokens.ElevatedHoverContainerElevation,
                            SuggestionChipTokens.ElevatedPressedContainerElevation, SuggestionChipTokens.ElevatedDisabledContainerElevation);
                    }
                    else
                    {
                        style.Outline = SuggestionChipTokens.FlatOutlineColor;
                        style.DisabledOutlineOpacity = SuggestionChipTokens.FlatDisabledOutlineOpacity;
                    }
                    break;
                case ChipKind.Input:
                    labelColor = InputChipTokens.UnselectedLabelTextColor;
                    // .copy(leadingIconColor = ChipsTokens.UnselectedLeadingIconColor)
                    leadingColor = ChipsTokens.UnselectedLeadingIconColor;
                    trailingColor = InputChipTokens.UnselectedTrailingIconColor;
                    labelSelected = InputChipTokens.SelectedLabelTextColor;
                    leadingSelected = InputChipTokens.SelectedLeadingIconColor;
                    trailingSelected = InputChipTokens.SelectedTrailingIconColor;
                    style.ContainerChecked = InputChipTokens.SelectedContainerColor;
                    style.DisabledContainer = InputChipTokens.DisabledSelectedContainerColor;
                    style.DisabledContainerOpacity = InputChipTokens.DisabledSelectedContainerOpacity;
                    style.Outline = InputChipTokens.UnselectedOutlineColor;
                    style.DisabledOutlineOpacity = InputChipTokens.DisabledUnselectedOutlineOpacity;
                    break;
                default: // Filter
                    labelColor = FilterChipTokens.UnselectedLabelTextColor;
                    leadingColor = ChipsTokens.UnselectedLeadingIconColor;
                    trailingColor = FilterChipTokens.UnselectedTrailingIconColor;
                    labelSelected = FilterChipTokens.SelectedLabelTextColor;
                    leadingSelected = FilterChipTokens.SelectedLeadingIconColor;
                    trailingSelected = FilterChipTokens.SelectedTrailingIconColor;
                    if (elevated)
                    {
                        style.Container = FilterChipTokens.ElevatedUnselectedContainerColor;
                        style.ContainerChecked = FilterChipTokens.ElevatedSelectedContainerColor;
                        style.DisabledContainer = FilterChipTokens.ElevatedDisabledContainerColor;
                        style.DisabledContainerOpacity = FilterChipTokens.ElevatedDisabledContainerOpacity;
                        style.Elevation = Levels(FilterChipTokens.ElevatedContainerElevation, FilterChipTokens.ElevatedHoverContainerElevation,
                            FilterChipTokens.ElevatedPressedContainerElevation, FilterChipTokens.ElevatedDisabledContainerElevation);
                    }
                    else
                    {
                        style.ContainerChecked = FilterChipTokens.FlatSelectedContainerColor;
                        style.DisabledContainer = FilterChipTokens.FlatDisabledSelectedContainerColor;
                        style.DisabledContainerOpacity = FilterChipTokens.FlatDisabledSelectedContainerOpacity;
                        style.Outline = FilterChipTokens.FlatUnselectedOutlineColor;
                        style.DisabledOutlineOpacity = FilterChipTokens.FlatDisabledUnselectedOutlineOpacity;
                    }
                    break;
            }
            style.Content = labelColor;
            style.ContentChecked = labelSelected;
            // flat filter / input chips: the border goes away when selected (selectedBorderWidth 0)
            style.OutlineWhenChecked = false;
            style.DisabledCheckedHasContainer = toggle;

            var root = M3Build.Rect($"Chip ({kind})", parent);
            var row = M3Build.Row(root, 8f);
            int padStart = 8, padEnd = 8;
            bool hasLeading = leadingIcon != null || (kind == ChipKind.Filter && checkmarkWhenSelected);
            if (kind == ChipKind.Input)
            {
                // InputChipDefaults.contentPadding: start 4 (avatar or no icon) / 8, end 8 (trailing) / 4
                padStart = leadingIcon != null ? 8 : 4;
                padEnd = trailingIcon != null ? 8 : 4;
            }
            row.padding = new RectOffset(padStart, padEnd, 0, 0);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            var p = new M3Pressable(root, ctx, style);

            // leading slot
            if (leadingIcon != null)
            {
                var icon = M3Build.Icon("LeadingIcon", root, leadingIcon, iconSize, ColorRef.Role(leadingColor), ctx);
                p.Content(icon, leadingColor, leadingSelected);
            }
            else if (kind == ChipKind.Filter && checkmarkWhenSelected)
            {
                // the usual FilterChip sample: leadingIcon = if (selected) Icons.Filled.Done else null
                var spacer = Spacer(root, "LeadingSpacer");
                var icon = M3Build.Icon("Checkmark", root, "check", iconSize, ColorRef.Role(leadingSelected), ctx);
                p.Content(icon, leadingSelected, leadingSelected);
                p.Spec.ShowWhenSelected.Add(icon.gameObject);
                p.Spec.HideWhenSelected.Add(spacer.gameObject);
            }
            else Spacer(root, "LeadingSpacer");

            p.Content(M3Build.Text("Label", root, label, AssistChipTokens.LabelTextFont, ColorRef.Role(labelColor), ctx), labelColor, labelSelected);

            if (trailingIcon != null)
                p.Content(M3Build.Icon("TrailingIcon", root, trailingIcon, iconSize, ColorRef.Role(trailingColor), ctx), trailingColor, trailingSelected);
            else Spacer(root, "TrailingSpacer");

            p.Finish();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            root.sizeDelta = new Vector2(LayoutUtility.GetPreferredWidth(root), height);
            M3Build.AutoSize(root);
            return root;
        }

        static RectTransform Spacer(Transform parent, string name)
        {
            var rt = M3Build.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 0;
            return rt;
        }

        static int[] Levels(float rest, float hover, float pressed, float disabled) =>
            new[] { Elevation.LevelOf(rest), Elevation.LevelOf(hover), Elevation.LevelOf(pressed), Elevation.LevelOf(disabled) };
    }
}
