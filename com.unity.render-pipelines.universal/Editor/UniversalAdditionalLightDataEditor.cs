using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEditor.Rendering.Universal
{
    /// <summary>
    /// Editor script for a <c>UniversalAdditionalLightData</c> class.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UniversalAdditionalLightData))]
    public class UniversalAdditionLightDataEditor : Editor
    {
        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
        }

        [MenuItem("CONTEXT/UniversalAdditionalLightData/Remove Component")]
        static void RemoveComponent(MenuCommand command)
        {
            RemoveAdditionalDataUtils.RemoveAdditionalData<UniversalAdditionalLightData>(command);
        }
    }
}
