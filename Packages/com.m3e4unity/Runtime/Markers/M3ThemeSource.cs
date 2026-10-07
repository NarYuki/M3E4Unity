using UnityEngine;
#if UDONSHARP
using VRC.SDKBase;
#endif

namespace M3E4Unity
{
    /// <summary>Editor-time marker on an M3 UI root: which theme asset the baker uses.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("M3E4Unity/Theme Source")]
    public sealed class M3ThemeSource : MonoBehaviour
#if UDONSHARP
        , IEditorOnly
#endif
    {
        public M3ThemeAsset theme;
    }
}
