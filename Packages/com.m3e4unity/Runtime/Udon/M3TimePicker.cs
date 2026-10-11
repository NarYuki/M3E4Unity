#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
#endif
using TMPro;
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// Time picker with a clock dial (TimePicker.kt AnalogTimePickerState / ClockFace / drawSelector,
    /// 12-hour). The dial takes the pointer like ClockDialModifier: a tap rotates the selector to the
    /// nearest hour / 5 minutes with DefaultSpatial (onTap) and, on the hour face, switches to the
    /// minutes after 100 ms; a drag makes the selector follow the pointer (rotateTo without animation,
    /// any minute) and on release switches to the minutes and settles with DefaultSpatial
    /// (onGestureEnd). The selection chips animate the selector with DefaultSpatial (animateToCurrent);
    /// the hour and minute faces cross-fade (DefaultEffects). The numbers inside the selector are a
    /// masked OnPrimary copy of the face (the XOR blend of drawSelector).
    /// Events: _DialDown / _DialUp (from the dial), _SelHour, _SelMinute, _AM, _PM; _H0 … _H11 and
    /// _M0 … _M11 select a value without animation (onClockTextClick, SnapSpec).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3TimePicker : UdonSharpBehaviour
#else
    public class M3TimePicker : M3BehaviourBase
