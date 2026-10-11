#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Pull to refresh (PullToRefresh.kt: PullToRefreshBox, PullToRefreshModifierNode,
    /// PullToRefreshDefaults.Indicator / LoadingIndicator) over a vertical ScrollRect.
    /// <list type="bullet">
    /// <item>Pulling down while the list is at its top adds to distancePulled; the indicator offset is
    /// distancePulled * 0.5 up to the threshold, then grows with the non-linear tension (up to 200 %
    /// overshoot). distanceFraction = offset / threshold follows the pull without animation.</item>
    /// <item>On release past the threshold the refresh starts (_M3PullRefresh to the listener) and the
    /// state animates to 1; otherwise, and when the refresh ends (_FinishRefresh / SetRefreshing(false)),
    /// to 0. Both use Animatable.animateTo's default spring() (no bounce, StiffnessMedium).</item>
    /// <item>IndicatorBox: translationY = distanceFraction * maxDistance - height, clipped at the top
    /// edge of the box, shadow while pulled or refreshing. The content cross-fades (DefaultEffects)
    /// between the pull indicator (arrow with alpha 0.3 / 1 at the threshold, DefaultEffects; or a
    /// determinate contained loading indicator, rotated by -(fraction - 1) * 180 past the threshold) and
    /// the refreshing indicator (indeterminate, started when it appears).</item>
    /// </list>
    /// The pointer is polled while the ScrollRect is dragged (_BeginDrag / _EndDrag from an EventTrigger
    /// on it): the mouse / touch outside VRChat, the head or hand ray inside.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3PullToRefresh : UdonSharpBehaviour
#else
    public class M3PullToRefresh : M3BehaviourBase
