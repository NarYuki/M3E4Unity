#if !UDONSHARP
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Outside VRChat, M3E4Unity behaviours derive from this class instead of
    /// UdonSharpBehaviour. It provides the small part of the Udon API the
    /// behaviours use (custom events, delayed events), so the same source runs
    /// unchanged in both environments.
    /// </summary>
    public abstract class M3BehaviourBase : MonoBehaviour
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Calls the public or private method with this name (UdonBehaviour.SendCustomEvent).</summary>
        public void SendCustomEvent(string eventName)
        {
            var method = GetType().GetMethod(eventName, Flags, null, System.Type.EmptyTypes, null);
            if (method != null) method.Invoke(this, null);
            else Debug.LogWarning($"{GetType().Name}: no event '{eventName}'", this);
        }

        public void SendCustomEventDelayedFrames(string eventName, int delayFrames)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(DelayFrames(eventName, delayFrames));
        }

        public void SendCustomEventDelayedSeconds(string eventName, float delaySeconds)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(DelaySeconds(eventName, delaySeconds));
        }

        IEnumerator DelayFrames(string eventName, int frames)
        {
            for (int i = 0; i < Mathf.Max(frames, 1); i++) yield return null;
            SendCustomEvent(eventName);
        }

        IEnumerator DelaySeconds(string eventName, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SendCustomEvent(eventName);
        }
    }
}
#endif
