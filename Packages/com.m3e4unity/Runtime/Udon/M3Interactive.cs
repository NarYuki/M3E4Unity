#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Interaction behaviour shared by every pressable M3 component (buttons, icon buttons,
    /// FABs, chips, list items, navigation items...). It reproduces Compose Material 3:
    /// <list type="bullet">
    /// <item>shape morphing between the rest, pressed and selected shapes (rememberAnimatedShape,
    /// springs from the motion scheme), by swapping pre-baked shape frames;</item>
    /// <item>the bounded ripple (RippleAnimation: from the touch point, 75 ms fade in, 225 ms
    /// FastOutSlowIn radius and linear move to the center, 150 ms fade out once the fade in has
    /// finished; up to two overlapping ripples), drawn with a pooled material of the theme;</item>
    /// <item>toggle selection and disabled colors, optionally faded in Oklab with an effects spring
    /// (list items, menu items, toggle borders...);</item>
    /// <item>elevation per state, tweened like animateElevation (in: 120 ms FastOutSlowIn; out:
    /// 150 ms, 120 ms from hover, CubicBezier(0.4, 0, 0.6, 1); disabled snaps).</item>
    /// </list>
    /// Hover and focus state layers are plain uGUI ColorTint transitions and need no script.
    /// The EventTrigger on the same object sends _Down, _Up, _Enter and _Exit.
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3Interactive : UdonSharpBehaviour
#else
    public class M3Interactive : M3BehaviourBase
