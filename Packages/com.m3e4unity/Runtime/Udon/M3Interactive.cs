#if UDONSHARP
using UdonSharp;
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
    /// <item>the bounded ripple (RippleAnimation: 75 ms fade in, 225 ms FastOutSlowIn radius,
    /// 150 ms fade out);</item>
    /// <item>toggle selection, disabled colors and elevation per state.</item>
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
        public Sprite[] rippleFrames;
        [Tooltip("Palette index of the ripple color (unselected, selected).")]
        public int rippleColor = -1;
        public int rippleColorSelected = -1;

        [Header("Colors")]
        [Tooltip("Graphics recolored by state.")]
        public Graphic[] colored;
        [Tooltip("Palette indices, 4 per graphic: enabled, enabled+selected, disabled, disabled+selected. -1 = keep.")]
        public int[] colorIndices;

        [Header("Elevation")]
        public Image[] shadowLayers;
        [Tooltip("Shadow sprites [(layer * 4 + state) * frameCount + frame]; states: rest, hover, pressed, disabled. null = no shadow.")]
        public Sprite[] shadowFrames;
        int currentFrame;

        // shape spring state
        float shapeValue;
        float shapeVelocity;
        float shapeFrom;
        float shapeTarget;
        float shapeStart;     // time of the last retarget
        bool shapeAnimating;

        // ripple state
        float rippleStart;
        float rippleRelease = -1f;
        bool rippleActive;

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
            ApplyShape();
            ApplyColors();
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
            ApplyColors();
            StartTicking();
            if (selectionListener != null) selectionListener.SendCustomEvent("_M3SelectionChanged");
        }

        /// <summary>Selects without notifying (for radio-like groups driven by code).</summary>
        public void _Select()
        {
            if (selected) return;
            selected = true;
            Retarget();
            ApplyColors();
            StartTicking();
        }

        public void _Deselect()
        {
            if (!selected) return;
            selected = false;
            Retarget();
            ApplyColors();
            StartTicking();
        }

        /// <summary>Call after changing selectable.interactable or selected from code.</summary>
        public void _M3Refresh()
        {
            lastInteractable = IsInteractable();
            if (!lastInteractable) pressed = false;
            Retarget();
            ApplyColors();
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
            ApplyColors();
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
            if (rippleActive && StepRipple()) busy = true;
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

        void StartRipple()
        {
            if (ripple == null || rippleFrames == null || rippleFrames.Length == 0) return;
            rippleStart = Time.time;
            rippleRelease = -1f;
            rippleActive = true;
            UpdateRippleColor();
            ripple.enabled = true;
            StepRipple();
        }

        void ReleaseRipple()
        {
            if (!rippleActive) return;
            rippleRelease = Time.time;
        }

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

        bool StepRipple()
        {
            float t = Time.time - rippleStart;
            // radius: 225 ms FastOutSlowIn (cubic-bezier 0.4, 0, 0.2, 1)
            float radiusFraction = FastOutSlowIn(Mathf.Clamp01(t / 0.225f));
            int frame = Mathf.Clamp(Mathf.RoundToInt(radiusFraction * (rippleFrames.Length - 1)), 0, rippleFrames.Length - 1);
            if (ripple.sprite != rippleFrames[frame]) ripple.sprite = rippleFrames[frame];
            // alpha: 75 ms linear fade in; after release (and after the fade in), 150 ms linear fade out
            float alpha = Mathf.Clamp01(t / 0.075f);
            bool fadingOut = false;
            if (rippleRelease >= 0f)
            {
                float outStart = Mathf.Max(rippleRelease, rippleStart + 0.075f);
                float tOut = Time.time - outStart;
                if (tOut >= 0f)
                {
                    alpha = 1f - Mathf.Clamp01(tOut / 0.15f);
                    fadingOut = true;
                }
                else
                {
                    // released while fading in: Compose jumps to full alpha
                    alpha = 1f;
                }
            }
            Color c = ripple.color;
            c.a = alpha * rippleAlpha;
            ripple.color = c;
            if (fadingOut && alpha <= 0f)
            {
                ripple.enabled = false;
                rippleActive = false;
                return false;
            }
            return true;
        }

        float FastOutSlowIn(float x)
        {
            // cubic-bezier(0.4, 0, 0.2, 1): solve x(t) by Newton, return y(t)
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float u = 1f - t;
                float bx = 3f * u * u * t * 0.4f + 3f * u * t * t * 0.2f + t * t * t;
                float dx = 3f * u * u * 0.4f + 6f * u * t * (0.2f - 0.4f) + 3f * t * t * (1f - 0.2f);
                if (Mathf.Abs(dx) < 1e-5f) break;
                t = Mathf.Clamp01(t - (bx - x) / dx);
            }
            float uu = 1f - t;
            return 3f * uu * t * t + t * t * t;
        }

        // ---- colors & elevation ----

        void ApplyVisibility()
        {
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

        void ApplyColors()
        {
            ApplyVisibility();
            if (theme == null || colored == null || colorIndices == null) return;
            bool enabled = IsInteractable();
            int slot = (enabled ? 0 : 2) + (selected ? 1 : 0);
            for (int i = 0; i < colored.Length; i++)
            {
                Graphic g = colored[i];
                if (g == null) continue;
                int idx = colorIndices[i * 4 + slot];
                if (idx < 0) continue;
                g.color = theme.Get(idx);
            }
        }

        void ApplyElevation()
        {
            if (shadowLayers == null || shadowFrames == null || shadowLayers.Length == 0) return;
            int frames = frameCount > 0 ? frameCount : 1;
            int state = 0;
            if (!IsInteractable()) state = 3;
            else if (pressed) state = 2;
            else if (hovered) state = 1;
            int frame = Mathf.Clamp(currentFrame, 0, frames - 1);
            for (int i = 0; i < shadowLayers.Length; i++)
            {
                Image img = shadowLayers[i];
                if (img == null) continue;
                Sprite s = shadowFrames[(i * 4 + state) * frames + frame];
                if (img.sprite != s) img.sprite = s;
                img.enabled = s != null;
            }
        }
    }
}
