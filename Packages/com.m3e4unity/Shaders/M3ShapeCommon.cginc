// Shared math for the M3E4Unity UI shaders. All lengths are in dp (canvas units).
#ifndef M3E4UNITY_SHAPE_COMMON
#define M3E4UNITY_SHAPE_COMMON

// Size of the quad in dp: the Jacobian of canvas position with respect to the
// exact 0..1 local coordinate. Works for any rotation and for perspective.
float2 M3QuadSize(float2 pos, float2 local)
{
    float2 dLx = ddx(local), dLy = ddy(local);
    float2 dPx = ddx(pos), dPy = ddy(pos);
    float det = dLx.x * dLy.y - dLy.x * dLx.y;
    if (abs(det) < 1e-12) return float2(1, 1);
    float2 dPdu = (dPx * dLy.y - dPy * dLx.y) / det;
    float2 dPdv = (dPy * dLx.x - dPx * dLy.x) / det;
    return float2(length(dPdu), length(dPdv));
}

// Size of one screen pixel in the same units as pos (for anti-aliasing).
float M3PixelSize(float2 pos)
{
    return max(length(ddx(pos)), length(ddy(pos)));
}

// Radii (TL, TR, BR, BL); negative = "full" (half the shorter side). Clamped like Compose.
float4 M3ResolveRadii(float4 r, float2 size)
{
    float full = min(size.x, size.y) * 0.5;
    r = r < 0 ? full : r;
    return min(r, full);
}