#endif
    {
        public M3Theme theme;
        public Selectable selectable;

        [Header("Toggle")]
        public bool isToggle;
        public bool selected;
#if UDONSHARP
        [Tooltip("Optional: gets _M3SelectionChanged when the user toggles this component.")]
        public UdonSharpBehaviour selectionListener;
#else
        [Tooltip("Optional: gets _M3SelectionChanged when the user toggles this component.")]
        public M3BehaviourBase selectionListener;
#endif

        [Tooltip("Objects shown only while selected (e.g. a filter chip's check mark).")]
        public GameObject[] showWhenSelected;
        [Tooltip("Objects hidden while selected.")]
        public GameObject[] hideWhenSelected;
        [Tooltip("Content that animates in while selected (AnimatedVisibility), e.g. a filter chip's check mark.")]
        public M3Reveal[] revealWhenSelected;

        [Header("Shape morph")]
        public Image[] shapeLayers;
        [Tooltip("Frames per layer, layer-major: shapeFrames[layer * frameCount + frame].")]
        public Sprite[] shapeFrames;
        public int frameCount;
        [Tooltip("Shape value (corner radius in dp) of frame 0 and of the last frame.")]
        public float shapeMin;
        public float shapeMax;
        public float restShape;
        public float pressedShape;
        public float selectedShape;
        public float selectedPressedShape;
        [Tooltip("Shape while hovered (list items morph on hover); NaN-free: set equal to restShape to disable.")]
        public float hoveredShape;
        public bool useHoveredShape;
        [Tooltip("Shape once the pointer has hovered and left again (menu groups: MenuGroupShapes.inactiveShape).")]
        public float inactiveShape;
        public bool useInactiveShape;
        public float shapeDamping = 1f;
        public float shapeStiffness = 1600f;

        [Header("Optical centering")]
        [Tooltip("Content shifted by horizontalCenterOptically: x = clamp(a + b * shapeValue, min, max).")]
        public RectTransform opticalContent;
        public float opticalA;
        public float opticalB;
        public float opticalMin;
        public float opticalMax;

        [Header("Ripple")]
        public Image ripple;
        [Tooltip("Palette index of the ripple color (unselected, selected).")]
        public int rippleColor = -1;
        public int rippleColorSelected = -1;

        [Header("Colors")]
        [Tooltip("Graphics recolored by state.")]
        public Graphic[] colored;
        [Tooltip("Palette indices, 4 per graphic: enabled, enabled+selected, disabled, disabled+selected. -1 = keep.")]
        public int[] colorIndices;
        [Tooltip("Per graphic: fade its color changes (animateColor) instead of switching.")]
        public bool[] colorAnimated;
        [Tooltip("Stiffness of the effects spring (damping 1) of the color fades; 0 = no fades.")]
        public float colorStiffness;

        [Header("Elevation")]
        public Image[] shadowLayers;
        [Tooltip("Shadow sprites [(layer * 4 + state) * frameCount + frame]; states: rest, hover, pressed, disabled. null = no shadow.")]
        public Sprite[] shadowFrames;
        [Tooltip("Shadow parameters [layer * 4 + state] = (blur sigma, spread, y offset, opacity) for the elevation tween.")]
        public Vector4[] shadowParams;
        int currentFrame;

        // shape spring state
        float shapeValue;
        float shapeVelocity;
        float shapeFrom;
        float shapeTarget;
        float shapeStart;     // time of the last retarget
        bool shapeAnimating;

        // ripple state: slot 0 is the newest ripple, slot 1 an older one still fading out
        float[] rStart;
        float[] rRelease;
        Vector2[] rOrigin;
        bool[] rActive;
        Material rippleMaterial;
        Material baseMaterial;

        // color fade state
        Color[] colorFrom;
        float colorT = 1f;
        float colorV;

        // elevation tween state
        int elevState = -1;
        Vector4[] elevFrom;
        Vector4[] elevNow;
        int elevTarget;
        float elevStart;
        float elevDuration;
        bool elevIncoming;
        bool elevAnimating;
        Material elevMaterial;
        Material baseShadowMaterial;

        bool pressed;
        bool hovered;
        bool hasBeenHovered;
        bool ticking;
        bool lastInteractable = true;

        void Start()
        {
            shapeValue = CurrentShapeTarget();
            shapeTarget = shapeValue;
            if (ripple != null) ripple.enabled = false;
            lastInteractable = IsInteractable();
            if (shadowLayers != null && shadowLayers.Length > 0 && shadowLayers[0] != null) baseShadowMaterial = shadowLayers[0].material;
            ApplyShape();
            ApplyColors(false);
            elevState = ElevationState();
            ApplyElevation();
        }

        bool IsInteractable()
        {
            return selectable == null || selectable.interactable;
        }

        float CurrentShapeTarget()
        {
            // ListItemShapes.shapeForInteraction order: pressed, selected, hovered, rest
            if (pressed) return selected ? selectedPressedShape : pressedShape;
            if (selected) return selectedShape;
            if (hovered && useHoveredShape) return hoveredShape;
            if (useInactiveShape && hasBeenHovered && !hovered) return inactiveShape;
            return restShape;
        }

        // ---- events from EventTrigger / Button ----

        public void _Down()
        {
            if (!IsInteractable()) return;
            pressed = true;
            Retarget();
            StartRipple();
            ApplyElevation();
            StartTicking();
        }

        public void _Up()
        {
            if (!pressed) return;
            pressed = false;
            Retarget();
            ReleaseRipple();
            ApplyElevation();
            StartTicking();
        }

        public void _Enter()
        {
            hovered = true;
            hasBeenHovered = true;
            if (useHoveredShape || useInactiveShape) { Retarget(); StartTicking(); }
            ApplyElevation();
        }

        public void _Exit()
        {
            hovered = false;
            if (pressed) _Up();
            if (useHoveredShape || useInactiveShape) { Retarget(); StartTicking(); }
            ApplyElevation();
        }

        /// <summary>Button.onClick: flips the selection of toggle components.</summary>
        [Tooltip("Radio behaviour: selecting this deselects these; clicking a selected item keeps it selected.")]
        public M3Interactive[] exclusiveWith;

        public void _Click()
        {
            if (!isToggle || !IsInteractable()) return;
            bool exclusive = exclusiveWith != null && exclusiveWith.Length > 0;
            if (exclusive && selected) return;
            selected = !selected;
            if (exclusive)
                for (int i = 0; i < exclusiveWith.Length; i++)
                    if (exclusiveWith[i] != null && exclusiveWith[i] != this) exclusiveWith[i]._Deselect();
            Retarget();
            ApplyColors(true);
            StartTicking();
            if (selectionListener != null) selectionListener.SendCustomEvent("_M3SelectionChanged");
        }

        /// <summary>Selects without notifying (for radio-like groups driven by code).</summary>
        public void _Select()
        {
            if (selected) return;
            selected = true;
            Retarget();
            ApplyColors(true);
            StartTicking();
        }

        public void _Deselect()
        {
            if (!selected) return;
            selected = false;
            Retarget();
            ApplyColors(true);
            StartTicking();
        }

        /// <summary>Call after changing selectable.interactable or selected from code.</summary>
        public void _M3Refresh()
        {
            lastInteractable = IsInteractable();
            if (!lastInteractable) pressed = false;
            Retarget();
            ApplyColors(true);
            ApplyElevation();
            StartTicking();
        }

        public void _Enable()
        {
            if (selectable != null) selectable.interactable = true;
            _M3Refresh();
        }

        public void _Disable()
        {
            if (selectable != null) selectable.interactable = false;
            _M3Refresh();
        }

        public void _M3ThemeChanged()
        {
            colorT = 1f;
            ApplyColors(false);
            UpdateRippleColor();
        }

        // ---- content rotation (SplitButtonSample: animateFloatAsState(if (checked) 180f else 0f)) ----

        [Header("Rotation when selected")]
        [Tooltip("Rotated by rotateSelected degrees while selected (e.g. the split button's trailing arrow).")]
        public RectTransform rotateTarget;
        public float rotateSelected = 180f;
        public float rotateStiffness = 1500f;   // the default spring(): StiffnessMedium, no bounce
        float rotation = -1f;
        float rotationV;
        float rotationTime;

        bool StepRotation()
        {
            float target = selected ? rotateSelected : 0f;
            float now = Time.time;
            if (rotation < 0f) { rotation = target; rotationTime = now; }
            float dt = Mathf.Min(now - rotationTime, 0.05f);
            rotationTime = now;
            float x0 = rotation - target;
            bool busy = Mathf.Abs(x0) > 0.01f || Mathf.Abs(rotationV) > 0.1f;
            if (busy)
            {
                float w = Mathf.Sqrt(rotateStiffness);
                float cb = rotationV + w * x0;
                float e = Mathf.Exp(-w * dt);
                rotation = target + (x0 + cb * dt) * e;
                rotationV = (cb - w * (x0 + cb * dt)) * e;
            }
            else { rotation = target; rotationV = 0f; }
            // graphicsLayer rotationZ is clockwise; Unity's z rotation is counter-clockwise
            rotateTarget.localEulerAngles = new Vector3(0f, 0f, -rotation);
            return busy;
        }

        // ---- animation loop ----

        void StartTicking()
        {
            if (ticking) return;
            ticking = true;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Tick()
        {
            bool busy = false;
            if (shapeAnimating && StepShape()) busy = true;
            if (rActive != null && (rActive[0] || rActive[1]) && StepRipple()) busy = true;
            if (colorT < 1f && StepColors()) busy = true;
            if (elevAnimating && StepElevation()) busy = true;
            if (rotateTarget != null && StepRotation()) busy = true;
            bool interactable = IsInteractable();
            if (interactable != lastInteractable) _M3Refresh();
            if (busy)
            {
                SendCustomEventDelayedFrames("_Tick", 1);
            }
            else
            {
                ticking = false;
            }
        }

        // ---- shape ----

        void Retarget()
        {
            float target = CurrentShapeTarget();
            if (Mathf.Approximately(target, shapeTarget) && shapeAnimating) return;
            if (Mathf.Approximately(target, shapeValue) && !shapeAnimating)
            {
                shapeTarget = target;
                return;
            }
            // continue from the current value and velocity (matches Compose's reversal behaviour)
            shapeFrom = shapeValue;
            shapeTarget = target;
            shapeStart = Time.time;
            shapeAnimating = true;
        }

        bool StepShape()
        {
            float t = Time.time - shapeStart;
            float x0 = shapeFrom - shapeTarget;
            float v0 = shapeVelocity;
            float w = Mathf.Sqrt(shapeStiffness);
            float z = shapeDamping;
            float x;
            float v;
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + v0) / wd;
                float e = Mathf.Exp(-z * w * t);
                float cs = Mathf.Cos(wd * t);
                float sn = Mathf.Sin(wd * t);
                x = e * (x0 * cs + sc * sn);
                v = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else if (z > 1f)
            {
                float s = w * Mathf.Sqrt(z * z - 1f);
                float gp = -z * w + s;
                float gm = -z * w - s;
                float cb = (gm * x0 - v0) / (gm - gp);
                float ca = x0 - cb;
                float ea = Mathf.Exp(gm * t);
                float eb = Mathf.Exp(gp * t);
                x = ca * ea + cb * eb;
                v = ca * gm * ea + cb * gp * eb;
            }
            else
            {
                float cb = v0 + w * x0;
                float e = Mathf.Exp(-w * t);
                x = (x0 + cb * t) * e;
                v = (cb - w * (x0 + cb * t)) * e;
            }
            shapeValue = shapeTarget + x;
            // keep the velocity of this segment so a retarget continues smoothly
            if (Mathf.Abs(x) < 0.01f && Mathf.Abs(v) < 0.1f)
            {
                shapeValue = shapeTarget;
                shapeVelocity = 0f;
                shapeAnimating = false;
                ApplyShape();
                return false;
            }
            shapeVelocity = v;
            // a retarget uses the instantaneous velocity at that moment
            shapeFrom = shapeValue;
            shapeStart = Time.time;
            ApplyShape();
            return true;
        }

        void ApplyShape()
        {
            if (shapeLayers == null || frameCount <= 0 || shapeFrames == null) return;
            float span = shapeMax - shapeMin;
            int frame = 0;
            if (span > 0.0001f) frame = Mathf.RoundToInt((shapeValue - shapeMin) / span * (frameCount - 1));
            frame = Mathf.Clamp(frame, 0, frameCount - 1);
            currentFrame = frame;
            if (opticalContent != null)
            {
                float ox = Mathf.Clamp(opticalA + opticalB * shapeValue, opticalMin, opticalMax);
                opticalContent.anchoredPosition = new Vector2(ox, opticalContent.anchoredPosition.y);
            }
            for (int i = 0; i < shapeLayers.Length; i++)
            {
                Image img = shapeLayers[i];
                if (img == null) continue;
                Sprite s = shapeFrames[i * frameCount + frame];
                if (img.sprite != s) img.sprite = s;
            }
            ApplyElevation();
        }

        // ---- ripple ----

        void EnsureRipple()
        {
            if (rStart != null) return;
            rStart = new float[2];
            rRelease = new float[2];
            rOrigin = new Vector2[2];
            rActive = new bool[2];
        }

        void StartRipple()
        {
            if (ripple == null) return;
            EnsureRipple();
            if (baseMaterial == null) baseMaterial = ripple.material;
            if (!rActive[0] && !rActive[1])
            {
                // a pooled material for this press (and an overlapping one)
                rippleMaterial = theme != null ? theme._NextRippleMaterial() : null;
                if (rippleMaterial != null) ripple.material = rippleMaterial;
            }
            else if (rActive[0])
            {
                // the previous ripple keeps animating in the second slot (already finished: it fades out)
                rStart[1] = rStart[0];
                rRelease[1] = rRelease[0] >= 0f ? rRelease[0] : Time.time;
                rOrigin[1] = rOrigin[0];
                rActive[1] = true;
            }
            rStart[0] = Time.time;
            rRelease[0] = -1f;
            rOrigin[0] = PressPoint();
            rActive[0] = true;
            UpdateRippleColor();
            ripple.enabled = true;
            StepRipple();
        }

        void ReleaseRipple()
        {
            if (rActive == null || !rActive[0]) return;
            if (rRelease[0] < 0f) rRelease[0] = Time.time;
        }

        /// <summary>The press position relative to the ripple's center (dp, y up); zero when unknown.</summary>
        Vector2 PressPoint()
        {
            RectTransform rt = ripple.rectTransform;
            Vector2 center = rt.rect.center;
#if UDONSHARP
            // VRChat sends UI events without a position: cast the pointer ray (head on desktop, hands in VR)
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null) return Vector2.zero;
            bool found;
            if (player.IsUserInVR())
            {
                found = RayPoint(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand));
                if (!found) found = RayPoint(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand));
            }
            else
            {
                found = RayPoint(rt, player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head));
            }
            return found ? rayHit - center : Vector2.zero;
