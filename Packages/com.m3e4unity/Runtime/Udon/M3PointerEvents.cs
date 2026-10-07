#if !UDONSHARP
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Outside VRChat, components receive pointer down / up / enter / exit through this instead of an
    /// EventTrigger. EventTrigger implements every event interface, so it also swallows scroll and drag
    /// events and stops them from reaching a ScrollRect behind the component; this handles only the
    /// four pointer events. (VRChat worlds keep EventTrigger, which is whitelisted there.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class M3PointerEvents : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public UnityEvent onPointerDown = new UnityEvent();
        public UnityEvent onPointerUp = new UnityEvent();
        public UnityEvent onPointerEnter = new UnityEvent();
        public UnityEvent onPointerExit = new UnityEvent();

        public void OnPointerDown(PointerEventData eventData) => onPointerDown.Invoke();
        public void OnPointerUp(PointerEventData eventData) => onPointerUp.Invoke();
        public void OnPointerEnter(PointerEventData eventData) => onPointerEnter.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onPointerExit.Invoke();
    }
}
#endif