#endif
    {
        public int hour = 9;       // 0..23
        public int minute;         // 0..59
        public bool minuteSelected;
#if UDONSHARP
        public UdonSharpBehaviour changeListener;
#else
        public M3BehaviourBase changeListener;
#endif

        [Header("Display")]
        public TextMeshProUGUI hourText;
        public TextMeshProUGUI minuteText;
        public M3Interactive hourSelector;
        public M3Interactive minuteSelector;
        public M3Interactive amToggle;
        public M3Interactive pmToggle;

        [Header("Dial")]
        [Tooltip("The clock face (its center is the dial center); sends _DialDown / _DialUp.")]
        public RectTransform dial;
        public float dialSize = 256f;
        public float outerRadiusRatio = 101f / 256f;     // OuterCircleToSizeRatio
        public float handleSize = 48f;                   // ClockDialSelectorHandleContainerSize
        public RectTransform handle;                     // the selector circle (a Mask)
        public RectTransform handleContent;              // child of the handle, counter-moved to stay aligned with the dial
        public RectTransform line;                       // the selector track (pivot at the dial center)
        public CanvasGroup hourFace;
        public CanvasGroup minuteFace;
        public CanvasGroup hourFaceSelected;
        public CanvasGroup minuteFaceSelected;

        [Header("Motion")]
        public float spatialDamping = 0.8f;
        public float spatialStiffness = 380f;
        public float effectsStiffness = 1600f;

        float angle;
        float angleV;
        float targetAngle;
        float face;          // 0 = hours, 1 = minutes
        float faceV;
        float lastTime;
        bool ticking;
        bool initialized;
        const float TwoPi = 6.2831853f;

        // dial gesture
        bool dialPressed;
        bool dragging;
        Vector2 pressLocal;
        Vector2 pointerLocal;
        int raySource;      // VRChat: 0 head, 1 right hand, 2 left hand
        const float TouchSlop = 8f;

        void Start()
        {
            Init();
        }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            targetAngle = CurrentAngle();
            angle = targetAngle;
            face = minuteSelected ? 1f : 0f;
            bool pm = hour >= 12;
            if (amToggle != null) { if (pm) amToggle._Deselect(); else amToggle._Select(); }
            if (pmToggle != null) { if (pm) pmToggle._Select(); else pmToggle._Deselect(); }
            Apply();
        }

        public void _M3Refresh() { initialized = false; Init(); }

        // hourAngle = RadiansPerHour * (hour % 12) - FullCircle / 4, minuteAngle = RadiansPerMinute * minute - FullCircle / 4
        float CurrentAngle()
        {
            return minuteSelected ? TwoPi / 60f * minute - TwoPi / 4f : TwoPi / 12f * (hour % 12) - TwoPi / 4f;
        }

        // endValueForAnimation: the shortest way round
        float EndValue(float target)
        {
            float diff = angle - target;
            while (diff > TwoPi / 2f) diff -= TwoPi;
            while (diff <= -TwoPi / 2f) diff += TwoPi;
            return angle - diff;
        }

        void Kick()
        {
            if (ticking) return;
            ticking = true;
            lastTime = Time.time;
            SendCustomEventDelayedFrames("_Tick", 1);
        }

        public void _Tick()
        {
            float now = Time.time;
            float dt = Mathf.Min(now - lastTime, 0.05f);
            lastTime = now;
            bool busy = false;
            if (dialPressed) { PollDial(); busy = true; }
            // DefaultSpatial (under-damped) for the angle
            float w = Mathf.Sqrt(spatialStiffness);
            float z = spatialDamping;
            float x0 = angle - targetAngle;
            if (Mathf.Abs(x0) > 0.0005f || Mathf.Abs(angleV) > 0.005f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + angleV) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float x = e * (x0 * Mathf.Cos(wd * dt) + sc * Mathf.Sin(wd * dt));
                angleV = -z * w * x + e * (-x0 * wd * Mathf.Sin(wd * dt) + sc * wd * Mathf.Cos(wd * dt));
                angle = targetAngle + x;
                busy = true;
            }
            else { angle = targetAngle; angleV = 0f; }
            // DefaultEffects (critically damped) for the cross-fade
            float ft = minuteSelected ? 1f : 0f;
            float we = Mathf.Sqrt(effectsStiffness);
            float f0 = face - ft;
            if (Mathf.Abs(f0) > 0.002f || Mathf.Abs(faceV) > 0.01f)
            {
                float cb = faceV + we * f0;
                float e2 = Mathf.Exp(-we * dt);
                float fx = (f0 + cb * dt) * e2;
                faceV = (cb - we * (f0 + cb * dt)) * e2;
                face = ft + fx;
                busy = true;
            }
            else { face = ft; faceV = 0f; }
            Apply();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        void Apply()
        {
            int h12 = hour % 12 == 0 ? 12 : hour % 12; // hourForDisplay
            if (hourText != null) hourText.text = h12 < 10 ? "0" + h12 : h12.ToString();
            if (minuteText != null) minuteText.text = minute < 10 ? "0" + minute : minute.ToString();
            // selectorPos: length = OuterCircleToSizeRatio * diameter
            float length = dialSize * outerRadiusRatio;
            float cx = Mathf.Cos(angle) * length;
            float cy = -Mathf.Sin(angle) * length; // the dial y axis points down in Compose
            if (handle != null) handle.anchoredPosition = new Vector2(cx, cy);
            if (handleContent != null) handleContent.anchoredPosition = new Vector2(-cx, -cy);
            if (line != null)
            {
                // from the center to the selector edge
                line.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                line.sizeDelta = new Vector2(Mathf.Max(0f, length - handleSize / 2f), line.sizeDelta.y);
            }
            float f = Mathf.Clamp01(face);
            if (hourFace != null) { hourFace.alpha = 1f - f; hourFace.blocksRaycasts = !minuteSelected; hourFace.interactable = !minuteSelected; }
            if (minuteFace != null) { minuteFace.alpha = f; minuteFace.blocksRaycasts = minuteSelected; minuteFace.interactable = minuteSelected; }
            if (hourFaceSelected != null) hourFaceSelected.alpha = 1f - f;
            if (minuteFaceSelected != null) minuteFaceSelected.alpha = f;
        }

        void SetSelection(bool minutes)
        {
            Init();
            if (minuteSelected == minutes) return;
            minuteSelected = minutes;
            if (hourSelector != null) { if (minutes) hourSelector._Deselect(); else hourSelector._Select(); }
            if (minuteSelector != null) { if (minutes) minuteSelector._Select(); else minuteSelector._Deselect(); }
            // animateToCurrent(DefaultSpatial)
            targetAngle = EndValue(CurrentAngle());
            Kick();
        }

        public void _SelHour() { SetSelection(false); }
        public void _SelMinute() { SetSelection(true); }
        public void _AutoSwitch() { SetSelection(true); }

        // ---- dial pointer (ClockDialModifier) ----

        public void _DialDown()
        {
            Init();
            if (!PointerOnDial(true)) return;
            dialPressed = true;
            dragging = false;
            pressLocal = pointerLocal;
            Kick();
        }

        public void _DialUp()
        {
            if (!dialPressed) return;
            PollDial();
            dialPressed = false;
            if (dragging)
            {
                // onDragEnd: switch to the minutes, then onGestureEnd animates to the selected value
                dragging = false;
                if (!minuteSelected) SetSelection(true);
                targetAngle = EndValue(CurrentAngle());
                Kick();
                return;
            }
            Tap(pressLocal);
        }

        void PollDial()
        {
            if (!PointerOnDial(false)) return;
            if (!dragging && (pointerLocal - pressLocal).magnitude > TouchSlop) dragging = true;
            if (!dragging) return;
            // rotateTo(atan(...), animate = false): the selector follows the pointer exactly
            float a = Mathf.Atan2(-pointerLocal.y, pointerLocal.x);
            targetAngle = EndValue(a);
            angle = targetAngle;
            angleV = 0f;
            float fromTop = a + TwoPi / 4f;
            bool changed;
            if (minuteSelected)
            {
                int m = ((Mathf.RoundToInt(fromTop / (TwoPi / 60f)) % 60) + 60) % 60;
                changed = m != minute;
                minute = m;
            }
            else
            {
                int h = ((Mathf.RoundToInt(fromTop / (TwoPi / 12f)) % 12) + 12) % 12;
                int nh = h + (hour >= 12 ? 12 : 0);
                changed = nh != hour;
                hour = nh;
            }
            Apply();
            if (changed && changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        void Tap(Vector2 p)
        {
            // onTap: the nearest hour, or the nearest 5 minutes, animated with DefaultSpatial
            float fromTop = Mathf.Atan2(-p.y, p.x) + TwoPi / 4f;
            int step = ((Mathf.RoundToInt(fromTop / (TwoPi / 12f)) % 12) + 12) % 12;
            if (minuteSelected) minute = step * 5;
            else
            {
                hour = step + (hour >= 12 ? 12 : 0);
                SendCustomEventDelayedSeconds("_AutoSwitch", 0.1f);
            }
            targetAngle = EndValue(CurrentAngle());
            Apply();
            Kick();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        /// <summary>The pointer position relative to the dial center (dp, y up), into pointerLocal.</summary>
        bool PointerOnDial(bool press)
        {
            RectTransform rt = dial;
            if (rt == null) return false;
#if UDONSHARP
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null) return false;
            if (press)
            {
                // the pointer that pressed: a hand ray in VR, the head ray on desktop
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
            Vector2 screen;
            if (press && M3PointerEvents.LastPressFrame == Time.frameCount) screen = M3PointerEvents.LastPressPosition;
            else if (Input.touchCount > 0) screen = Input.GetTouch(0).position;
            else screen = Input.mousePosition;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, M3PointerEvents.LastPressCamera, out local)) return false;
            pointerLocal = local - rt.rect.center;
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
            Vector2 p = new Vector2(local.x, local.y);
            if (mustHit && !rt.rect.Contains(p)) return false;
            pointerLocal = p - rt.rect.center;
            return true;
        }
#endif

        void PickHour(int value12)
        {
            Init();
            bool pm = hour >= 12;
            hour = value12 % 12 + (pm ? 12 : 0);
            // onClockTextClick: rotateTo with SnapSpec
            targetAngle = EndValue(CurrentAngle());
            angle = targetAngle; angleV = 0f;
            Apply();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
            SendCustomEventDelayedSeconds("_AutoSwitch", 0.1f);
        }

        void PickMinute(int value)
        {
            Init();
            minute = value;
            targetAngle = EndValue(CurrentAngle());
            angle = targetAngle; angleV = 0f;
            Apply();
            if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged");
        }

        public void _AM()
        {
            if (hour >= 12) { hour -= 12; Apply(); if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged"); }
            if (amToggle != null) amToggle._Select();
            if (pmToggle != null) pmToggle._Deselect();
        }

        public void _PM()
        {
            if (hour < 12) { hour += 12; Apply(); if (changeListener != null) changeListener.SendCustomEvent("_M3TimeChanged"); }
            if (pmToggle != null) pmToggle._Select();
            if (amToggle != null) amToggle._Deselect();
        }

        public void _H0() { PickHour(12); } public void _H1() { PickHour(1); } public void _H2() { PickHour(2); } public void _H3() { PickHour(3); }
        public void _H4() { PickHour(4); } public void _H5() { PickHour(5); } public void _H6() { PickHour(6); } public void _H7() { PickHour(7); }
        public void _H8() { PickHour(8); } public void _H9() { PickHour(9); } public void _H10() { PickHour(10); } public void _H11() { PickHour(11); }
        public void _M0() { PickMinute(0); } public void _M1() { PickMinute(5); } public void _M2() { PickMinute(10); } public void _M3() { PickMinute(15); }
        public void _M4() { PickMinute(20); } public void _M5() { PickMinute(25); } public void _M6() { PickMinute(30); } public void _M7() { PickMinute(35); }
        public void _M8() { PickMinute(40); } public void _M9() { PickMinute(45); } public void _M10() { PickMinute(50); } public void _M11() { PickMinute(55); }
    }
}
