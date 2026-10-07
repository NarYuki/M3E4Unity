using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UDONSHARP
using VRC.SDKBase;
#endif

namespace M3E4Unity
{
    /// <summary>Colors of one part of a checkbox / radio / switch in its six states.</summary>
    [Serializable]
    public struct SelectionColorEntry
    {
        public Graphic graphic;
        public ColorRef off, on, indeterminate, disabledOff, disabledOn, disabledIndeterminate;
    }

    /// <summary>Editor-time marker next to an <c>M3Selection</c>; baked into palette indices.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("M3E4Unity/Selection Colors")]
    public sealed class M3SelectionColors : MonoBehaviour
#if UDONSHARP
        , IEditorOnly
#endif
    {
        public List<SelectionColorEntry> entries = new List<SelectionColorEntry>();
        /// <summary>Extra colors baked into the target's <c>extraColors</c> palette indices (e.g. caret colors).</summary>
        public List<ColorRef> extra = new List<ColorRef>();
    }
}
