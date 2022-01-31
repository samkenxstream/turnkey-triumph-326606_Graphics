using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A volume component that holds settings for the Panini Projection effect.
    /// </summary>
    [Serializable, VolumeComponentMenu("Post-processing/Panini Projection"), SupportedOn(typeof(UniversalRenderPipeline))]
    public sealed class PaniniProjection : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Controls the panini projection distance. This controls the strength of the distorion.
        /// </summary>
        [Tooltip("Panini projection distance.")]
        public ClampedFloatParameter distance = new ClampedFloatParameter(0f, 0f, 1f);

        /// <summary>
        /// Controls how much cropping HDRP applies to the screen with the panini projection effect. A value of 1 crops the distortion to the edge of the screen.
        /// </summary>
        [Tooltip("Panini projection crop to fit.")]
        public ClampedFloatParameter cropToFit = new ClampedFloatParameter(1f, 0f, 1f);

        /// <inheritdoc/>
        public bool IsActive() => distance.value > 0f;

        /// <inheritdoc/>
        public bool IsTileCompatible() => false;
    }
}
