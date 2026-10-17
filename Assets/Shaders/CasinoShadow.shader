// 背景の床に落とす、ぼかした影（テーブルの真下など、URP）。半透明の黒で、UV の中心から外側へなめらかに薄くなる。
// 濃さは頂点カラーのアルファ。ライト・環境光の影響は受けない。
Shader "LuckyTrash/CasinoShadow"
{
    Properties
    {
        _Softness ("Softness", Range(0.5,4)) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Shadow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
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
                float a = pow(1 - r, _Softness) * i.color.a;
                return half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