#endif
    {
        public ScrollRect scrollRect;
        [Tooltip("Rect the pointer is measured in (the scroll viewport).")]
        public RectTransform viewport;
        public bool refreshing;
        public bool enabledPull = true;
#if UDONSHARP
        public UdonSharpBehaviour refreshListener;
#else
        public M3BehaviourBase refreshListener;
#endif
        [Tooltip("Demo: end the refresh by itself after this many seconds (0 = the listener calls _FinishRefresh).")]
        public float demoRefreshSeconds;

        [Header("Indicator")]
        [Tooltip("0 = Indicator (arrow / circular), 1 = LoadingIndicator (contained, expressive).")]
        public int style;
        public RectTransform indicatorBox;
        public float boxHeight = 40f;
        public float maxDistance = 80f;          // IndicatorMaxDistance
        public float threshold = 80f;            // PositionalThreshold
        public Image[] shadows;
        public CanvasGroup pullGroup;
        public CanvasGroup arrowAlphaGroup;
        public CanvasGroup refreshGroup;
        public Material pullMaterial;
        public Image[] pullImages;
        public RectTransform pullRotate;
        public Material refreshMaterial;
        public Image[] refreshImages;

        [Header("Motion")]
        public float stateStiffness = 1500f;     // spring(): StiffnessMedium, no bounce
        public float effectsStiffness = 1600f;   // DefaultEffects

        float fraction;
        float fractionV;
        bool animating;
        float animTarget;
        float distancePulled;
        bool dragging;
        float baseline;
        float pointerY;
        int raySource;
        float crossfade;     // 0 = pull indicator, 1 = refreshing indicator
        float crossfadeV;
        float arrowAlpha = 0.3f;
        float arrowAlphaV;
        float refreshEndTime;
        float lastTime;
        bool ticking;

        void Start()
        {
#if !UDONSHARP
            if (pullMaterial != null) { pullMaterial = new Material(pullMaterial); SetMaterial(pullImages, pullMaterial); }
            if (refreshMaterial != null) { refreshMaterial = new Material(refreshMaterial); SetMaterial(refreshImages, refreshMaterial); }
#endif
            fraction = refreshing ? 1f : 0f;
            crossfade = refreshing ? 1f : 0f;
            arrowAlpha = fraction >= 1f ? 1f : 0.3f;
            if (refreshing) StampRefreshStart();
            Apply();
        }

        void SetMaterial(Image[] images, Material m)
        {
            if (images == null) return;
            for (int i = 0; i < images.Length; i++) if (images[i] != null) images[i].material = m;
        }

        public void _M3ThemeChanged() { }

        // ---- refresh state ----

        public void _StartRefresh() { SetRefreshing(true); }
        public void _FinishRefresh() { SetRefreshing(false); }

        public void SetRefreshing(bool value)
        {
            if (value == refreshing) return;
            refreshing = value;
            if (value)
            {
                StampRefreshStart();
                refreshEndTime = demoRefreshSeconds > 0f ? Time.time + demoRefreshSeconds : -1f;
            }
            // PullToRefreshModifierNode.update(): animateToThreshold / animateToHidden
            AnimateTo(value ? 1f : 0f);
        }

        void StampRefreshStart()
        {
            // the indeterminate indicator starts its cycle now (shader time = _Time.y - w)
            Vector4 v = new Vector4(0f, 0f, 0f, Time.timeSinceLevelLoad);
            SetDyn(refreshMaterial, refreshImages, v);
        }

        void SetDyn(Material m, Image[] images, Vector4 v)
        {
            if (m == null) return;
            m.SetVector("_M3Dyn", v);
            if (images == null) return;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                Material r = images[i].materialForRendering;
                if (r != null && r != m) r.SetVector("_M3Dyn", v);
            }
        }

        void AnimateTo(float target)
        {
            animTarget = target;
            animating = true;
            Kick();
        }

        // ---- drag ----

        public void _BeginDrag()
        {
            if (!enabledPull) return;
            if (!PointerY(true)) return;
            dragging = true;
            baseline = pointerY;
            distancePulled = 0f;
            Kick();
        }

        public void _EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            if (refreshing || animating) { distancePulled = 0f; return; }
            // onRelease: refresh past the threshold, then animateToHidden (the refresh state takes over)
            bool trigger = distancePulled * 0.5f > threshold;
            distancePulled = 0f;
            AnimateTo(0f);
            if (trigger)
            {
                if (refreshListener != null) refreshListener.SendCustomEvent("_M3PullRefresh");
                SetRefreshing(true);
            }
        }

        void PollDrag()
        {
            if (!PointerY(false)) return;
            if (refreshing || animating) { baseline = pointerY; return; }
            RectTransform content = scrollRect != null ? scrollRect.content : null;
            float scrolled = content != null ? content.anchoredPosition.y : 0f;
            if (scrolled > 0.5f && distancePulled <= 0f)
            {
                // the list scrolls first; the pull starts where it reaches the top
                baseline = pointerY;
                return;
            }
            distancePulled = Mathf.Max(0f, baseline - pointerY);
            if (distancePulled > 0f && content != null)
            {
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
                scrollRect.velocity = Vector2.zero;
            }
            // state.snapTo(verticalOffset / threshold)
            fraction = VerticalOffset() / threshold;
            fractionV = 0f;
        }

        float VerticalOffset()
        {
            float adjusted = distancePulled * 0.5f; // DragMultiplier
            if (adjusted <= threshold) return adjusted;
            float overshoot = adjusted / threshold - 1f;
            float linear = Mathf.Clamp(overshoot, 0f, 2f);
            float tension = linear - linear * linear / 4f;
            return threshold + threshold * tension;
        }

        /// <summary>The pointer's y in the viewport (dp, up), into pointerY.</summary>
        bool PointerY(bool press)
        {
            RectTransform rt = viewport;
            if (rt == null) return false;
#if UDONSHARP
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null) return false;
            if (press)
            {
                if (player.IsUserInVR())
                {
                    if (Ray(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand), true)) { raySource = 1; return true; }
                    if (Ray(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand), true)) { raySource = 2; return true; }
                    return false;
                }
                raySource = 0;
                return Ray(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head), true);
            }
            VRCPlayerApi.TrackingDataType type = VRCPlayerApi.TrackingDataType.Head;
            if (raySource == 1) type = VRCPlayerApi.TrackingDataType.RightHand;
            else if (raySource == 2) type = VRCPlayerApi.TrackingDataType.LeftHand;
            return Ray(rt, player.GetTrackingData(type), false);
