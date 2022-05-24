using System;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.Rendering.HighDefinition
{
    //Data of VisibleLight that has been processed / evaluated.
    internal struct HDProcessedVisibleLight
    {
        public int dataIndex;
        public GPULightType gpuLightType;
        public HDLightType lightType;
        public float lightDistanceFade;
        public float lightVolumetricDistanceFade;
        public float distanceToCamera;
        public HDProcessedVisibleLightsBuilder.ShadowMapFlags shadowMapFlags;
        public bool isBakedShadowMask;
    }

    //Class representing lights in the context of a view.
    internal partial class HDProcessedVisibleLightsBuilder
    {
        #region internal HDRP API
        [Flags]
        internal enum ShadowMapFlags
        {
            None = 0,
            WillRenderShadowMap = 1 << 0,
            WillRenderScreenSpaceShadow = 1 << 1,
            WillRenderRayTracedShadow = 1 << 2
        }

        //Member lights counts
        public int sortedLightCounts => m_ProcessVisibleLightCounts.IsCreated ? m_ProcessVisibleLightCounts[(int)ProcessLightsCountSlots.ProcessedLights] : 0;
        public int sortedDirectionalLightCounts => m_ProcessVisibleLightCounts.IsCreated ? m_ProcessVisibleLightCounts[(int)ProcessLightsCountSlots.DirectionalLights] : 0;
        public int sortedNonDirectionalLightCounts => sortedLightCounts - sortedDirectionalLightCounts;
        public int bakedShadowsCount => m_ProcessVisibleLightCounts.IsCreated ? m_ProcessVisibleLightCounts[(int)ProcessLightsCountSlots.BakedShadows] : 0;
        public int sortedDGILightCounts => m_ProcessDynamicGILightCounts.IsCreated ? m_ProcessDynamicGILightCounts[(int)ProcessLightsCountSlots.ProcessedLights] : 0;

        //Indexed by VisibleLights
        public NativeArray<LightBakingOutput> visibleLightBakingOutput => m_VisibleLightBakingOutput;
        public NativeArray<LightShadowCasterMode> visibleLightShadowCasterMode => m_VisibleLightShadowCasterMode;
        public NativeArray<int> visibleLightEntityDataIndices => m_VisibleLightEntityDataIndices;
        public NativeArray<HDProcessedVisibleLight> processedEntities => m_ProcessedEntities;
        public NativeArray<float> visibleLightBounceIntensity => m_VisibleLightBounceIntensity;

        //Indexed by sorted lights.
        public NativeArray<uint> sortKeys => m_SortKeys;
        public NativeArray<uint> sortKeysDGI => m_SortKeysDGI;
        public NativeArray<uint> sortSupportArray => m_SortSupportArray;
        public NativeArray<int> shadowLightsDataIndices => m_ShadowLightsDataIndices;

        //Other
        public NativeArray<VisibleLight> offscreenDynamicGILights => m_OffscreenDynamicGILights;

        //Resets internal size of processed lights.
        public void Reset()
        {
            m_Size = 0;
        }

        //Builds sorted HDProcessedVisibleLight structures.
        public void Build(
            HDCamera hdCamera,
            in CullingResults cullingResult,
            in List<Light> allEnabledLights,
            in List<HDAdditionalLightData> allEnabledLightsAdditionalData,
            HDShadowManager shadowManager,
            in HDShadowInitParameters inShadowInitParameters,
            in AOVRequestData aovRequestData,
            in GlobalLightLoopSettings lightLoopSettings,
            DebugDisplaySettings debugDisplaySettings,
            bool processDynamicGI)
        {
            BuildVisibleLightEntities(cullingResult, allEnabledLights, allEnabledLightsAdditionalData, processDynamicGI);

            if (m_Size == 0)
                return;

            FilterVisibleLightsByAOV(aovRequestData);
            StartProcessVisibleLightJob(hdCamera, cullingResult.visibleLights, m_OffscreenDynamicGILights, lightLoopSettings, debugDisplaySettings, processDynamicGI);
            CompleteProcessVisibleLightJob();
            SortLightKeys();
            ProcessShadows(hdCamera, shadowManager, inShadowInitParameters, cullingResult);
        }

        #endregion

        #region private definitions

        private enum ProcessLightsCountSlots
        {
            ProcessedLights,
            DirectionalLights,
            PunctualLights,
            AreaLightCounts,
            ShadowLights,
            BakedShadows,
        }

        private const int ArrayCapacity = 32;

        private NativeArray<int> m_ProcessVisibleLightCounts;
        private NativeArray<int> m_ProcessDynamicGILightCounts;
        private NativeArray<int> m_VisibleLightEntityDataIndices;
        private NativeArray<VisibleLight> m_OffscreenDynamicGILights;
        private NativeArray<LightBakingOutput> m_VisibleLightBakingOutput;
        private NativeArray<LightShadowCasterMode> m_VisibleLightShadowCasterMode;
        private NativeArray<LightShadows> m_VisibleLightShadows;
        private NativeArray<float> m_VisibleLightBounceIntensity;
        private NativeArray<HDProcessedVisibleLight> m_ProcessedEntities;

        private int m_Capacity = 0;
        private int m_Size = 0;

        private NativeArray<int> m_OffscreenDgiIndices;

        private int m_OffscreenDgiIndicesCapacity;
        private int m_OffscreenDgiIndicesSize;

        private NativeArray<uint> m_SortKeys;
        private NativeArray<uint> m_SortKeysDGI;
        private NativeArray<uint> m_SortSupportArray;
        private NativeArray<int> m_ShadowLightsDataIndices;

        private void ResizeArrays(int newCapacity)
        {
            m_Capacity = Math.Max(Math.Max(newCapacity, ArrayCapacity), m_Capacity * 2);
            m_VisibleLightEntityDataIndices.ResizeArray(m_Capacity);
            m_OffscreenDynamicGILights.ResizeArray(m_Capacity);
            m_VisibleLightBakingOutput.ResizeArray(m_Capacity);
            m_VisibleLightShadowCasterMode.ResizeArray(m_Capacity);
            m_VisibleLightShadows.ResizeArray(m_Capacity);
            m_VisibleLightBounceIntensity.ResizeArray(m_Capacity);

            m_ProcessedEntities.ResizeArray(m_Capacity);
            m_SortKeys.ResizeArray(m_Capacity);
            m_SortKeysDGI.ResizeArray(m_Capacity);
            m_ShadowLightsDataIndices.ResizeArray(m_Capacity);
        }

        private void ResizeOffscreenDgiIndices(int newCapacity)
        {
            m_OffscreenDgiIndicesCapacity = Math.Max(Math.Max(newCapacity, ArrayCapacity), m_OffscreenDgiIndicesCapacity * 2);
            m_OffscreenDgiIndices.ResizeArray(m_OffscreenDgiIndicesCapacity);
        }

        public void Cleanup()
        {
            if (m_SortSupportArray.IsCreated)
                m_SortSupportArray.Dispose();

            if (m_ProcessVisibleLightCounts.IsCreated)
                m_ProcessVisibleLightCounts.Dispose();

            if (m_ProcessDynamicGILightCounts.IsCreated)
                m_ProcessDynamicGILightCounts.Dispose();

            if (m_OffscreenDgiIndicesCapacity > 0)
            {
                m_OffscreenDgiIndices.Dispose();
            }

            m_OffscreenDgiIndicesCapacity = 0;
            m_OffscreenDgiIndicesSize = 0;

            if (m_Capacity == 0)
                return;

            m_VisibleLightEntityDataIndices.Dispose();
            m_OffscreenDynamicGILights.Dispose();
            m_VisibleLightBakingOutput.Dispose();
            m_VisibleLightShadowCasterMode.Dispose();
            m_VisibleLightShadows.Dispose();
            m_VisibleLightBounceIntensity.Dispose();

            m_ProcessedEntities.Dispose();
            m_SortKeys.Dispose();
            m_SortKeysDGI.Dispose();
            m_ShadowLightsDataIndices.Dispose();

            m_Capacity = 0;
            m_Size = 0;
        }

        #endregion
    }
}