// Signed distance to a rounded box centred at the origin, y up. radii = (TL, TR, BR, BL).
float M3SdRoundBox(float2 p, float2 halfSize, float4 radii)
{
    float r = p.x > 0 ? (p.y > 0 ? radii.y : radii.z) : (p.y > 0 ? radii.x : radii.w);
    float2 q = abs(p) - halfSize + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

// Animation clock: _Time.y, or a fixed time for tests (Shader.SetGlobalFloat("_M3TimeOverrideOn", 1)).
float _M3TimeOverrideOn;
float _M3TimeOverride;
float M3Now() { return _M3TimeOverrideOn > 0.5 ? _M3TimeOverride : _Time.y; }

// Coverage of a signed distance in dp, anti-aliased over one screen pixel.
// pixel is set once per fragment by the caller (M3_PIXEL) so no derivative is taken in branches.
static float M3_PIXEL = 1.0;

float M3Coverage(float d)
{
    return saturate(0.5 - d / max(M3_PIXEL, 1e-5));
}

// erf approximation (Abramowitz-Stegun 7.1.26), max error 1.5e-7.
float M3Erf(float x)
{
    float s = sign(x);
    x = abs(x);
    float t = 1.0 / (1.0 + 0.3275911 * x);
    float y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * exp(-x * x);
    return s * y;
}

// Gaussian-blurred edge: coverage of a shape whose signed distance is d, blurred with sigma.
float M3ShadowCoverage(float d, float sigma)
{
    if (sigma < 1e-3) return M3Coverage(d);
    return 0.5 - 0.5 * M3Erf(d / (sigma * 1.41421356));
}

// Angle in [0, 360) for any input (HLSL fmod keeps the sign of negative values).
float M3Wrap360(float deg) { return deg - 360.0 * floor(deg / 360.0); }

// Sampling transform for a shape drawn rotated clockwise (on screen) by deg degrees.
float2 M3Rotate(float2 v, float deg)
{
    float a = radians(deg);
    float s = sin(a), c = cos(a);
    // y is up in UI space, so a clockwise screen rotation is a negative math angle;
    // sampling uses the inverse, a counter-clockwise rotation of the point.
    return float2(c * v.x - s * v.y, s * v.x + c * v.y);
}

// Baked SDF tile lookup. grid = (tiles per row, tile px, texture px, unused).
// q is in the unit square of the shape (y up). Returns the signed distance in shape units.
float M3SampleShape(sampler2D tex, float4 grid, float tile, float2 q)
{
    float tilesPerRow = grid.x, tilePx = grid.y, texPx = grid.z, range = grid.w;
    float2 outside = max(max(-q, q - 1.0), 0.0);
    float pad = 2.0;
    float col = fmod(tile, tilesPerRow);
    float row = floor(tile / tilesPerRow);
    float2 qc = saturate(q);
    float2 uv = (float2(col, row) * tilePx + pad + qc * (tilePx - 2.0 * pad)) / texPx;
    // RG half-float tiles: R = signed distance in shape units (negative inside)
    float d = tex2Dlod(tex, float4(uv, 0, 0)).r;
    return d + length(outside);
}

// Second channel of a path tile: arc-length parameter 0..1 of the closest point.
float M3SamplePathParam(sampler2D tex, float4 grid, float tile, float2 q)
{
    float tilesPerRow = grid.x, tilePx = grid.y, texPx = grid.z;
    float pad = 2.0;
    float col = fmod(tile, tilesPerRow);
    float row = floor(tile / tilesPerRow);
    float2 uv = (float2(col, row) * tilePx + pad + saturate(q) * (tilePx - 2.0 * pad)) / texPx;
    // G = arc-length parameter (0..1) of the closest point on the path
    return tex2Dlod(tex, float4(uv, 0, 0)).g;
}

// Unit-mass spring from 0 to 1 (Compose SpringSimulation), progress at t seconds.
float M3SpringProgress(float t, float damping, float stiffness)
{
    float w = sqrt(stiffness);
    if (damping < 1.0)
    {
        float wd = w * sqrt(1.0 - damping * damping);
        float e = exp(-damping * w * t);
        return 1.0 - e * (cos(wd * t) + (damping * w / wd) * sin(wd * t));
    }
    float e = exp(-w * t);
    return 1.0 - e * (1.0 + w * t);
}

// LoadingIndicator (indeterminate) timeline, from LoadingIndicator.kt:
// every 650 ms a morph springs 0 -> 1 (damping 0.6, stiffness 200, visibility 0.1) and,
// when it settles, the next morph starts at 0 and the rotation target advances 90 degrees.
// A global rotation adds 360 degrees every 4666 ms. Returns the clockwise angle and the
// fractional frame index into the baked morph frames.
// p3 = (first tile, morph count, frames per morph, morph end time (s))
// p4 = (frame progress min, frame progress max, spring damping, spring stiffness)
void M3LoadingIndicatorFrame(float time, float4 p3, float4 p4, out float angle, out float frame)
{
    const float interval = 0.65;
    float k = floor(time / interval);
    float tt = time - k * interval;
    float morphCount = p3.y;
    float morph = fmod(k, morphCount);
    float progress;
    float target = 90.0 * (fmod(k, 4.0) + 1.0);
    if (tt < p3.w)
    {
        progress = M3SpringProgress(tt, p4.z, p4.w);
    }
    else
    {
        progress = 0.0;
        morph = fmod(morph + 1.0, morphCount);
        target += 90.0;
    }
    float global = 360.0 * frac(time / 4.666);
    angle = progress * 90.0 + target + global;
    float framesPerMorph = p3.z;
    float f = saturate((progress - p4.x) / (p4.y - p4.x)) * (framesPerMorph - 1.0);
    frame = p3.x + morph * framesPerMorph + min(f, framesPerMorph - 1.001);
}

// Distance to the wavy line of LinearWavyProgressIndicator: consecutive quadratic arcs
// every half wavelength, peaking at amplitude, drawn with round (or butt) caps from x = 0
// to x = width of the shape rect. Returns a signed distance to the stroked line.
// p3 = (amplitude dp, wavelength dp, stroke width dp, phase speed dp/s)
// p4 = (phase offset dp, cap (0 butt, 1 round), unused, unused)
float M3WavyLine(float2 pos, float2 size, float4 p3, float4 p4, float time)
{
    float amp = p3.x, wl = max(p3.y, 1e-3), w = p3.z;
    float halfW = w * 0.5;
    float cap = p4.y > 0.5 ? halfW : 0.0;
    float y0 = size.y * 0.5;
    // the line runs from x = cap to x = size.x - cap so the round caps stay inside the rect
    float x0 = cap, x1 = max(size.x - cap, cap);
    float phase = p4.x + p3.w * time;
    float x = clamp(pos.x, x0, x1);
    // quadratic arc i covers [i * wl/2, (i + 1) * wl/2] in wave space; sign alternates
    float u = (x + phase) / (wl * 0.5);
    float i = floor(u);
    float t = u - i;
    // Compose builds the path y-down with the first arc bulging down (+y); UI space is y-up.
    float sgn = fmod(abs(i), 2.0) < 0.5 ? -1.0 : 1.0;
    // quadratic Bezier with control height 2*amp: y = 4 * amp * t * (1 - t)
    float fy = y0 + sgn * 4.0 * amp * t * (1.0 - t);
    float slope = sgn * 4.0 * amp * (1.0 - 2.0 * t) / (wl * 0.5);
    float dLine = abs(pos.y - fy) / sqrt(1.0 + slope * slope);
    // beyond the ends: distance to the end points (round cap) or flat cut (butt)
    float dx = pos.x < x0 ? x0 - pos.x : (pos.x > x1 ? pos.x - x1 : 0.0);
    float d = cap > 0 ? length(float2(dx, dLine)) : max(dLine, dx);
    return d - halfW;
}

// Distance to a circular arc stroke (flat CircularProgressIndicator and the track of the
// wavy one when amplitude is 0). Angles are clockwise from 12 o'clock, in degrees.
// p3 = (unused, unused, stroke width dp, rotation deg/s), p4 = (start deg, sweep deg, unused, round caps)
float M3WavyArc(float2 p, float2 size, float4 p3, float4 p4, float time)
{
    float w = p3.z;
    float radius = min(size.x, size.y) * 0.5 - w * 0.5;
    float start = p4.x + p3.w * time;
    float sweep = clamp(p4.y, 0.0, 360.0);
    // clockwise angle from 12 o'clock
    float ang = degrees(atan2(p.x, p.y));
    float rel = M3Wrap360(ang - start);
    float dRing = abs(length(p) - radius);
    float d;
    if (sweep >= 359.99 || rel <= sweep)
    {
        d = dRing;
    }
    else
    {
        // distance to the nearer end point (round cap)
        float a0 = radians(start), a1 = radians(start + sweep);
        float2 e0 = radius * float2(sin(a0), cos(a0));
        float2 e1 = radius * float2(sin(a1), cos(a1));
        d = min(length(p - e0), length(p - e1));
        if (p4.w < 0.5) d = max(d, w); // butt caps: nothing outside the sweep
    }
    return d - w * 0.5;
}

// Distance to a segment with square caps (Compose StrokeCap.Square), half width h.
float M3SquareCapSegment(float2 p, float2 a, float2 b, float h)
{
    float2 ab = b - a;
    float len = length(ab);
    if (len < 1e-5) return length(p - a) - h;
    float2 u = ab / len;
    float2 n = float2(-u.y, u.x);
    float s = dot(p - a, u);
    float t = dot(p - a, n);
    float along = max(-s - h, s - len - h);
    return max(abs(t) - h, along);
}

// Checkbox check mark (Checkbox.kt drawCheck, flag isCheckboxStylingFixEnabled = false):
// path (0.2, 0.5) -> (0.4, 0.7) -> (0.8, 0.3) of the box width, y down, drawn up to
// checkFraction of its length with square caps; crossCenterGravitation pulls the
// points toward a centred dash for the indeterminate state.
// p3 = (check fraction, gravitation, stroke width dp, unused)
float M3CheckMark(float2 p, float2 size, float4 p3)
{
    float frac = saturate(p3.x);
    float g = saturate(p3.y);
    float w = p3.z;
    float s = size.x;
    // box coordinates, y down, origin top-left
    float2 b = float2(p.x / s + 0.5, 0.5 - p.y / s) * s;
    float2 L = float2(0.2, lerp(0.5, 0.5, g)) * s;
    float2 C = float2(lerp(0.4, 0.5, g), lerp(0.7, 0.5, g)) * s;
    float2 R = float2(0.8, lerp(0.3, 0.5, g)) * s;
    float l1 = length(C - L), l2 = length(R - C);
    float total = (l1 + l2) * frac;
    if (total <= 1e-4) return 1e4;
    float h = w * 0.5;
    float d;
    if (total <= l1)
    {
        d = M3SquareCapSegment(b, L, L + (C - L) * (total / l1), h);
    }
    else
    {
        float d1 = M3SquareCapSegment(b, L, C, h);
        float d2 = M3SquareCapSegment(b, C, C + (R - C) * ((total - l1) / l2), h);
        d = min(d1, d2);
    }
    return d;
}

// ---------------------------------------------------------------------------
// Progress indicators (ProgressIndicator.kt, WavyProgressIndicator.kt)
// ---------------------------------------------------------------------------

// Cubic-bezier easing (MotionTokens): x(t) solved by Newton, returns y.
float M3CubicBezier(float x, float x1, float y1, float x2, float y2)
{
    x = saturate(x);
    float t = x;
    for (int i = 0; i < 6; i++)
    {
        float u = 1.0 - t;
        float bx = 3.0 * u * u * t * x1 + 3.0 * u * t * t * x2 + t * t * t;
        float dx = 3.0 * u * u * x1 + 6.0 * u * t * (x2 - x1) + 3.0 * t * t * (1.0 - x2);
        t = saturate(t - (bx - x) / max(dx, 1e-4));
    }
    float uu = 1.0 - t;
    return 3.0 * uu * uu * t * y1 + 3.0 * uu * t * t * y2 + t * t * t;
}

// Keyframe 0 -> 1 between start and start + duration (ms) inside a cycle, EmphasizedAccelerate.
float M3LinearKey(float ms, float start, float duration)
{
    // EasingEmphasizedAccelerateCubicBezier = (0.3, 0, 0.8, 0.15)
    return M3CubicBezier((ms - start) / duration, 0.3, 0.0, 0.8, 0.15);
}

// Distance from p to a horizontal round-capped segment [x0, x1] at height y0.
float M3RoundSegment(float2 p, float x0, float x1, float y0, float halfW)
{
    float x = clamp(p.x, x0, x1);
    return length(p - float2(x, y0)) - halfW;
}

// Distance to the wavy line between x0 and x1 (round caps). Wave as in M3WavyLine.
float M3WavySegment(float2 p, float x0, float x1, float y0, float amp, float wl, float phase, float halfW)
{
    if (amp <= 1e-3) return M3RoundSegment(p, x0, x1, y0, halfW);
    float x = clamp(p.x, x0, x1);
    float u = (x + phase) / (wl * 0.5);
    float i = floor(u);
    float t = u - i;
    float sgn = fmod(abs(i), 2.0) < 0.5 ? -1.0 : 1.0;
    float fy = y0 + sgn * 4.0 * amp * t * (1.0 - t);
    float slope = sgn * 4.0 * amp * (1.0 - 2.0 * t) / (wl * 0.5);
    float dLine = abs(p.y - fy) / sqrt(1.0 + slope * slope);
    float dx = p.x < x0 ? x0 - p.x : (p.x > x1 ? p.x - x1 : 0.0);
    return length(float2(dx, dLine)) - halfW;
}

// Linear progress indicator, one part per Image (0 = active indicator + stop, 1 = track).
// p3 = (progress, gap dp, stroke dp, part), p4 = (amplitude dp, wavelength dp, wave speed dp/s, indeterminate)
// p5 = (stop size dp, unused...)
float M3LinearProgress(float2 pos, float2 size, float4 p3, float4 p4, float4 p5, float time)
{
    float W = size.x, H = size.y;
    float w = p3.z, hw = w * 0.5;
    float y0 = H * 0.5;
    float part = p3.w;
    float amp = p4.x, wl = max(p4.y, 1e-3);
    float phase = p4.z * time;
    // adjustedGapSize = gap + stroke (round caps), as a fraction of the width
    float gapFrac = (p3.y + w) / W;
    float capOff = hw;
    float d = 1e4;
    if (p4.w < 0.5)
    {
        float progress = saturate(p3.x);
        if (part < 0.5)
        {
            if (progress > 0.0)
            {
                float x0 = clamp(0.0, capOff, W - capOff), x1 = clamp(progress * W, capOff, W - capOff);
                d = M3WavySegment(pos, x0, x1, y0, amp, wl, phase, hw);
            }
            // stop indicator: circle at the end (drawStopIndicator)
            float stop = min(p5.x, w);
            if (stop > 0.0)
            {
                float stopOffset = min((w - stop) * 0.5, 0.0);
                float cx = W - stop * 0.5 - stopOffset;
                d = min(d, length(pos - float2(cx, y0)) - stop * 0.5);
            }
        }
        else
        {
            float trackStart = progress + min(progress, gapFrac);
            if (trackStart < 1.0)
            {
                float x0 = clamp(trackStart * W, capOff, W - capOff), x1 = W - capOff;
                d = M3RoundSegment(pos, x0, x1, y0, hw);
            }
        }
        return d;
    }
    // indeterminate: two lines over a 1750 ms cycle
    float ms = fmod(time * 1000.0, 1750.0);
    float h1 = M3LinearKey(ms, 0.0, 1000.0), t1 = M3LinearKey(ms, 250.0, 1000.0);
    float h2 = M3LinearKey(ms, 650.0, 850.0), t2 = M3LinearKey(ms, 900.0, 850.0);
    if (part < 0.5)
    {
        if (h1 - t1 > 0.0) d = min(d, M3WavySegment(pos, clamp(t1 * W, capOff, W - capOff), clamp(h1 * W, capOff, W - capOff), y0, amp, wl, phase, hw));
        if (h2 - t2 > 0.0) d = min(d, M3WavySegment(pos, clamp(t2 * W, capOff, W - capOff), clamp(h2 * W, capOff, W - capOff), y0, amp, wl, phase, hw));
    }
    else
    {
        // track segments around the two lines (LinearProgressIndicator indeterminate drawing)
        if (h1 < 1.0 - gapFrac)
        {
            float s = h1 > 0.0 ? h1 + gapFrac : 0.0;
            d = min(d, M3RoundSegment(pos, clamp(s * W, capOff, W - capOff), W - capOff, y0, hw));
        }
        if (t1 > gapFrac)
        {
            float s = h2 > 0.0 ? h2 + gapFrac : 0.0;
            float e = t1 < 1.0 ? t1 - gapFrac : 1.0;
            if (e > s) d = min(d, M3RoundSegment(pos, clamp(s * W, capOff, W - capOff), clamp(e * W, capOff, W - capOff), y0, hw));
        }
        if (t2 > gapFrac)
        {
            float e = t2 < 1.0 ? t2 - gapFrac : 1.0;
            d = min(d, M3RoundSegment(pos, capOff, clamp(e * W, capOff, W - capOff), y0, hw));
        }
    }
    return d;
}

// Distance to an arc (round caps) on a circle of radius r; angles clockwise from 12 o'clock.
float M3ArcDistance(float2 p, float r, float start, float sweep, float hw)
{
    sweep = clamp(sweep, 0.0, 360.0);
    float ang = degrees(atan2(p.x, p.y));
    float rel = M3Wrap360(ang - start);
    if (sweep >= 359.99 || rel <= sweep) return abs(length(p) - r) - hw;
    float a0 = radians(start), a1 = radians(start + sweep);
    float2 e0 = r * float2(sin(a0), cos(a0));
    float2 e1 = r * float2(sin(a1), cos(a1));
    return min(length(p - e0), length(p - e1)) - hw;
}

float M3KeyHold(float ms, float t0, float t1, float v0, float v1)
{
    return lerp(v0, v1, saturate((ms - t0) / (t1 - t0)));
}

// Circular progress indicator part (0 active, 1 track). p is centred, y up.
// p3 = (progress, gap dp, stroke dp, part), p4 = (wave tile first, wave tile count, wave deg/s, indeterminate)
// p5 = (amplitude 0..1 for wavy, unused...). Wavy shapes come from path tiles (star outline frames).
float M3CircularProgress(float2 p, float2 size, float4 p3, float4 p4, float4 p5, float time, sampler2D tiles, float4 grid)
{
    float S = min(size.x, size.y);
    float w = p3.z, hw = w * 0.5;
    float r = (S - w) * 0.5;
    float part = p3.w;
    float sweep, start, rotation = 0.0;
    if (p4.w < 0.5)
    {
        sweep = saturate(p3.x) * 360.0;
        start = 0.0;
    }
    else
    {
        // 6000 ms cycle: global rotation 1080 deg linear; extra 90 deg steps (300 ms each, every 1500 ms);
        // sweep 0.1 -> 0.87 linearly over 3000 ms then back with EasingStandard (0.2, 0, 0, 1).
        float ms = fmod(time * 1000.0, 6000.0);
        float global = ms / 6000.0 * 1080.0;
        float extra = M3KeyHold(ms, 0, 300, 0, 90) + M3KeyHold(ms, 1500, 1800, 0, 90) + M3KeyHold(ms, 3000, 3300, 0, 90) + M3KeyHold(ms, 4500, 4800, 0, 90);
        float prog = ms < 3000.0 ? lerp(0.1, 0.87, ms / 3000.0) : lerp(0.87, 0.1, M3CubicBezier((ms - 3000.0) / 3000.0, 0.2, 0.0, 0.0, 1.0));
        // Compose rotate() is clockwise; startAngle 0 is 3 o'clock = 90 deg from 12 o'clock
        rotation = global + extra;
        start = 90.0 + rotation;
        sweep = prog * 360.0;
    }
    // gapSizeSweep = (gap + stroke) / (PI * diameter) * 360
    float gapSweep = (p3.y + w) / (3.14159265 * S) * 360.0;
    if (part < 0.5)
    {
        if (p4.y > 0.5 && p5.x > 0.001)
        {
            // wavy: path tile distance, clipped to the active angle range with round caps
            float frame = p4.x + saturate(p5.x) * (p4.y - 1.0);
            // The star path starts at a vertex at 3 o'clock; Compose draws it rotated to start at
            // 12 o'clock (toPath startAngle = 270), the indeterminate drawing is rotated by
            // rotation + 90 as a whole (so the waves turn with the arc), and the wave offset turns
            // the pattern back counter-clockwise by offset * 360 while the arc ends stay in place.
            float waveRot = p4.z * time;
            float patternRot = 270.0 + (p4.w < 0.5 ? 0.0 : 90.0 + rotation) - waveRot;
            float2 q = M3Rotate(p, patternRot) / (S - w) + 0.5;
            float f0 = floor(frame);
            float dPath = lerp(M3SampleShape(tiles, grid, f0, q), M3SampleShape(tiles, grid, min(f0 + 1.0, p4.x + p4.y - 1.0), q), frame - f0) * (S - w);
            float ang = degrees(atan2(p.x, p.y));
            float rel = M3Wrap360(ang - start);
            if (rel <= sweep) return abs(dPath) - hw;
            float outside = min(rel - sweep, 360.0 - rel);
            float along = radians(outside) * r;
            return length(float2(along, abs(dPath))) - hw;
        }
        return M3ArcDistance(p, r, start, max(sweep, 0.1), hw);
    }
    float tStart = start + sweep + min(sweep, gapSweep);
    float tSweep = 360.0 - sweep - min(sweep, gapSweep) * 2.0;
    if (tSweep <= 0.0) return 1e4;
    return M3ArcDistance(p, r, tStart, tSweep, hw);
}

#endif
