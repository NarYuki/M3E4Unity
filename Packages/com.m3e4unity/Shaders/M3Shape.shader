// M3E4Unity shape shader for uGUI Images.
//
// Every shape is an Image whose sprite is a 2x2-texel window into a "parameter
// atlas" (M3ShapeAtlas). The 4x4-texel cell around that window stores the
// shape parameters, so all shapes share one material and one texture and batch
// together. The shape is evaluated analytically per pixel (signed distance),
// which keeps edges crisp at any scale, in VR and in screen space.
//
// Cell layout (texel (i, j) of the cell, RGBA half floats):
//   P(0,0) = kind, radius TL, radius TR, radius BR      (radius < 0: full = half the short side)
//   P(1,0) = radius BL, stroke width, blur sigma, spread
//   P(2,0) = shape rect insets L, B, R, T (dp, inside the quad)
//   P(3,0), P(0,1), P(1,1), P(2,1), P(3,1) = kind specific
// The sprite itself covers texels (1..3, 1..3) of the cell, so the vertex shader
// recovers both the cell and an exact 0..1 local coordinate.
Shader "M3E4Unity/UI/Shape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Parameter Atlas", 2D) = "black" {}
        _ShapeTex ("Shape SDF Atlas", 2D) = "gray" {}
        _ShapeGrid ("Shape grid (tiles per row, tile px, texture px, sdf range)", Vector) = (32, 64, 2048, 0.125)
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "M3ShapeCommon.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 local    : TEXCOORD0;   // 0..1 across the quad, exact
                float4 objPos   : TEXCOORD1;   // canvas-space position (dp)
                half4  mask     : TEXCOORD2;
                float2 cell     : TEXCOORD3;   // cell origin in texels (constant over the quad)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _ShapeTex;
            float4 _ShapeGrid;
            fixed4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.objPos = v.vertex;
                OUT.vertex = vPosition;

                float2 t = v.texcoord * _MainTex_TexelSize.zw;
                float2 cell = floor(t * 0.25) * 4.0;
                OUT.cell = cell;
                OUT.local = (t - cell - 1.0) * 0.5;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float4 P(float2 cell, float i, float j)
            {
                return tex2Dlod(_MainTex, float4((cell + float2(i, j) + 0.5) * _MainTex_TexelSize.xy, 0, 0));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float4 p0 = P(IN.cell, 0, 0);
                float4 p1 = P(IN.cell, 1, 0);
                float4 p2 = P(IN.cell, 2, 0);
                float4 p3 = P(IN.cell, 3, 0);
                float4 p4 = P(IN.cell, 0, 1);
                float4 p5 = P(IN.cell, 1, 1);

                // Quad size in dp from the screen-space Jacobian of (position, local).
                float2 size = M3QuadSize(IN.objPos.xy, IN.local);
                float2 posInQuad = IN.local * size;           // dp, origin bottom-left
                M3_PIXEL = M3PixelSize(posInQuad);
                // Shape rect inside the quad (insets L, B, R, T).
                float2 rectMin = p2.xy;
                float2 rectSize = max(size - p2.xy - p2.zw, 1e-4);
                float2 p = posInQuad - rectMin - rectSize * 0.5; // dp, centered on the shape, y up

                float kind = round(p0.x);
                float4 radii = M3ResolveRadii(float4(p0.y, p0.z, p0.w, p1.x), rectSize);
                float coverage = 0;

                if (kind < 0.5)            // 0: filled rounded rectangle
                {
                    float d = M3SdRoundBox(p, rectSize * 0.5, radii);
                    coverage = M3Coverage(d);
                }
                else if (kind < 1.5)       // 1: stroke inside the rounded rectangle
                {
                    float d = M3SdRoundBox(p, rectSize * 0.5, radii);
                    float w = p1.y;
                    coverage = M3Coverage(abs(d + w * 0.5) - w * 0.5);
                }
                else if (kind < 2.5)       // 2: CSS-style blurred shadow of a rounded rectangle
                {
                    float spread = p1.w;
                    float4 r = radii + (radii > 0 ? spread : 0);
                    float d = M3SdRoundBox(p, rectSize * 0.5 + spread, r);
                    coverage = M3ShadowCoverage(d, p1.z);
                }
                else if (kind < 3.5)       // 3: MaterialShape / baked SDF tile, rotated
                {
                    // p3 = (tile, rotation degrees (clockwise), scale, unused)
                    float2 q = M3Rotate(IN.local - 0.5, p3.y) / max(p3.z, 1e-4) + 0.5;
                    float d = M3SampleShape(_ShapeTex, _ShapeGrid, p3.x, q) * min(rectSize.x, rectSize.y) / max(p3.z, 1e-4);
                    coverage = M3Coverage(d);
                }
                else if (kind < 4.5)       // 4: indeterminate LoadingIndicator, driven by time
                {
                    // p3 = (first tile, morph count, frames per morph, morph end time (s))
                    // p4 = (frame progress min, frame progress max, spring damping, spring stiffness)
                    float angle; float frame;
                    M3LoadingIndicatorFrame(_Time.y, p3, p4, angle, frame);
                    float2 q = M3Rotate(IN.local - 0.5, angle) + 0.5;
                    float f0 = floor(frame);
                    float d0 = M3SampleShape(_ShapeTex, _ShapeGrid, f0, q);
                    float d1 = M3SampleShape(_ShapeTex, _ShapeGrid, f0 + 1, q);
                    float d = lerp(d0, d1, frame - f0) * min(rectSize.x, rectSize.y);
                    coverage = M3Coverage(d);
                }
                else if (kind < 5.5)       // 5: ripple, a circle clipped to the container's rounded rectangle
                {
                    // p3 = (center x, center y (dp from the shape center), radius progress e, extra dp)
                    // RippleAnimation.kt: radius = lerp(0.3 * max(w, h), diagonal / 2 + 10 dp, e)
                    float dBox = M3SdRoundBox(p, rectSize * 0.5, radii);
                    float rStart = 0.3 * max(rectSize.x, rectSize.y);
                    float rEnd = length(rectSize) * 0.5 + p3.w;
                    float dCircle = length(p - p3.xy) - lerp(rStart, rEnd, p3.z);
                    coverage = M3Coverage(max(dBox, dCircle));
                }
                else if (kind < 6.5)       // 6: wavy / flat linear progress segment
                {
                    // p3 = (amplitude, wavelength, stroke width, phase speed dp/s)
                    // p4 = (phase offset dp, cap: 0 butt / 1 round, unused, unused)
                    float d = M3WavyLine(posInQuad - rectMin, rectSize, p3, p4, _Time.y);
                    coverage = M3Coverage(d);
                }
                else if (kind < 7.5)       // 7: wavy / flat circular arc
                {
                    // p3 = (amplitude, wave count, stroke width, rotation deg/s)
                    // p4 = (start angle deg (clockwise from 12 o'clock), sweep deg, wave phase deg/s, unused)
                    float d = M3WavyArc(p, rectSize, p3, p4, _Time.y);
                    coverage = M3Coverage(d);
                }
                else if (kind < 8.5)       // 8: checkbox check mark / dash, stroked path with progress
                {
                    // p3 = (check fraction 0..1, crossfade to dash 0..1, stroke width dp, unused)
                    float d = M3CheckMark(p, rectSize, p3);
                    coverage = M3Coverage(d);
                }
                else if (kind < 9.5)       // 9: filled ellipse/circle (radio dot, slider handle dot)
                {
                    float2 e = rectSize * 0.5;
                    float d = (length(p / e) - 1.0) * min(e.x, e.y);
                    coverage = M3Coverage(d);
                }

                else if (kind < 10.5)      // 10: linear progress indicator part (flat / wavy, determinate / indeterminate)
                {
                    float d = M3LinearProgress(posInQuad - rectMin, rectSize, p3, p4, p5, _Time.y);
                    coverage = M3Coverage(d);
                }
                else if (kind < 11.5)      // 11: circular progress indicator part
                {
                    float d = M3CircularProgress(p, rectSize, p3, p4, p5, _Time.y, _ShapeTex, _ShapeGrid);
                    coverage = M3Coverage(d);
                }

                half4 color = IN.color;
                color.a *= coverage;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
