Shader "Hidden/Universal Render Pipeline/Sky/HDRISky"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE

        // TODO What are these for?
        #pragma prefer_hlslcc gles
        #pragma exclude_renderers d3d11_9x
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SDF2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Runtime/Sky/SkyUtils.hlsl"

        TEXTURECUBE(_Cubemap);
        SAMPLER(sampler_Cubemap);

        float4 _SkyParam; // x exposure, y multiplier, zw rotation (cosPhi and sinPhi)
        #define _Intensity          _SkyParam.x
        #define _CosSinPhi          _SkyParam.zw
        float4 _BackplateParameters0; // xy: scale, z: groundLevel, w: projectionDistance
        #define _Scales             _BackplateParameters0.xy
        #define _ScaleX             _BackplateParameters0.x
        #define _ScaleY             _BackplateParameters0.y
        #define _GroundLevel        _BackplateParameters0.z
        #define _ProjectionDistance _BackplateParameters0.w
        float4 _BackplateParameters1; // x: BackplateType, y: BlendAmount, zw: backplate rotation (cosPhi_plate, sinPhi_plate)
        #define _BackplateType      _BackplateParameters1.x
        #define _BlendAmount        _BackplateParameters1.y
        #define _CosSinPhiPlate     _BackplateParameters1.zw
        float4 _BackplateParameters2; // xy: BackplateTextureRotation (cos/sin), zw: Backplate Texture Offset
        #define _CosSinPhiPlateTex  _BackplateParameters2.xy
        #define _OffsetTexX         _BackplateParameters2.z
        #define _OffsetTexY         _BackplateParameters2.w

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_Position;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;

            UNITY_SETUP_INSTANCE_ID(input); // TODO What is this?
            UNITY_TRANSFER_INSTANCE_ID(input, output); // TODO and this?

            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); // TODO this too?

            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);

            return output;
        }

        float3 RotationUp(float3 p, float2 cos_sin)
        {
            float3 rotDirX = float3(cos_sin.x, 0, -cos_sin.y);
            float3 rotDirY = float3(cos_sin.y, 0, cos_sin.x);

            return float3(dot(rotDirX, p), p.y, dot(rotDirY, p));
        }

        float3 GetPositionOnInfinitePlane(float3 dir)
        {
            const float alpha = (_GroundLevel - _WorldSpaceCameraPos.y) / dir.y;

            return _WorldSpaceCameraPos + alpha * dir;
        }

        float GetSDF(out float scale, float2 position)
        {
            position = RotationUp(float3(position.x, 0.0f, position.y), _CosSinPhiPlate).xz;
            if (_BackplateType == 0) // Circle
            {
                scale = _ScaleX;
                return CircleSDF(position, _ScaleX);
            }
            else if (_BackplateType == 1) // Rectangle
            {
                scale = min(_ScaleX, _ScaleY);
                return RectangleSDF(position, _Scales);
            }
            else if (_BackplateType == 2) // Ellipse
            {
                scale = min(_ScaleX, _ScaleY);
                return EllipseSDF(position, _Scales);
            }
            else //if (_BackplateType == 3) // Infinite backplate
            {
                scale = FLT_MAX;
                return CircleSDF(position, scale);
            }
        }

        void IsBackplateCommon(out float sdf, out float localScale, out float3 positionOnBackplatePlane, float3 dir)
        {
            positionOnBackplatePlane = GetPositionOnInfinitePlane(dir);

            sdf = GetSDF(localScale, positionOnBackplatePlane.xz);
        }

        bool IsHit(float sdf, float dirY)
        {
            return sdf < 0.0f && dirY < 0.0f && _WorldSpaceCameraPos.y > _GroundLevel;
        }

        bool IsBackplateHitWithBlend(out float3 positionOnBackplatePlane, out float blend, float3 dir)
        {
            float sdf;
            float localScale;
            IsBackplateCommon(sdf, localScale, positionOnBackplatePlane, dir);

            blend = smoothstep(0.0f, localScale * _BlendAmount, max(-sdf, 0));

            return IsHit(sdf, dir.y);
        }

        float3 GetSkyColor(float3 dir)
        {
            return SAMPLE_TEXTURECUBE_LOD(_Cubemap, sampler_Cubemap, dir, 0).rgb;
        }

        float4 GetColorWithRotation(float3 dir, float exposure, float2 cos_sin)
        {
            dir = RotationUp(dir, cos_sin);

            float3 skyColor = GetSkyColor(dir) * _Intensity * exposure;
            skyColor = ClampToFloat16Max(skyColor);

            return float4(skyColor, 1.0);
        }

        float4 RenderSky(Varyings input, float exposure)
        {
            float3 viewDirWS = -GetSkyViewDirWS(input.positionCS.xy);

            return GetColorWithRotation(viewDirWS, exposure, _CosSinPhi);
        }

        float4 RenderSkyWithBackplate(Varyings input, float3 positionOnBackplate, float exposure, float3 originalDir, float blend, float depth)
        {
            // Reverse it to point into the scene
            float3 offset = RotationUp(float3(_OffsetTexX, 0.0, _OffsetTexY), _CosSinPhiPlate);
            float3 dir = positionOnBackplate - float3(0.0, _ProjectionDistance + _GroundLevel, 0.0) + offset; // No need for normalization

            // TODO Shadows

            float3 output = lerp(
                GetColorWithRotation(originalDir, exposure, _CosSinPhi).rgb,
                GetColorWithRotation(RotationUp(dir, _CosSinPhiPlateTex), exposure, _CosSinPhi).rgb,
                blend);

            // TODO Ambient occlusion

            return float4(output, exposure);
        }

        struct SkyAndBackplate { float4 sky : SV_Target; float depth : SV_Depth; };
        SkyAndBackplate RenderBackplate(Varyings input, float exposure)
        {
            float3 viewDirWS = -GetSkyViewDirWS(input.positionCS.xy);

            float depth = UNITY_RAW_FAR_CLIP_VALUE;

            float3 finalPos;
            float blend;
            if (IsBackplateHitWithBlend(finalPos, blend, viewDirWS))
            {
                depth = ComputeNormalizedDeviceCoordinatesWithZ(finalPos, UNITY_MATRIX_VP).z;
            }

            SkyAndBackplate results;
            results.depth = depth;

            if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                results.sky = RenderSky(input, exposure);
            else
                results.sky = RenderSkyWithBackplate(input, finalPos, exposure, viewDirWS, blend, depth);

            return results;
        }

        float4 FragRender(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return RenderSky(input, 1.0); // TODO CurrentExposureMultiplier
        }

        SkyAndBackplate FragRenderBackplate(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return RenderBackplate(input, 1.0); // TODO CurrentExposureMultiplier
        }


        ENDHLSL

        Pass
        {
            Name "SkyRender"
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRender
            ENDHLSL
        }

        Pass
        {
            Name "SkyRenderWithBackplate"
            Cull Off
            ZTest LEqual
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRenderBackplate
            ENDHLSL
        }
    }
}
