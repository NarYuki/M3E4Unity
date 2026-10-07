using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UDONSHARP
using VRC.SDKBase;
#endif

namespace M3E4Unity
{
    /// <summary>Colors of one graphic in the four interaction color states.</summary>
    [Serializable]
    public struct StateColorEntry
    {
        public Graphic graphic;
        public ColorRef enabled;
        public ColorRef enabledSelected;
        public ColorRef disabled;
        public ColorRef disabledSelected;
    }

    /// <summary>
    /// Editor-time marker next to an <c>M3Interactive</c>: per-state theme colors of its parts.
    /// The theme baker writes them as palette indices into the interactive behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("M3E4Unity/State Colors")]
    public sealed class M3StateColors : MonoBehaviour
#if UDONSHARP
        , IEditorOnly
#endif
    {
        public List<StateColorEntry> entries = new List<StateColorEntry>();
        public ColorRef ripple;
        public ColorRef rippleSelected;
        public bool hasRipple;
    }
}
