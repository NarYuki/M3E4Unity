#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;

namespace M3E4Unity.Udon
{
    /// <summary>
    /// ButtonGroup (ButtonGroup.kt): the pressed item grows by expandedRatio of its width (FastSpatial
    /// spring) and squeezes its neighbours, each by at most its compressionLimit. Items are laid out
    /// left to right with a fixed spacing. Up to 12 items; their EventTriggers call _DownN / _UpN.
    /// Optional single-selection (connected button groups used as segmented controls).
    /// </summary>
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M3ButtonGroup : UdonSharpBehaviour
#else
    public class M3ButtonGroup : M3BehaviourBase
#endif
    {
        public RectTransform[] items;
        public float[] baseWidths;
        public float[] compressionLimits;
        public float spacing = 12f;
        public float expandedRatio = 0.15f;
        public float springDamping = 0.6f;
        public float springStiffness = 800f;
        [Tooltip("Connected groups: selecting one item deselects the others (single select).")]
        public bool singleSelect;
        public M3Interactive[] toggles;

        float[] value;
        float[] velocity;
        bool[] pressed;
        bool[] waiting;
        float lastTime;
        bool ticking;

        void Start()
        {
            Init();
            Layout();
        }

        void Init()
        {
            if (items == null) return;
            if (value == null || value.Length != items.Length)
            {
                value = new float[items.Length];
                velocity = new float[items.Length];
                pressed = new bool[items.Length];
                waiting = new bool[items.Length];
            }
        }

        void Down(int i)
        {
            Init();
            if (i >= items.Length) return;
            pressed[i] = true;
            waiting[i] = false;
            Kick();
        }

        void Up(int i)
        {
            Init();
            if (i >= items.Length) return;
            pressed[i] = false;
            // collectLatest: waitUntil { value > 0.75 } before animating back to 0
            waiting[i] = true;
            if (singleSelect && toggles != null)
            {
                for (int k = 0; k < toggles.Length; k++)
                {
                    if (k != i && toggles[k] != null) toggles[k]._Deselect();
                }
            }
            Kick();
        }

        public void _Down0() { Down(0); } public void _Up0() { Up(0); }
        public void _Down1() { Down(1); } public void _Up1() { Up(1); }
        public void _Down2() { Down(2); } public void _Up2() { Up(2); }
        public void _Down3() { Down(3); } public void _Up3() { Up(3); }
        public void _Down4() { Down(4); } public void _Up4() { Up(4); }
        public void _Down5() { Down(5); } public void _Up5() { Up(5); }
        public void _Down6() { Down(6); } public void _Up6() { Up(6); }
        public void _Down7() { Down(7); } public void _Up7() { Up(7); }
        public void _Down8() { Down(8); } public void _Up8() { Up(8); }
        public void _Down9() { Down(9); } public void _Up9() { Up(9); }
        public void _Down10() { Down(10); } public void _Up10() { Up(10); }
        public void _Down11() { Down(11); } public void _Up11() { Up(11); }

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
            for (int i = 0; i < items.Length; i++)
            {
                float target = pressed[i] ? 1f : 0f;
                if (!pressed[i] && waiting[i])
                {
                    if (value[i] > 0.75f) waiting[i] = false;
                    else target = 1f;
                }
                if (Step(i, target, dt)) busy = true;
                if (waiting[i]) busy = true;
            }
            Layout();
            if (busy) SendCustomEventDelayedFrames("_Tick", 1);
            else ticking = false;
        }

        bool Step(int i, float target, float dt)
        {
            float x0 = value[i] - target;
            float v0 = velocity[i];
            if (Mathf.Abs(x0) < 0.001f && Mathf.Abs(v0) < 0.01f)
            {
                value[i] = target;
                velocity[i] = 0f;
                return false;
            }
            float w = Mathf.Sqrt(springStiffness);
            float z = springDamping;
            float x;
            float v;
            if (z < 1f)
            {
                float wd = w * Mathf.Sqrt(1f - z * z);
                float sc = (z * w * x0 + v0) / wd;
                float e = Mathf.Exp(-z * w * dt);
                float cs = Mathf.Cos(wd * dt);
                float sn = Mathf.Sin(wd * dt);
                x = e * (x0 * cs + sc * sn);
                v = -z * w * x + e * (-x0 * wd * sn + sc * wd * cs);
            }
            else
            {
                float cb = v0 + w * x0;
                float e = Mathf.Exp(-w * dt);
                x = (x0 + cb * dt) * e;
                v = (cb - w * (x0 + cb * dt)) * e;
            }
            value[i] = target + x;
            velocity[i] = v;
            return true;
        }

        public void Layout()
        {
            if (items == null || baseWidths == null) return;
            Init();
            int n = items.Length;
            float[] widths = new float[n];
            for (int i = 0; i < n; i++) widths[i] = baseWidths[i];
            if (n > 1)
            {
                for (int i = 0; i < n; i++)
                {
                    if (value[i] == 0f) continue;
                    float growth;
                    if (i > 0 && i < n - 1)
                    {
                        float g = Mathf.Round(value[i] * Mathf.Min(Mathf.Min(expandedRatio * widths[i] / 2f, compressionLimits[i - 1]), compressionLimits[i + 1]));
                        float gl = Mathf.Min(g, widths[i - 1]);
                        float gr = Mathf.Min(g, widths[i + 1]);
                        widths[i - 1] -= gl;
                        widths[i + 1] -= gr;
                        growth = gl + gr;
                    }
                    else if (i == 0)
                    {
                        float g = Mathf.Round(value[i] * Mathf.Min(expandedRatio * widths[i], compressionLimits[i + 1]));
                        float gr = Mathf.Min(g, widths[i + 1]);
                        widths[i + 1] -= gr;
                        growth = gr;
                    }
                    else
                    {
                        float g = Mathf.Round(value[i] * Mathf.Min(expandedRatio * widths[i], compressionLimits[i - 1]));
                        float gl = Mathf.Min(g, widths[i - 1]);
                        widths[i - 1] -= gl;
                        growth = gl;
                    }
                    widths[i] += growth;
                }
            }
            float x = 0f;
            for (int i = 0; i < n; i++)
            {
                RectTransform rt = items[i];
                if (rt == null) continue;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(widths[i], rt.sizeDelta.y);
                x += widths[i] + spacing;
            }
        }
    }
}
