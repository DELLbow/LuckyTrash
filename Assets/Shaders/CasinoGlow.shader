// 背景の光（スロットマシンのランプのにじみ、別のテーブルを照らす明るい楕円、URP）。加算合成で、
// UV の中心から外側へなめらかに消える。色は頂点カラー（スクリプトから明るさを動かす）。
Shader "LuckyTrash/CasinoGlow"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1
        _Softness ("Softness", Range(0.5,4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float r = saturate(length(i.uv - 0.5) * 2);
                float a = pow(1 - r, _Softness) * i.color.a * _Intensity;
                return half4(i.color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
