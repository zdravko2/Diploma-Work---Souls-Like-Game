#ifndef ADDITIONAL_LIGHT_INCLUDED
#define ADDITIONAL_LIGHT_INCLUDED

void MainLight_float(float3 WorldPos, 
                     out float3 Color, out float3 Direction,  out float Attenuation)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(1.0f, 1.0f, 0.0f));
    Color = 1.0f;
    Attenuation = 1.0f;
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color;
    Attenuation = mainLight.distanceAttenuation;
    Attenuation *= mainLight.shadowAttenuation;
#endif
}

void AllAdditionalLights_float(float3 WorldPos, float3 WorldNormal, 
                               out float3 LightColor, out float3 Direction, out float Attenuation)
{
    LightColor = 0.0f;
    Direction = 0.0f;
    Attenuation = 0.0f;

#ifndef SHADERGRAPH_PREVIEW
    int lightCount = GetAdditionalLightsCount();

    for (int i = 0; i < lightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPos, half4(1.0, 1.0, 1.0, 1.0));
                
        float distanceStrength = 0.30f;
        float3 dir = normalize(light.direction);
        
        float distanceAtt = saturate(pow(light.distanceAttenuation, distanceStrength));
        float shadowAtt = light.shadowAttenuation;
        
        float att = distanceAtt * shadowAtt;
        float NdotL = max(dot(WorldNormal, dir), 0.0f);
        float3 color = NdotL * light.color * att;

        LightColor += color;
        Direction += dir * NdotL;
        Attenuation += att * NdotL;
    }

    if (lightCount > 0)
    {
        Direction = normalize(Direction);
        Attenuation /= lightCount;
    }
#endif
}

//Half Rendering///////////////

void MainLight_half(half3 WorldPos, 
                    out half3 Color, out half3 Direction, out half Attenuation)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(half3(1.0f, 1.0f, 0.0f));
    Color = 1.0f;
    Attenuation = 1.0f;
#else
    Light mainLight = GetMainLight();
    Direction = mainLight.direction;
    Color = mainLight.color;
    Attenuation = mainLight.distanceAttenuation;
#endif
}

void AllAdditionalLights_half(half3 WorldPos, half3 WorldNormal, 
                              out half3 LightColor)
{
    LightColor = 0.0f;

#ifndef SHADERGRAPH_PREVIEW
    int lightCount = GetAdditionalLightsCount();

    for(int i = 0; i < lightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPos);
        
        float3 color = dot(light.direction, WorldNormal);
        color *= light.color;
        color *= light.distanceAttenuation;

        LightColor += color;
    } 
#endif
}

#endif


#ifndef ANIME_OUTLINE_INCLUDED
#define ANIME_OUTLINE_INCLUDED

// Declare the depth texture
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

void AnimeOutline_float(float4 ScreenPos, float4 BaseColor, float Thickness, float4 OutlineColor, float DepthSensitivity, float OutlineOpacity, float DepthCutoffDistance, 
                        out float4 Out)
{
    float2 screenUV = ScreenPos.xy / ScreenPos.w;
    float center = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
    float linearDepth = Linear01Depth(center, _ZBufferParams);
    float eyeDepth = linearDepth * _ProjectionParams.z;
    float fadeRange = DepthCutoffDistance * 0.25;
    float fadeStart = max(0, DepthCutoffDistance - fadeRange);
    
    if (eyeDepth > DepthCutoffDistance)
    {
        Out = BaseColor;
        return;
    }
    
    float2 offset = (Thickness + 0.49) * float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
    
    float topLeft = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(-offset.x, offset.y)).r;
    float top = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(0, offset.y)).r;
    float topRight = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(offset.x, offset.y)).r;
    float left = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(-offset.x, 0)).r;
    float right = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(offset.x, 0)).r;
    float bottomLeft = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(-offset.x, -offset.y)).r;
    float bottom = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(0, -offset.y)).r;
    float bottomRight = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV + float2(offset.x, -offset.y)).r;

    float sobelX = (topRight + 2.0 * right + bottomRight) - (topLeft + 2.0 * left + bottomLeft);
    float sobelY = (topLeft + 2.0 * top + topRight) - (bottomLeft + 2.0 * bottom + bottomRight);
    
    float edge = sqrt(sobelX * sobelX + sobelY * sobelY) * DepthSensitivity;
    float distanceFade = smoothstep(fadeStart, DepthCutoffDistance, eyeDepth);
    float distanceMask = 1.0 - distanceFade;
    float edgeStrength = smoothstep(0.01, 0.1, edge) * distanceMask;
    float finalOutlineStrength = edgeStrength * OutlineOpacity;
    
    Out = lerp(BaseColor, OutlineColor, finalOutlineStrength);
}

#endif
