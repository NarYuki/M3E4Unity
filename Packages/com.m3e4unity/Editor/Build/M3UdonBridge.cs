using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if UDONSHARP
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;
#else
using M3E4Unity.Udon;
#endif

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Adds M3E4Unity behaviours and wires uGUI events to them, both in VRChat projects
    /// (UdonSharp proxy + UdonBehaviour.SendCustomEvent) and in plain Unity
    /// (MonoBehaviour.SendCustomEvent from M3BehaviourBase).
    /// </summary>
    public static class M3UdonBridge
    {
#if UDONSHARP
        public static T Add<T>(GameObject go) where T : UdonSharpBehaviour
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return UdonSharpUndo.AddComponent<T>(go);
        }

        static UnityAction<string> Target(Component behaviour)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)behaviour);
            return udon.SendCustomEvent;
        }

        /// <summary>Copies edited proxy fields into the backing UdonBehaviour.</summary>
        public static void Sync(Component behaviour)
        {
            if (behaviour is UdonSharpBehaviour usb) UdonSharpEditorUtility.CopyProxyToUdon(usb);
        }
#else
        public static T Add<T>(GameObject go) where T : M3BehaviourBase
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return go.AddComponent<T>();
        }

        static UnityAction<string> Target(Component behaviour) => ((M3BehaviourBase)behaviour).SendCustomEvent;

        public static void Sync(Component behaviour) { }
#endif

        /// <summary>evt -> behaviour.SendCustomEvent(eventName), as a persistent (serialized) call.</summary>
        public static void Wire(UnityEventBase evt, Component behaviour, string eventName)
        {
            UnityEventTools.AddStringPersistentListener(evt, Target(behaviour), eventName);
        }

        /// <summary>Adds an EventTrigger entry that sends eventName to the behaviour.</summary>
        public static void WireTrigger(EventTrigger trigger, EventTriggerType type, Component behaviour, string eventName)
        {
            var entry = trigger.triggers.Find(e => e.eventID == type);
            if (entry == null)
            {
                entry = new EventTrigger.Entry { eventID = type };
                trigger.triggers.Add(entry);
            }
            Wire(entry.callback, behaviour, eventName);
        }
    }
}
