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

        /// <summary>Screen position and camera of the latest press, read by the ripple (touch point origin).</summary>
        public static Vector2 LastPressPosition;
        public static Camera LastPressCamera;
        public static int LastPressFrame = -1;

        public void OnPointerDown(PointerEventData eventData)
        {
            LastPressPosition = eventData.position;
            LastPressCamera = eventData.pressEventCamera;
            LastPressFrame = Time.frameCount;
            onPointerDown.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => onPointerUp.Invoke();
        public void OnPointerEnter(PointerEventData eventData) => onPointerEnter.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onPointerExit.Invoke();
    }
}
#endif
