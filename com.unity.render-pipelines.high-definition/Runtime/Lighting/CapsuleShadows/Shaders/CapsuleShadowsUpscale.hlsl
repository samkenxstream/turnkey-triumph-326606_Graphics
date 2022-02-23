#ifndef CAPSULE_SHADOWS_UPSCALE_DEF
#define CAPSULE_SHADOWS_UPSCALE_DEF

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/CapsuleShadows/Shaders/CapsuleShadowsGlobals.hlsl"

#define CAPSULE_UPSCALE_DEPTH_TOLERANCE_IN_PIXELS   8.f

// low weight, the second gather just fills in when the first gather has low depth weight
#define CAPSULE_UPSCALE_SECOND_GATHER_WEIGHT        0.01f

float MaxElement(float2 v)
{
    return max(v.x, v.y);
}
float SumElements(float4 v)
{
    return v.x + v.y + v.z + v.w;
}

float4 LinearEyeDepth(float4 deviceDepths, float4 zBufferParams)
{
    return float4(
        LinearEyeDepth(deviceDepths.x, zBufferParams),
        LinearEyeDepth(deviceDepths.y, zBufferParams),
        LinearEyeDepth(deviceDepths.z, zBufferParams),
        LinearEyeDepth(deviceDepths.w, zBufferParams));
}

float4 GetCapsuleBilinearWeights(float2 halfResPositionSS)
{
    float2 t = frac(halfResPositionSS + .5f);
    float2 s = 1.f - t;
    return float4(s.x, t.x, t.x, s.x)*float4(t.y, t.y, s.y, s.y);
}

struct CapsuleShadowDepthWeightParams
{
    float unitDepthInPixels;
    float unitDepthInTargetPixels;
};

CapsuleShadowDepthWeightParams GetCapsuleShadowsDepthWeightParams(float targetLinearDepth, float2 upscaledSizeRcp)
{
    // TODO: handle orthographic/oblique
    float2 viewFromClipScale = float2(UNITY_MATRIX_I_P._m00, UNITY_MATRIX_I_P._m11);
    float2 pixelHalfExtentXY = abs(viewFromClipScale*upscaledSizeRcp);

    CapsuleShadowDepthWeightParams params;
    params.unitDepthInPixels = .5f/MaxElement(pixelHalfExtentXY);
    params.unitDepthInTargetPixels = params.unitDepthInPixels/targetLinearDepth;
    return params;
}

float4 GetCapsuleShadowsDepthWeights(float2 depthUV, CapsuleShadowDepthWeightParams params)
{
    float4 gatherDeviceDepths = GATHER_RED_TEXTURE2D_X(_CameraDepthTexture, s_linear_clamp_sampler, depthUV);
    float4 gatherDepthsInTargetPixels = LinearEyeDepth(gatherDeviceDepths, _ZBufferParams)*params.unitDepthInTargetPixels;
    return 1.f/max(abs(gatherDepthsInTargetPixels - params.unitDepthInPixels) - CAPSULE_UPSCALE_DEPTH_TOLERANCE_IN_PIXELS, 1.f);
}

void GetCapsuleShadowsUpscaleWeights(
    float2 halfResPositionSS,
    float2 depthUVBase,
    float2 depthUVOffset,
    CapsuleShadowDepthWeightParams params,
    out float4 weights0,
    out float4 weights1,
    out float4 weights2,
    out float4 weights3,
    out float norm)
{
    /*
                w0.x   w0.y   w0.z   w0.w

        w1.x    g0.w | g0.z | g1.w | g1.z
                -----+------+------+-----
        w1.y    g0.x | g0.y | g1.x | g1.y
                -----+------+------+-----
        w1.z    g2.w | g2.z | g3.w | g3.z
                -----+------+------+-----
        w1.w    g2.x | g2.y | g3.x | g3.y
    */
    float2 t = frac(halfResPositionSS + .5f);
    float4 w0 = lerp(float4(1.f, 2.f, 1.f, 0.f), float4(0.f, 1.f, 2.f, 1.f), t.x);
    float4 w1 = lerp(float4(1.f, 2.f, 1.f, 0.f), float4(0.f, 1.f, 2.f, 1.f), t.y);
    float4 gw0 = w0.xyyx * w1.yyxx;
    float4 gw1 = w0.zwwz * w1.yyxx;
    float4 gw2 = w0.xyyx * w1.wwzz;
    float4 gw3 = w0.zwwz * w1.wwzz;

    float3 uvOffset = float3(depthUVOffset.x, depthUVOffset.y, 0.f);
    gw0 *= GetCapsuleShadowsDepthWeights(depthUVBase + uvOffset.zz, params);
    gw1 *= GetCapsuleShadowsDepthWeights(depthUVBase + uvOffset.xz, params);
    gw2 *= GetCapsuleShadowsDepthWeights(depthUVBase + uvOffset.zy, params);
    gw3 *= GetCapsuleShadowsDepthWeights(depthUVBase + uvOffset.xy, params);

    weights0 = gw0;
    weights1 = gw1;
    weights2 = gw2;
    weights3 = gw3;
    norm = 1.f/SumElements(float4(SumElements(gw0), SumElements(gw1), SumElements(gw2), SumElements(gw3)));
}


#endif // ndef CAPSULE_SHADOWS_UPSCALE_DEF