#else
            if (M3PointerEvents.LastPressFrame != Time.frameCount) return Vector2.zero;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, M3PointerEvents.LastPressPosition, M3PointerEvents.LastPressCamera, out local))
                return Vector2.zero;
            return local - center;
#endif
        }

#if UDONSHARP
        Vector2 rayHit;

        bool RayPoint(RectTransform rt, VRCPlayerApi.TrackingData data)
        {
            Vector3 dir = data.rotation * Vector3.forward;
            Vector3 n = rt.forward;
            float denom = Vector3.Dot(dir, n);
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = Vector3.Dot(rt.position - data.position, n) / denom;
            if (t < 0f) return false;
            Vector3 local = rt.InverseTransformPoint(data.position + dir * t);
            rayHit = new Vector2(local.x, local.y);
            return rt.rect.Contains(rayHit);
        }
#endif

        void UpdateRippleColor()
        {
            if (ripple == null || theme == null) return;
            int idx = selected && rippleColorSelected >= 0 ? rippleColorSelected : rippleColor;
            if (idx < 0) return;
            Color c = theme.Get(idx);
            // a translucent ripple color (drawn over a background that varies) scales the ripple alpha
            rippleAlpha = c.a;
            c.a = ripple.color.a;
            ripple.color = c;
        }

        float rippleAlpha = 1f;

        /// <summary>RippleAnimation of one slot: x, y = center (dp from the shape center), z = radius progress, w = alpha.</summary>
        Vector4 RippleState(int i)
        {
            if (!rActive[i]) return Vector4.zero;
            float t = Time.time - rStart[i];
            // fadeIn() launches together: alpha 75 ms linear, radius 225 ms FastOutSlowIn, center 225 ms linear
            float radius = FastOutSlowIn(Mathf.Clamp01(t / 0.225f));
            Vector2 c = rOrigin[i] * (1f - Mathf.Clamp01(t / 0.225f));
            float alpha = Mathf.Clamp01(t / 0.075f);
            if (rRelease[i] >= 0f)
            {
                if (t < 0.225f)
                {
                    // finish() while still fading in: draw at full alpha
                    alpha = 1f;
                }
                else
                {
                    // fadeOut() (150 ms linear) starts once fadeIn() has completed and finish() was called
                    float outStart = Mathf.Max(rRelease[i], rStart[i] + 0.225f);
                    alpha = 1f - Mathf.Clamp01((Time.time - outStart) / 0.15f);
                    if (alpha <= 0f)
                    {
                        rActive[i] = false;
                        return Vector4.zero;
                    }
                }
            }
            return new Vector4(c.x, c.y, radius, alpha);
        }

        bool StepRipple()
        {
            Vector4 a = RippleState(0);
            Vector4 b = RippleState(1);
            Color col = ripple.color;
            if (rippleMaterial != null)
            {
                rippleMaterial.SetVector("_M3Dyn", a);
                rippleMaterial.SetVector("_M3Dyn2", b);
                Material m = ripple.materialForRendering;
                if (m != null && m != rippleMaterial)
                {
                    m.SetVector("_M3Dyn", a);
                    m.SetVector("_M3Dyn2", b);
                }
                col.a = rippleAlpha;
            }
            else
            {
                // no material pool (theme not re-applied): fade the static full-size ripple
                col.a = Mathf.Max(a.w, b.w) * rippleAlpha;
            }
            ripple.color = col;
            if (!rActive[0] && !rActive[1])
            {
                ripple.enabled = false;
                if (rippleMaterial != null && baseMaterial != null) ripple.material = baseMaterial;
                rippleMaterial = null;
                return false;
            }
            return true;
        }

        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float lo = 0f;
            float hi = 1f;
            float s = x;
            for (int i = 0; i < 24; i++)
            {
                s = (lo + hi) * 0.5f;
                float u = 1f - s;
                float bx = 3f * u * u * s * x1 + 3f * u * s * s * x2 + s * s * s;
                if (bx < x) lo = s; else hi = s;
            }
            float v = 1f - s;
            return 3f * v * v * s * y1 + 3f * v * s * s * y2 + s * s * s;
        }

        float FastOutSlowIn(float x)
        {
            return Bezier(0.4f, 0f, 0.2f, 1f, x);
        }

        // ---- colors & elevation ----

        void ApplyVisibility(bool animated)
        {
            if (revealWhenSelected != null)
            {
                for (int i = 0; i < revealWhenSelected.Length; i++)
                {
                    if (revealWhenSelected[i] == null) continue;
                    if (animated) revealWhenSelected[i].SetVisible(selected);
                    else revealWhenSelected[i].SnapVisible(selected);
                }
            }
            if (showWhenSelected != null)
            {
                for (int i = 0; i < showWhenSelected.Length; i++)
                {
                    if (showWhenSelected[i] != null) showWhenSelected[i].SetActive(selected);
                }
            }
            if (hideWhenSelected != null)
            {
                for (int i = 0; i < hideWhenSelected.Length; i++)
                {
                    if (hideWhenSelected[i] != null) hideWhenSelected[i].SetActive(!selected);
                }
            }
        }

        int ColorSlot()
        {
            return (IsInteractable() ? 0 : 2) + (selected ? 1 : 0);
        }

        void ApplyColors(bool animated)
        {
            ApplyVisibility(animated);
            if (theme == null || colored == null || colorIndices == null) return;
            int slot = ColorSlot();
            bool fade = animated && colorStiffness > 0f && colorAnimated != null && colorAnimated.Length == colored.Length;
            if (fade)
            {
                // animateColor: continue from the colors shown now
                if (colorFrom == null || colorFrom.Length != colored.Length) colorFrom = new Color[colored.Length];
                for (int i = 0; i < colored.Length; i++) if (colored[i] != null) colorFrom[i] = colored[i].color;
                colorT = 0f;
                colorV = 0f;
                lastColorTime = Time.time;
                StartTicking();
            }
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                if (fade && colorAnimated[i]) continue;
                int idx = colorIndices[i * 4 + slot];
                if (idx < 0) continue;
                g.color = theme.Get(idx);
            }
        }

        float lastColorTime;

        bool StepColors()
        {
            if (theme == null || colored == null || colorFrom == null || colorAnimated == null) { colorT = 1f; return false; }
            float now = Time.time;
            float dt = Mathf.Min(now - lastColorTime, 0.05f);
            lastColorTime = now;
            // effects springs are critically damped
            float w = Mathf.Sqrt(colorStiffness);
            float x0 = colorT - 1f;
            float cb = colorV + w * x0;
            float e = Mathf.Exp(-w * dt);
            float x = (x0 + cb * dt) * e;
            colorV = (cb - w * (x0 + cb * dt)) * e;
            colorT = 1f + x;
            bool busy = Mathf.Abs(x) > 0.002f || Mathf.Abs(colorV) > 0.01f;
            if (!busy) { colorT = 1f; colorV = 0f; }
            int slot = ColorSlot();
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null || !colorAnimated[i]) continue;
                int idx = colorIndices[i * 4 + slot];
                if (idx < 0) continue;
                g.color = theme.MixOklab(colorFrom[i], theme.Get(idx), colorT);
            }
            return busy;
        }

        int ElevationState()
        {
            if (!IsInteractable()) return 3;
            if (pressed) return 2;
            if (hovered) return 1;
            return 0;
        }

        void ApplyElevation()
        {
            if (shadowLayers == null || shadowFrames == null || shadowLayers.Length == 0) return;
            int frames = frameCount > 0 ? frameCount : 1;
            int state = ElevationState();
            int frame = Mathf.Clamp(currentFrame, 0, frames - 1);
            bool tween = shadowParams != null && shadowParams.Length >= shadowLayers.Length * 4 && theme != null;
            if (tween && elevState >= 0 && state != elevState)
            {
                if (state == 3 || elevState == 3)
                {
                    // enabled <-> disabled: snapTo
                    if (elevAnimating) EndElevation();
                }
                else
                {
                    if (elevFrom == null) { elevFrom = new Vector4[shadowLayers.Length]; elevNow = new Vector4[shadowLayers.Length]; }
                    for (int i = 0; i < shadowLayers.Length; i++)
                        elevFrom[i] = elevAnimating ? elevNow[i] : shadowParams[i * 4 + elevState];
                    // animateElevation: incoming spec of the new interaction, or outgoing spec of the one that ended
                    elevIncoming = state != 0;
                    elevDuration = elevIncoming ? 0.12f : (elevState == 1 ? 0.12f : 0.15f);
                    elevStart = Time.time;
                    elevTarget = state;
                    if (!elevAnimating)
                    {
                        elevMaterial = theme._NextRippleMaterial();
                        if (elevMaterial != null)
                            for (int i = 0; i < shadowLayers.Length; i++) if (shadowLayers[i] != null) shadowLayers[i].material = elevMaterial;
                    }
                    elevAnimating = elevMaterial != null;
                    if (elevAnimating) StartTicking();
                }
            }
            elevState = state;
            for (int i = 0; i < shadowLayers.Length; i++)
            {
                Image img = shadowLayers[i];
                if (img == null) continue;
                Sprite s = shadowFrames[(i * 4 + state) * frames + frame];
                // while tweening any shadow cell of this shape frame gives the geometry; _M3Dyn sets the rest
                if (elevAnimating) for (int k = 0; k < 4 && s == null; k++) s = shadowFrames[(i * 4 + k) * frames + frame];
                if (img.sprite != s) img.sprite = s;
                img.enabled = s != null;
            }
            if (elevAnimating) StepElevation();
        }

        bool StepElevation()
        {
            float t = Mathf.Clamp01((Time.time - elevStart) / elevDuration);
            // DefaultIncomingSpec: FastOutSlowIn; outgoing specs: CubicBezier(0.4, 0, 0.6, 1)
            float k = elevIncoming ? Bezier(0.4f, 0f, 0.2f, 1f, t) : Bezier(0.4f, 0f, 0.6f, 1f, t);
            for (int i = 0; i < shadowLayers.Length; i++)
                elevNow[i] = Vector4.Lerp(elevFrom[i], shadowParams[i * 4 + elevTarget], k);
            Vector4 key = elevNow[0];
            Vector4 ambient = shadowLayers.Length > 1 ? elevNow[1] : Vector4.zero;
            elevMaterial.SetVector("_M3Dyn", key);
            elevMaterial.SetVector("_M3Dyn2", ambient);
            for (int i = 0; i < shadowLayers.Length; i++)
            {
                if (shadowLayers[i] == null) continue;
                Material m = shadowLayers[i].materialForRendering;
                if (m != null && m != elevMaterial) { m.SetVector("_M3Dyn", key); m.SetVector("_M3Dyn2", ambient); }
            }
            if (t < 1f) return true;
            EndElevation();
            ApplyElevation();
            return false;
        }

        void EndElevation()
        {
            // back to the shared material and the static cells of the current state
            elevAnimating = false;
            for (int i = 0; i < shadowLayers.Length; i++)
                if (shadowLayers[i] != null && baseShadowMaterial != null) shadowLayers[i].material = baseShadowMaterial;
        }
    }
}
