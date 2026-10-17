// 背景（カジノのフロア）の物だけを照らす、追加ライト（ポイントライト）の明るさを足し合わせる。
// Rendering Layers（Light Layers）で、背景用のレイヤーを照らすライトだけを拾う（テーブルのスポットライトは拾わない）。
// Forward（スマホ）と Forward+（PC）のどちらでも動くよう、URP のライトのループの書き方に合わせる。影は使わない。
#ifndef CASINO_ROOM_LIGHTING_INCLUDED
#define CASINO_ROOM_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half3 CasinoRoomLights(float3 positionWS, half3 normalWS, float4 positionCS)
{
    half3 sum = 0;
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            // 両面とも照らす（背景の板は裏表どちらからも見えるように描いているため）。
            sum += light.color * light.distanceAttenuation * abs(dot(normalWS, light.direction));
        }
    LIGHT_LOOP_END
#endif
    return sum;
}

#endif
