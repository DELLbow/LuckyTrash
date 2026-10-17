// 背景（カジノのフロア）用の不透明シェーダー（URP）。Directional Light・環境光・影の影響は受けない。
// 色は「頂点カラー × _Color」に、背景用のレイヤーだけを照らすポイントライトの明るさを _LightResponse の割合で足したもの
// （光る部分のマテリアルは _LightResponse = 0）。テーブルの中心から遠いほど _FadeColor（カメラの背景色）に溶け込ませて、
// 部屋の端が暗闇に消えるようにする（フォグの代わり。RenderSettings のフォグは使わない）。
Shader "LuckyTrash/CasinoBackgroundUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _FadeColor ("Fade Color", Color) = (0.035,0.025,0.02,1)
        _FadeCenter ("Fade Center (XZ)", Vector) = (0,0,0.5,0)
        _FadeStart ("Fade Start", Float) = 20
        _FadeEnd ("Fade End", Float) = 45
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
                half4 _FadeColor;
                float4 _FadeCenter;
                float _FadeStart;
                float _FadeEnd;
                float _LightResponse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.positionWS = world;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 遠さは画素ごとに計算する（壁のように頂点が遠くにある大きな面でも、手前の部分は溶け込まない）。
                half3 col = i.color.rgb;
                if (_LightResponse > 0)
                {
                    col += i.color.rgb * CasinoRoomLights(i.positionWS, normalize(i.normalWS), i.positionCS) * _LightResponse;
                }
                float d = distance(i.positionWS.xz, _FadeCenter.xz);
                float fade = saturate((d - _FadeStart) / max(0.001, _FadeEnd - _FadeStart));
                return half4(lerp(col, _FadeColor.rgb, fade), 1);
            }
            ENDHLSL
        }
    }
}
