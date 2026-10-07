using UnityEngine;
using UnityEngine.UI;
#if UDONSHARP
using VRC.SDKBase;
#endif

namespace M3E4Unity
{
    /// <summary>
    /// Editor-time marker: the Graphic on this object takes <see cref="color"/> from the theme.
    /// The theme baker turns these into palette entries of the runtime <c>M3Theme</c>; in
    /// VRChat builds the marker is stripped (IEditorOnly).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("M3E4Unity/Color Binding")]
    public sealed class M3ColorBinding : MonoBehaviour
#if UDONSHARP
        , IEditorOnly
#endif
    {
        public ColorRef color = ColorRef.Role(Tokens.ColorRole.Primary);

        public Graphic Target => GetComponent<Graphic>();
    }
}
