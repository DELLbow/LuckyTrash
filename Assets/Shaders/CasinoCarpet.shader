// 背景の床（深い赤のカーペット、URP）。Directional Light・環境光・影の影響は受けず、背景用のレイヤーだけを照らす
// ポイントライトの明るさを _LightResponse の割合で足す。
// ワールド座標の XZ から、細い金の線の格子をごく控えめに描き、テーブルから遠いほど _FadeColor に溶け込ませる。
Shader "LuckyTrash/CasinoCarpet"
{
    Properties
    {
        _Color ("Carpet Color", Color) = (0.19,0.022,0.032,1)
        _LineColor ("Line Color", Color) = (0.55,0.40,0.12,1)
        _LineStrength ("Line Strength", Range(0,1)) = 0.09
        _GridSize ("Grid Size", Float) = 3
        _LineWidth ("Line Width", Float) = 0.05
        _FadeColor ("Fade Color", Color) = (0.035,0.025,0.02,1)
        _FadeCenter ("Fade Center (XZ)", Vector) = (0,0,0.5,0)
        _FadeStart ("Fade Start", Float) = 11
        _FadeEnd ("Fade End", Float) = 40
        _LightResponse ("Room Light Response", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CasinoRoomLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _LineColor;
                float _LineStrength;
                float _GridSize;
                float _LineWidth;
                half4 _FadeColor;
                float4 _FadeCenter;
                float _FadeStart;
                float _FadeEnd;
                float _LightResponse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 xz : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.xz = world.xz;
                o.positionWS = world;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 格子の線：セルの端からの距離が線幅の半分以内なら線。遠くでちらつかないよう、画面上の変化量でぼかし、
                // 画面上で細かすぎる遠くの線は弱める。
                float2 cell = i.xz / _GridSize;
                float2 distToLine = abs(frac(cell - 0.5) - 0.5) * _GridSize;
                float2 blur = fwidth(i.xz) * 1.5;
                float2 lineMask = 1 - smoothstep(_LineWidth * 0.5, _LineWidth * 0.5 + blur, distToLine);
                float lineAmount = max(lineMask.x, lineMask.y) * _LineStrength;
                lineAmount *= saturate(1 - max(blur.x, blur.y) / (_GridSize * 0.25));
                half3 col = lerp(_Color.rgb, _LineColor.rgb, lineAmount);
                if (_LightResponse > 0)
                {
                    col += col * CasinoRoomLights(i.positionWS, half3(0, 1, 0), i.positionCS) * _LightResponse;
                }
                float d = distance(i.xz, _FadeCenter.xz);
                float fade = saturate((d - _FadeStart) / max(0.001, _FadeEnd - _FadeStart));
                return half4(lerp(col, _FadeColor.rgb, fade), 1);
            }
            ENDHLSL
        }
    }
}