#else
            Vector2 screen = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out local)) return false;
            pointerY = local.y;
            return true;
#endif
        }

#if UDONSHARP
        bool Ray(RectTransform rt, VRCPlayerApi.TrackingData data, bool mustHit)
        {
            Vector3 dir = data.rotation * Vector3.forward;
            Vector3 n = rt.forward;
            float denom = Vector3.Dot(dir, n);
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = Vector3.Dot(rt.position - data.position, n) / denom;
            if (t < 0f) return false;
            Vector3 local = rt.InverseTransformPoint(data.position + dir * t);
            if (mustHit && !rt.rect.Contains(new Vector2(local.x, local.y))) return false;
            pointerY = local.y;
            return true;
        }
#endif

        // ---- animation ----

        void Kick()
        {
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        float stepX;
        float stepV;

        bool Critical(float x0, float v0, float dt, float k, float threshold)
        {
            float w = Mathf.Sqrt(k);
            float cb = v0 + w * x0;
            float e = Mathf.Exp(-w * dt);
            stepX = (x0 + cb * dt) * e;
            stepV = (cb - w * (x0 + cb * dt)) * e;
            if (Mathf.Abs(stepX) < threshold && Mathf.Abs(stepV) < threshold * 10f) { stepX = 0f; stepV = 0f; return false; }
            return true;
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            if (dragging) { PollDrag(); busy = true; }
            if (animating)
            {
                // Animatable visibility threshold for floats: 0.01
                if (Critical(fraction - animTarget, fractionV, dt, stateStiffness, 0.01f)) busy = true;
                else animating = false;
                fraction = animTarget + stepX; fractionV = stepV;
            }
            // Crossfade(targetState = isRefreshing, DefaultEffects)
            float ct = refreshing ? 1f : 0f;
            if (Critical(crossfade - ct, crossfadeV, dt, effectsStiffness, 0.002f)) busy = true;
            crossfade = ct + stepX; crossfadeV = stepV;
            // arrow alpha: MaxAlpha at the threshold, MinAlpha below, DefaultEffects
            float at = fraction >= 1f ? 1f : 0.3f;
            if (Critical(arrowAlpha - at, arrowAlphaV, dt, effectsStiffness, 0.002f)) busy = true;
            arrowAlpha = at + stepX; arrowAlphaV = stepV;
            if (refreshing && refreshEndTime > 0f)
            {
                busy = true;
                if (now >= refreshEndTime) { refreshEndTime = -1f; SetRefreshing(false); }
            }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            float f = Mathf.Max(0f, fraction);
            if (indicatorBox != null)
                indicatorBox.anchoredPosition = new Vector2(indicatorBox.anchoredPosition.x, -(f * maxDistance - boxHeight));
            bool visible = f > 0f || refreshing;
            if (shadows != null)
                for (int i = 0; i < shadows.Length; i++) if (shadows[i] != null) shadows[i].enabled = visible;
            if (pullGroup != null) pullGroup.alpha = Mathf.Clamp01(1f - crossfade);
            if (refreshGroup != null) refreshGroup.alpha = Mathf.Clamp01(crossfade);
            if (arrowAlphaGroup != null) arrowAlphaGroup.alpha = Mathf.Clamp01(arrowAlpha);
            if (style == 0) SetDyn(pullMaterial, pullImages, new Vector4(f, 0f, 0f, 0f));
            else
            {
                SetDyn(pullMaterial, pullImages, new Vector4(Mathf.Clamp01(f), 0f, 0f, 0f));
                // past the threshold the whole indicator turns: rotate(-(progress - 1) * 180)
                if (pullRotate != null) pullRotate.localEulerAngles = new Vector3(0f, 0f, f > 1f ? (f - 1f) * 180f : 0f);
            }
        }
    }
}
